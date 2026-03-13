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

namespace XIAOFUTools.Tools.MapSheetsSmallAssign
{
    internal class SmallMapSheetIdentifyTool : MapTool
    {
        private static readonly List<IDisposable> Overlays = new List<IDisposable>();
        private static readonly SpatialReference Cgcs2000 = SpatialReferenceBuilder.CreateSpatialReference(4490);

        private static SmallScaleMapSheetOption _option = SmallScaleMapSheetCalculator.GetOption("10万");
        private static long _flashVersion;

        private CIMSymbolReference _lineSymbolReference;

        public static Action<MapSheetIdentifyResult> ResultHandler { get; set; }
        public static Action<bool> ActiveStateHandler { get; set; }

        public SmallMapSheetIdentifyTool()
        {
        }

        public static void Configure(SmallScaleMapSheetOption option)
        {
            _option = option;
        }

        protected override Task OnToolActivateAsync(bool active)
        {
            return QueuedTask.Run(() =>
            {
                ClearOverlays();
                _lineSymbolReference = SymbolFactory.Instance
                    .ConstructLineSymbol(CIMColor.CreateRGBColor(0, 153, 255), 3.2, SimpleLineStyle.Solid)
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
                    return MapSheetGeometryService.IdentifySmallScale(point, _option);
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
                var polygon = MapSheetGeometryService.CreatePolygon(cell, Cgcs2000);
                if (point.SpatialReference != null && point.SpatialReference.Wkid != Cgcs2000.Wkid)
                {
                    polygon = (Polygon)GeometryEngine.Instance.Project(polygon, point.SpatialReference);
                }

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
