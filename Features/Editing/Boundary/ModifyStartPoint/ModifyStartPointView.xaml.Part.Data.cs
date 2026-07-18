using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace XIAOFUTools.Features.Editing.Boundary.ModifyStartPoint
{
    public partial class ModifyStartPointView
    {

        // 加载多边形图层
        private void LoadPolygonLayers()
        {
            try
            {
                var map = MapView.Active?.Map;
                if (map != null)
                {
                    var polygonLayers = map.GetLayersAsFlattenedList().OfType<FeatureLayer>()
                        .Where(l => l.ShapeType == esriGeometryType.esriGeometryPolygon);
                    LayerComboBox.ItemsSource = polygonLayers;
                    LayerComboBox.DisplayMemberPath = "Name";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadPolygonLayers Error: {ex.Message}");
            }
        }

        // 加载角点选项
        private void LoadCornerOptions()
        {
            try
            {
                CornerComboBox.ItemsSource = new[] { "西北角", "东北角", "东南角", "西南角" };
                CornerComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadCornerOptions Error: {ex.Message}");
            }
        }

        // 获取起始点
        private MapPoint GetStartPoint(List<MapPoint> points, string corner, double angleThreshold)
        {
            try
            {
                if (points?.Count == 0) return null;

                var multipoint = MultipointBuilderEx.CreateMultipoint(points);
                var envelope = GeometryEngine.Instance.ConvexHull(multipoint).Extent;
                MapPoint targetPoint = null;

                switch (corner)
                {
                    case "西北角":
                        targetPoint = MapPointBuilderEx.CreateMapPoint(envelope.XMin, envelope.YMax, envelope.SpatialReference);
                        break;
                    case "东北角":
                        targetPoint = MapPointBuilderEx.CreateMapPoint(envelope.XMax, envelope.YMax, envelope.SpatialReference);
                        break;
                    case "东南角":
                        targetPoint = MapPointBuilderEx.CreateMapPoint(envelope.XMax, envelope.YMin, envelope.SpatialReference);
                        break;
                    case "西南角":
                        targetPoint = MapPointBuilderEx.CreateMapPoint(envelope.XMin, envelope.YMin, envelope.SpatialReference);
                        break;
                    default:
                        return points.FirstOrDefault();
                }

                var candidates = points.Where(p => IsValidCornerPoint(p, points, angleThreshold)).ToList();
                if (candidates.Count == 0)
                {
                    // 若角度筛选无结果，则以最近角距离作为候选，避免退回到第一个点导致“无变化”
                    return points.OrderBy(p => GeometryEngine.Instance.Distance(p, targetPoint))
                                 .FirstOrDefault();
                }
                return candidates.OrderBy(p => GeometryEngine.Instance.Distance(p, targetPoint))
                                 .FirstOrDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetStartPoint Error: {ex.Message}");
                return points?.FirstOrDefault();
            }
        }

        // 计算角度
        private double CalculateAngle(MapPoint p1, MapPoint p2, MapPoint p3)
        {
            try
            {
                double angle1 = Math.Atan2(p1.Y - p2.Y, p1.X - p2.X);
                double angle2 = Math.Atan2(p3.Y - p2.Y, p3.X - p2.X);
                double result = Math.Abs(angle1 - angle2) * 180 / Math.PI;
                return result > 180 ? 360 - result : result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CalculateAngle Error: {ex.Message}");
                return 180; // 返回默认值
            }
        }
    }
}
