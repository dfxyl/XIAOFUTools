using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.Analysis.ExtractPolygonHoles
{
    internal partial class ExtractPolygonHolesViewModel
    {

        public async void RefreshLayers()
        {
            try
            {
                var layers = await QueuedTask.Run(() =>
                {
                    var map = MapView.Active?.Map;
                    if (map == null)
                        return new List<FeatureLayer>();

                    return map.GetLayersAsFlattenedList()
                        .OfType<FeatureLayer>()
                        .Where(fl => fl.ShapeType == esriGeometryType.esriGeometryPolygon)
                        .ToList();
                });

                PolygonLayers.Clear();
                foreach (var layer in layers)
                    PolygonLayers.Add(layer);

                if (PolygonLayers.Count > 0)
                {
                    if (SelectedPolygonLayer == null || !PolygonLayers.Contains(SelectedPolygonLayer))
                        SelectedPolygonLayer = PolygonLayers[0];

                    AddLog($"已加载 {PolygonLayers.Count} 个面要素图层");
                }
                else
                {
                    SelectedPolygonLayer = null;
                    AddLog("当前地图中未找到面要素图层");
                }
            }
            catch (Exception ex)
            {
                AddLog($"刷新图层失败: {ex.Message}");
            }
        }

        private void UpdateDefaultOutputPath()
        {
            var defaultName = GetDefaultOutputName();
            var gdbPath = Project.Current?.DefaultGeodatabasePath;

            if (!string.IsNullOrWhiteSpace(gdbPath))
                OutputPath = Path.Combine(gdbPath, defaultName);
            else if (string.IsNullOrWhiteSpace(OutputPath))
                OutputPath = defaultName;
        }

        private void BrowseOutput()
        {
            try
            {
                var dialog = new SaveItemDialog
                {
                    Title = "选择输出面要素",
                    OverwritePrompt = true,
                    Filter = ItemFilters.FeatureClasses_All
                };

                var initialLocation = Project.Current?.DefaultGeodatabasePath;
                if (!string.IsNullOrWhiteSpace(initialLocation))
                    dialog.InitialLocation = initialLocation;

                if (dialog.ShowDialog() == true)
                    OutputPath = dialog.FilePath;
            }
            catch (Exception ex)
            {
                AddLog($"选择输出路径失败: {ex.Message}");
            }
        }

        private void ShowHelp()
        {
            AddLog("帮助: 提取输入面中的所有扣洞，输出面要素并继承源图层属性。可勾选将同一源要素的多个洞合并为一个多部件要素。");
        }

        private static void InsertPolygon(
            Feature sourceFeature,
            Polygon outputPolygon,
            string outputShapeField,
            List<FieldMapping> fieldMappings,
            FeatureClass outputFeatureClass,
            InsertCursor insertCursor)
        {
            using var rowBuffer = outputFeatureClass.CreateRowBuffer();
            rowBuffer[outputShapeField] = outputPolygon;

            foreach (var mapping in fieldMappings)
            {
                try
                {
                    var value = sourceFeature[mapping.SourceFieldName];
                    if (value == null || value == DBNull.Value)
                        continue;

                    rowBuffer[mapping.OutputFieldName] = value;
                }
                catch
                {
                    // 目标字段可能不可编辑或类型不兼容，忽略后继续复制其他字段。
                }
            }

            insertCursor.Insert(rowBuffer);
        }

        private static List<Polygon> ExtractHolePolygons(Polygon polygon)
        {
            var simplifiedPolygon = GeometryEngine.Instance.SimplifyAsFeature(polygon) as Polygon ?? polygon;
            var ringGeometries = new List<RingGeometry>();

            foreach (var part in simplifiedPolygon.Parts)
            {
                var points = GetPartPoints(part);
                if (points.Count < 4)
                    continue;

                var signedArea = ComputeSignedArea(points);
                var area = Math.Abs(signedArea);
                if (area <= double.Epsilon)
                    continue;

                var ringPolygon = PolygonBuilderEx.CreatePolygon(points, simplifiedPolygon.SpatialReference);
                if (ringPolygon == null || ringPolygon.IsEmpty)
                    continue;

                ringGeometries.Add(new RingGeometry(ringPolygon, signedArea));
            }

            if (ringGeometries.Count == 0)
                return new List<Polygon>();

            var largestRing = ringGeometries.OrderByDescending(r => Math.Abs(r.SignedArea)).First();
            var exteriorSign = Math.Sign(largestRing.SignedArea);
            if (exteriorSign == 0)
                exteriorSign = -1;

            var holePolygons = new List<Polygon>();
            foreach (var ring in ringGeometries)
            {
                if (Math.Sign(ring.SignedArea) == exteriorSign)
                    continue;

                var simplifiedRing = GeometryEngine.Instance.SimplifyAsFeature(ring.RingPolygon) as Polygon ?? ring.RingPolygon;
                if (!simplifiedRing.IsEmpty)
                    holePolygons.Add(simplifiedRing);
            }

            return holePolygons;
        }

        private void AddLog(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            LogContent += $"[{DateTime.Now:HH:mm:ss}] {message}\r\n";
        }
    }
}
