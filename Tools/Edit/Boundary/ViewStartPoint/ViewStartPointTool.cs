using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace XIAOFUTools.Tools.Edit.Boundary.ViewStartPoint
{
    internal class ViewStartPointTool : MapTool
    {
        private CIMSymbolReference _symbolReference; // 外环起点符号
        private CIMSymbolReference _innerSymbolReference; // 内环起点符号
        private IDisposable _graphic; // 当前显示的外环起点图形
        private List<IDisposable> _innerGraphics = new List<IDisposable>(); // 当前显示的内环起点图形
        private List<IDisposable> _textGraphics = new List<IDisposable>(); // 当前显示的文本图形

        public ViewStartPointTool()
        {
            IsSketchTool = true;
            SketchType = SketchGeometryType.Point;
            SketchOutputMode = SketchOutputMode.Map;
            UseSnapping = true;
        }

        protected override Task OnToolActivateAsync(bool active)
        {
            return QueuedTask.Run(() =>
            {
                // 创建外环起点的符号
                var outerSymbol = SymbolFactory.Instance.ConstructPointSymbol(
                    ColorFactory.Instance.RedRGB, 7, SimpleMarkerStyle.Circle);
                _symbolReference = outerSymbol.MakeSymbolReference();

                // 创建内环起点的符号
                var innerSymbol = SymbolFactory.Instance.ConstructPointSymbol(
                    ColorFactory.Instance.BlueRGB, 6, SimpleMarkerStyle.Square);
                _innerSymbolReference = innerSymbol.MakeSymbolReference();
            });
        }

        protected override Task OnToolDeactivateAsync(bool hasMapViewChanged)
        {
            return QueuedTask.Run(() =>
            {
                ClearGraphics(); // 清除所有图形
            });
        }

        protected override void OnToolMouseDown(MapViewMouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                e.Handled = true; // 处理左键点击事件
        }

        protected override Task HandleMouseDownAsync(MapViewMouseButtonEventArgs e)
        {
            return QueuedTask.Run(() =>
            {
                var mapPoint = MapView.Active.ClientToMap(e.ClientPoint);
                if (Project.Current.IsEditingEnabled)
                {
                    HandleEditingMode(mapPoint);
                }
                else
                {
                    HandleViewingMode(mapPoint);
                }
            });
        }

        private void HandleEditingMode(MapPoint clickPoint)
        {
            ClearGraphics(); // 清除现有图形
            if (AutoSelectFeature(clickPoint))
            {
                var mapView = MapView.Active;
                if (mapView == null) return;
                var map = mapView.Map;
                if (map == null) return;
                var selectedSet = map.GetSelection();
                var layer = selectedSet.ToDictionary().FirstOrDefault();
                FeatureLayer featureLayer = layer.Key as FeatureLayer;

                if (featureLayer != null && featureLayer.ShapeType == esriGeometryType.esriGeometryPolygon)
                {
                    using (var rowCursor = featureLayer.GetSelection().Search())
                    {
                        if (rowCursor.MoveNext())
                        {
                            using (var feature = rowCursor.Current as Feature)
                            {
                                if (feature != null)
                                {
                                    var polygon = feature.GetShape() as Polygon;
                                    if (polygon != null)
                                    {
                                        SetCustomStartPoint(polygon, clickPoint, feature);
                                        DisplayStartPointGraphics(polygon);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void HandleViewingMode(MapPoint clickPoint)
        {
            ClearGraphics(); // 清除现有图形
            var mapView = MapView.Active;
            if (mapView == null) return;
            var map = mapView.Map;
            if (map == null) return;

            var layers = map.GetLayersAsFlattenedList().OfType<FeatureLayer>()
                .Where(l => IsLayerSelectable(l) && l.ShapeType == esriGeometryType.esriGeometryPolygon);

            foreach (var layer in layers)
            {
                var spatialQuery = new SpatialQueryFilter
                {
                    FilterGeometry = clickPoint,
                    SpatialRelationship = SpatialRelationship.Intersects
                };

                using (var selection = layer.Select(spatialQuery))
                {
                    var objectIds = selection.GetObjectIDs();
                    if (objectIds != null && objectIds.Count > 0)
                    {
                        var oid = objectIds.First();
                        ProcessSelectedFeature(layer, oid);
                        return;
                    }
                }
            }
        }

        private bool AutoSelectFeature(MapPoint clickPoint)
        {
            var view = MapView.Active;
            var map = view.Map;

            var layers = map.GetLayersAsFlattenedList().OfType<FeatureLayer>()
                             .Where(l => IsLayerSelectable(l) && l.ShapeType == esriGeometryType.esriGeometryPolygon);

            foreach (var layer in layers)
            {
                var spatialQuery = new SpatialQueryFilter
                {
                    FilterGeometry = clickPoint,
                    SpatialRelationship = SpatialRelationship.Intersects
                };

                using (var selection = layer.Select(spatialQuery))
                {
                    var objectIds = selection.GetObjectIDs();
                    if (objectIds != null && objectIds.Count > 0)
                    {
                        var selectionDict = new Dictionary<MapMember, List<long>>
                        {
                            { layer, objectIds.ToList() }
                        };

                        map.SetSelection(SelectionSet.FromDictionary(selectionDict));
                        return true;
                    }
                }
            }

            return false;
        }

        private void SetCustomStartPoint(Polygon polygon, MapPoint clickPoint, Feature feature)
        {
            var points = polygon.Points.ToList();
            var startPoint = GetStartPoint(points, clickPoint, 0); // 不再使用面积

            if (startPoint != null)
            {
                List<double> xy = new List<double>() { startPoint.X, startPoint.Y };
                Polygon resultPolygon = ReshotMapPointReturnPolygonByCustom(polygon, xy);
                feature.SetShape(resultPolygon);
                feature.Store();

                // 实时更新图形
                DisplayStartPointGraphics(resultPolygon);
            }
        }

        private void DisplayStartPointGraphics(Polygon polygon)
        {
            ClearGraphics(); // 清除现有图形
            var view = MapView.Active;  // 缓存 MapView 对象

            var points = polygon.Points;
            if (points == null || !points.Any())
                return;

            // 显示外环起点
            var outerStartPoint = points.First();
            if (outerStartPoint != null)
            {
                _graphic = view.AddOverlay(outerStartPoint, _symbolReference);
                AddTextGraphic(outerStartPoint, "外");

                // 显示内环起点
                var parts = polygon.Parts;
                for (int i = 1; i < parts.Count; i++)
                {
                    var part = parts[i];
                    var segment = part.FirstOrDefault();
                    if (segment != null)
                    {
                        var innerStartPoint = segment.StartPoint;
                        var innerGraphic = view.AddOverlay(innerStartPoint, _innerSymbolReference);
                        _innerGraphics.Add(innerGraphic);
                        AddTextGraphic(innerStartPoint, $"内{i}");
                    }
                }
            }
        }

        private void ProcessSelectedFeature(FeatureLayer layer, long oid)
        {
            using (var rowCursor = layer.Search(new QueryFilter { ObjectIDs = new[] { oid } }))
            {
                if (rowCursor.MoveNext())
                {
                    using (var feature = rowCursor.Current as Feature)
                    {
                        if (feature != null)
                        {
                            var featureGeometry = feature.GetShape() as Polygon;
                            if (featureGeometry != null)
                            {
                                DisplayStartPointGraphics(featureGeometry);
                            }
                        }
                    }
                }
            }
        }

        private void AddTextGraphic(MapPoint point, string label)
        {
            var textSymbol = SymbolFactory.Instance.ConstructTextSymbol(ColorFactory.Instance.BlackRGB, 6, "Arial", "Normal");
            var textGraphic = new CIMTextGraphic
            {
                Symbol = textSymbol.MakeSymbolReference(),
                Text = $"{label}\nX: {point.Y:F3}\nY: {point.X:F3}",
                Shape = point
            };
            var overlay = MapView.Active.AddOverlay(textGraphic);
            _textGraphics.Add(overlay);
        }

        private void ClearGraphics()
        {
            if (_graphic != null)
            {
                _graphic.Dispose();
                _graphic = null;
            }

            foreach (var graphic in _innerGraphics)
            {
                graphic.Dispose();
            }
            _innerGraphics.Clear();

            foreach (var textGraphic in _textGraphics)
            {
                textGraphic.Dispose();
            }
            _textGraphics.Clear();
        }

        private Polygon ReshotMapPointReturnPolygonByCustom(Polygon polygon, List<double> xy)
        {
            var polygonBuilder = new PolygonBuilderEx(polygon.SpatialReference);

            foreach (var part in polygon.Parts)
            {
                var points = ExtractPointsFromSegments(part);
                var reorderedPoints = ReorderRingPoints(points, polygon.Parts.First() == part, xy, 0); // 保留排序
                polygonBuilder.AddPart(reorderedPoints);
            }

            return polygonBuilder.ToGeometry();
        }

        private List<MapPoint> ReorderRingPoints(List<MapPoint> points, bool isExterior, List<double> xy, double area)
        {
            if (points.Count < 3)
            {
                return points;
            }

            var spatialReference = points[0].SpatialReference;
            var clickPoint = MapPointBuilderEx.CreateMapPoint(xy[0], xy[1], spatialReference);

            MapPoint startPoint = GetStartPoint(points, clickPoint, area);
            if (startPoint == null)
            {
                return points;
            }

            int startIndex = points.IndexOf(startPoint);
            int count = points.Count - 1; // 假定最后一个点与首点重复，表示闭合环

            var reorderedPoints = new List<MapPoint>(count + 1);
            for (int i = 0; i < count; i++)
            {
                int index;
                if (isExterior)
                {
                    index = (startIndex + i) % count;
                }
                else
                {
                    index = ((startIndex - i + count) % count);
                }
                reorderedPoints.Add(points[index]);
            }
            // 关闭环
            reorderedPoints.Add(reorderedPoints[0]);

            return reorderedPoints;
        }

        private List<MapPoint> ExtractPointsFromSegments(ReadOnlySegmentCollection segments)
        {
            var points = new List<MapPoint>();
            foreach (var segment in segments)
            {
                if (segment is LineSegment lineSegment)
                {
                    points.Add(lineSegment.StartPoint);
                }
            }
            if (segments.Count > 0 && segments.Last() is LineSegment lastSegment)
            {
                points.Add(lastSegment.EndPoint);
            }
            return points;
        }

        private MapPoint GetStartPoint(List<MapPoint> points, MapPoint clickPoint, double area)
        {
            if (points == null || points.Count == 0 || clickPoint == null)
            {
                return null;
            }

            var view = MapView.Active;
            var camera = view.Camera;
            double searchDistanceMeters = camera.Scale * 2 / 1000.0;

            // 将点击点投影到环点的空间参考，避免“不兼容的空间参考”异常
            var targetSR = points[0].SpatialReference;
            MapPoint clickInRingSR = clickPoint;
            if (clickPoint.SpatialReference == null || !clickPoint.SpatialReference.Equals(targetSR))
            {
                clickInRingSR = (MapPoint)GeometryEngine.Instance.Project(clickPoint, targetSR);
            }

            // 将搜索距离（米）转换为目标参考系的单位（若为投影坐标系）
            double searchTolerance = searchDistanceMeters;
            if (targetSR != null && targetSR.IsProjected && targetSR.Unit is LinearUnit lu && lu.MetersPerUnit > 0)
            {
                searchTolerance = searchDistanceMeters / lu.MetersPerUnit;
            }

            foreach (var point in points)
            {
                if (GeometryEngine.Instance.Distance(point, clickInRingSR) <= searchTolerance)
                    return point;
            }
            return null;
        }

        private bool IsLayerSelectable(FeatureLayer layer)
        {
            return layer.IsVisible && layer.IsSelectable && IsLayerGroupVisible(layer);
        }

        private bool IsLayerGroupVisible(FeatureLayer layer)
        {
            var groupLayer = layer.Parent as GroupLayer;
            while (groupLayer != null)
            {
                if (!groupLayer.IsVisible)
                {
                    return false;
                }
                groupLayer = groupLayer.Parent as GroupLayer;
            }
            return true;
        }
    }
}