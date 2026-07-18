using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Framework.Events;
using ArcGIS.Desktop.Editing;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using XIAOFUTools.Shared.Presentation;

namespace XIAOFUTools.Features.DataManagement.RotateGeometry
{
    internal partial class RotateGeometryDockPaneViewModel
    {
        private void LoadNumericFields()
        {
            NumericFields.Clear();
            if (SelectedLayer == null) return;
            Task.Run(async () =>
            {
                try
                {
                    var names = new List<string>();
                    await QueuedTask.Run(() =>
                    {
                        using (var table = SelectedLayer.GetTable())
                        {
                            var def = table.GetDefinition();
                            foreach (var f in def.GetFields())
                            {
                                if (f.FieldType == FieldType.Double || f.FieldType == FieldType.Single || f.FieldType == FieldType.Integer || f.FieldType == FieldType.SmallInteger)
                                {
                                    names.Add(f.Name);
                                }
                            }
                        }
                    });
                    PresentationServices.UiThread.InvokeOrRun(() =>
                    {
                        foreach (var n in names) NumericFields.Add(n);
                        if (NumericFields.Count > 0 && string.IsNullOrEmpty(SelectedAngleField))
                            SelectedAngleField = NumericFields[0];
                    });
                }
                catch (Exception ex)
                {
                    PresentationServices.UiThread.InvokeOrRun(() => AppendLog($"读取字段失败: {ex.Message}"));
                }
            });
        }

        private double GetAngleDegreesForRow(Row row)
        {
            if (UseConstantAngle)
            {
                var angle = ConstantAngleDegrees;
                if (string.Equals(AngleDirection, "顺时针")) angle = -Math.Abs(angle);
                else angle = Math.Abs(angle);
                return angle;
            }
            else if (UseFieldAngle && !string.IsNullOrEmpty(SelectedAngleField))
            {
                try
                {
                    var obj = row[SelectedAngleField];
                    if (obj == null) return 0.0;
                    double ang;
                    if (obj is double d) ang = d;
                    else if (obj is float f) ang = f;
                    else if (obj is int i) ang = i;
                    else if (obj is short s) ang = s;
                    else if (!double.TryParse(Convert.ToString(obj, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out ang))
                        return 0.0;
                    if (string.Equals(AngleDirection, "顺时针")) ang = -Math.Abs(ang); else ang = Math.Abs(ang);
                    return ang;
                }
                catch { return 0.0; }
            }
            return 0.0;
        }

        private MapPoint GetPolylineAnchorPoint(Polyline pl, string anchor)
        {
            try
            {
                var pts = pl.Points?.ToList();
                if (pts == null || pts.Count == 0) return null;

                if (anchor == "起点")
                {
                    return pts.First();
                }
                else if (anchor == "终点")
                {
                    return pts.Last();
                }
                else // 中点
                {
                    // 沿顶点线性插值的半程点
                    double totalLen = 0;
                    for (int i = 0; i < pts.Count - 1; i++)
                    {
                        totalLen += Distance2D(pts[i], pts[i + 1]);
                    }
                    if (totalLen <= 0) return pts.First();
                    double half = totalLen / 2.0;
                    double acc = 0;
                    for (int i = 0; i < pts.Count - 1; i++)
                    {
                        double seg = Distance2D(pts[i], pts[i + 1]);
                        if (acc + seg >= half)
                        {
                            double t = (half - acc) / seg;
                            return Interpolate(pts[i], pts[i + 1], t);
                        }
                        acc += seg;
                    }
                    return pts.Last();
                }
            }
            catch { return null; }
        }

        private MapPoint GetPolygonAnchorPoint(Polygon pg, string anchor)
        {
            try
            {
                if (anchor == "起点")
                {
                    var part = pg.Parts?.FirstOrDefault();
                    var seg = part?.FirstOrDefault();
                    return seg?.StartPoint;
                }
                else if (anchor == "质心")
                {
                    var c = GeometryEngine.Instance.Centroid(pg) as MapPoint;
                    if (c == null || double.IsNaN(c.X) || double.IsNaN(c.Y))
                        c = GeometryEngine.Instance.LabelPoint(pg) as MapPoint;
                    return c;
                }
                else if (anchor == "标签点")
                {
                    return GeometryEngine.Instance.LabelPoint(pg) as MapPoint;
                }
                else if (anchor == "包络中心")
                {
                    var env = pg.Extent;
                    if (env == null) return null;
                    var sr = pg.SpatialReference;
                    return MapPointBuilderEx.CreateMapPoint((env.XMin + env.XMax) / 2.0, (env.YMin + env.YMax) / 2.0, sr);
                }
                else if (anchor == "左下角" || anchor == "左上角" || anchor == "右下角" || anchor == "右上角")
                {
                    var env = pg.Extent;
                    if (env == null) return null;
                    var sr = pg.SpatialReference;
                    double x = 0, y = 0;
                    switch (anchor)
                    {
                        case "左下角": x = env.XMin; y = env.YMin; break;
                        case "左上角": x = env.XMin; y = env.YMax; break;
                        case "右下角": x = env.XMax; y = env.YMin; break;
                        case "右上角": x = env.XMax; y = env.YMax; break;
                    }
                    return MapPointBuilderEx.CreateMapPoint(x, y, sr);
                }
                // 默认回退：质心
                var c2 = GeometryEngine.Instance.Centroid(pg) as MapPoint;
                if (c2 == null || double.IsNaN(c2.X) || double.IsNaN(c2.Y))
                    c2 = GeometryEngine.Instance.LabelPoint(pg) as MapPoint;
                return c2;
            }
            catch { return null; }
        }
    }
}
