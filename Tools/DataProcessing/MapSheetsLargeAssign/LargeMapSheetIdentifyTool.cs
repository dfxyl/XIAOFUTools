using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Common;

namespace XIAOFUTools.Tools.MapSheetsLargeAssign
{
    internal class LargeMapSheetIdentifyTool : MapTool
    {
        private static readonly List<IDisposable> Overlays = new List<IDisposable>();

        private static LargeScaleMapSheetOption _option = LargeScaleMapSheetCalculator.GetOption("1:2000/50*50");
        private static string _namingConvention = "X-Y";
        private static int _decimalPlaces = 3;
        private static long _flashVersion;
        private CIMSymbolReference _lineSymbolReference;

        public static Action<MapSheetIdentifyResult> ResultHandler { get; set; }
        public static Action<bool> ActiveStateHandler { get; set; }

        public LargeMapSheetIdentifyTool()
        {
        }

        public static void Configure(LargeScaleMapSheetOption option, string namingConvention, int decimalPlaces)
        {
            _option = option;
            _namingConvention = namingConvention;
            _decimalPlaces = decimalPlaces;
        }

        protected override Task OnToolActivateAsync(bool active)
        {
            return QueuedTask.Run(() =>
            {
                ClearOverlays();
                _lineSymbolReference = SymbolFactory.Instance
                    .ConstructLineSymbol(CIMColor.CreateRGBColor(255, 170, 0), 3.2, SimpleLineStyle.Solid)
                    .MakeSymbolReference();
            }).ContinueWith(_ => ActiveStateHandler?.Invoke(true));
        }

        protected override Task OnToolDeactivateAsync(bool hasMapViewChanged)
        {
            Interlocked.Increment(ref _flashVersion);
            return QueuedTask.Run(ClearOverlays).ContinueWith(_ => ActiveStateHandler?.Invoke(false));
        }

        protected override void OnToolMouseDown(MapViewMouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                e.Handled = true;
            }
        }

        protected override async Task HandleMouseDownAsync(MapViewMouseButtonEventArgs e)
        {
            if (e.ChangedButton != System.Windows.Input.MouseButton.Left)
            {
                return;
            }

            MapPoint point = null;
            try
            {
                point = await QueuedTask.Run(() => MapView.Active?.ClientToMap(e.ClientPoint));
                if (point == null)
                {
                    return;
                }

                var result = await QueuedTask.Run(() =>
                {
                    return MapSheetGeometryService.IdentifyLargeScale(point, _option, _namingConvention, _decimalPlaces);
                });

                ResultHandler?.Invoke(result);
                await FlashResultAsync(point, result);
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"查询失败: {ex.Message}", "错误");
            }
        }

        private async Task FlashResultAsync(MapPoint point, MapSheetIdentifyResult result)
        {
            var version = Interlocked.Increment(ref _flashVersion);

            if (result == null || !result.HasResult)
            {
                await QueuedTask.Run(ClearOverlays);
                return;
            }

            for (var i = 0; i < 2; i++)
            {
                if (version != Interlocked.Read(ref _flashVersion))
                {
                    return;
                }

                await QueuedTask.Run(() => ShowOverlays(point, result));
                await Task.Delay(180);

                if (version != Interlocked.Read(ref _flashVersion))
                {
                    return;
                }

                await QueuedTask.Run(ClearOverlays);
                await Task.Delay(90);
            }
        }

        private void ShowOverlays(MapPoint point, MapSheetIdentifyResult result)
        {
            var mapView = MapView.Active;
            if (mapView == null)
            {
                return;
            }

            ClearOverlays();
            foreach (var cell in result.Cells)
            {
                var polygon = MapSheetGeometryService.CreatePolygon(cell, point.SpatialReference);
                var boundary = GeometryEngine.Instance.Boundary(polygon);
                Overlays.Add(mapView.AddOverlay(boundary, _lineSymbolReference));
            }
        }

        private static void ClearOverlays()
        {
            foreach (var overlay in Overlays.ToList())
            {
                overlay.Dispose();
            }

            Overlays.Clear();
        }
    }
}
