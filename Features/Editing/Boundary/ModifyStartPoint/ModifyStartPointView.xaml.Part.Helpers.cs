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

        // 从线段中提取点
        private List<MapPoint> ExtractPointsFromSegments(ReadOnlySegmentCollection segments)
        {
            var points = new List<MapPoint>();
            try
            {
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
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ExtractPointsFromSegments Error: {ex.Message}");
            }
            return points;
        }

        // 重新排序环的点
        private List<MapPoint> ReorderRingPoints(List<MapPoint> points, bool isExterior, string corner, double angleThreshold)
        {
            try
            {
                if (points?.Count < 3)
                {
                    return points;
                }

                MapPoint startPoint = GetStartPoint(points, corner, angleThreshold);
                if (startPoint == null) return points;

                int startIndex = points.IndexOf(startPoint);
                if (startIndex == -1) return points;

                var reorderedPoints = new List<MapPoint>();
                if (isExterior)
                {
                    for (int i = 0; i < points.Count - 1; i++)
                    {
                        reorderedPoints.Add(points[(startIndex + i) % (points.Count - 1)]);
                    }
                }
                else
                {
                    for (int i = 0; i < points.Count - 1; i++)
                    {
                        reorderedPoints.Add(points[(startIndex - i + points.Count - 1) % (points.Count - 1)]);
                    }
                }

                reorderedPoints.Add(reorderedPoints[0]);
                return reorderedPoints;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ReorderRingPoints Error: {ex.Message}");
                return points;
            }
        }

        // 判断是否为有效的角点
        private bool IsValidCornerPoint(MapPoint point, List<MapPoint> allPoints, double angleThreshold)
        {
            try
            {
                int index = allPoints.IndexOf(point);
                if (index == -1) return false;

                MapPoint prevPoint = allPoints[(index - 1 + allPoints.Count) % allPoints.Count];
                MapPoint nextPoint = allPoints[(index + 1) % allPoints.Count];

                double angle = CalculateAngle(prevPoint, point, nextPoint);
                return angle <= angleThreshold;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"IsValidCornerPoint Error: {ex.Message}");
                return false;
            }
        }
    }
}
