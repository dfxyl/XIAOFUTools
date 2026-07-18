using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Framework.Events;
using XIAOFUTools.Features.Editing.Boundary.Shared.Core;
using XIAOFUTools.Features.Editing.Boundary.Shared.Infrastructure;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.Editing.Boundary.BoundaryPointLineGenerator
{
    internal partial class BoundaryPointLineGeneratorDockPaneViewModel
    {
        private async Task GeneratePointFeatures(string outputFeatureClassPath)
        {
            try
            {
                using var inputTable = SelectedPolygonLayer.GetTable();
                // 计算总量：若使用选择集则为选择数量，否则为全部数量
                bool useSelection = UseSelection && SelectionUtils.GetSelectionCount(SelectedPolygonLayer) > 0;
                int total = 0;
                if (useSelection)
                {
                    total = SelectionUtils.GetSelectionCount(SelectedPolygonLayer);
                    LogInfo($"检测到选择集: {total} 个要素，将仅处理选择的要素。");
                }
                else
                {
                    using (var c = inputTable.Search()) { while (c.MoveNext()) total++; }
                    LogInfo($"未检测到选择集，将处理全部 {total} 个要素。");
                }
                int processed = 0; int globalBSM = 1;

                FeatureClass featureClass = null;
                Geodatabase gdb = null;
                FileSystemDatastore fsds = null;
                try
                {
                    // 打开输出要素类（GDB 或 Shapefile）
                    var catalogPath = outputFeatureClassPath;
                    if (catalogPath.EndsWith(".shp", StringComparison.OrdinalIgnoreCase))
                    {
                        var folder = Path.GetDirectoryName(catalogPath);
                        var shpName = Path.GetFileName(catalogPath);
                        var conn = new FileSystemConnectionPath(new Uri(folder), FileSystemDatastoreType.Shapefile);
                        fsds = new FileSystemDatastore(conn);
                        featureClass = fsds.OpenDataset<FeatureClass>(shpName);
                    }
                    else
                    {
                        int gdbIdx = catalogPath.IndexOf(".gdb", StringComparison.OrdinalIgnoreCase);
                        if (gdbIdx >= 0)
                        {
                            var gdbRoot = catalogPath.Substring(0, gdbIdx + 4);
                            var relative = catalogPath.Length > gdbIdx + 4 ? catalogPath.Substring(gdbIdx + 4).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : string.Empty;
                            gdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(gdbRoot)));
                            featureClass = gdb.OpenDataset<FeatureClass>(relative);
                        }
                        else
                        {
                            var workspace = Path.GetDirectoryName(catalogPath);
                            var featureClassName = Path.GetFileNameWithoutExtension(catalogPath);
                            bool isGdb = workspace.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase);
                            if (isGdb)
                            {
                                gdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(workspace)));
                                featureClass = gdb.OpenDataset<FeatureClass>(featureClassName);
                            }
                            else
                            {
                                var conn = new FileSystemConnectionPath(new Uri(workspace), FileSystemDatastoreType.Shapefile);
                                fsds = new FileSystemDatastore(conn);
                                featureClass = fsds.OpenDataset<FeatureClass>(featureClassName + ".shp");
                            }
                        }
                    }

                    var shapeField = featureClass.GetDefinition().GetShapeField();

                // 按范围获取游标（通用接口）
                using var cursor = SelectionUtils.GetSelectionOrAllCursor(SelectedPolygonLayer, useSelection, new QueryFilter(), false);
                while (cursor.MoveNext())
                {
                    if (CancelRequested) break;
                    var feature = cursor.Current as Feature;
                    var polygon = feature?.GetShape() as Polygon;
                    if (polygon == null) continue;

                    var zdzhdm = GetStringSafe(feature, SelectedCodeFieldName); // 宗地/宗海代码

                    int sxh = 1;
                    foreach (var part in polygon.Parts)
                    {
                        var rawVertices = new List<MapPoint>();
                        foreach (var seg in part)
                        {
                            if (rawVertices.Count == 0)
                            {
                                rawVertices.Add(seg.StartPoint);
                            }
                            rawVertices.Add(seg.EndPoint);
                        }

                        var vertices = ArcGisBoundaryGeometryAdapter.NormalizeRing(rawVertices, 1e-7);

                        foreach (var pt in vertices)
                        {
                            using var rowBuf = featureClass.CreateRowBuffer();
                            rowBuf[shapeField] = pt;
                            TrySet(rowBuf, "BSM", globalBSM++);
                            TrySet(rowBuf, "ZDZHDM", zdzhdm);
                            TrySet(rowBuf, "YSDM", JZDYSDM);
                            TrySet(rowBuf, "JZDH", string.IsNullOrEmpty(JZDHPrefix) ? sxh.ToString() : $"{JZDHPrefix}{sxh}");
                            TrySet(rowBuf, "SXH", sxh);
                            TrySet(rowBuf, "JBLX", null);
                            TrySet(rowBuf, "JZDLX", null);
                            // 坐标字段：按测绘习惯反转XY，写入 XZBZ/YZBZ
                            TrySet(rowBuf, "XZBZ", pt.Y);
                            TrySet(rowBuf, "YZBZ", pt.X);
                            TrySet(rowBuf, "ZZBZ", pt.HasZ ? pt.Z : (double?)null);
                            using var newRow = featureClass.CreateRow(rowBuf); newRow.Store();
                            sxh++;
                        }
                    }

                    processed++;
                    UpdateProgress(processed, total);
                }
                }
                finally
                {
                    featureClass?.Dispose();
                    gdb?.Dispose();
                    fsds?.Dispose();
                }
            }
            catch (Exception ex) { LogError($"生成界址点失败: {ex.Message}"); }
        }

        private async Task GenerateLineFeatures(string outputFeatureClassPath)
        {
            try
            {
                using var inputTable = SelectedPolygonLayer.GetTable();
                // 计算总量：若使用选择集则为选择数量，否则为全部数量
                bool useSelection = UseSelection && SelectionUtils.GetSelectionCount(SelectedPolygonLayer) > 0;
                int total = 0;
                if (useSelection)
                {
                    total = SelectionUtils.GetSelectionCount(SelectedPolygonLayer);
                    LogInfo($"检测到选择集: {total} 个要素，将仅处理选择的要素。");
                }
                else
                {
                    using (var c = inputTable.Search()) { while (c.MoveNext()) total++; }
                    LogInfo($"未检测到选择集，将处理全部 {total} 个要素。");
                }
                int processed = 0; int globalBSM = 1;

                FeatureClass featureClass = null;
                Geodatabase gdb = null;
                FileSystemDatastore fsds = null;
                try
                {
                    var catalogPath = outputFeatureClassPath;
                    if (catalogPath.EndsWith(".shp", StringComparison.OrdinalIgnoreCase))
                    {
                        var folder = Path.GetDirectoryName(catalogPath);
                        var shpName = Path.GetFileName(catalogPath);
                        var conn = new FileSystemConnectionPath(new Uri(folder), FileSystemDatastoreType.Shapefile);
                        fsds = new FileSystemDatastore(conn);
                        featureClass = fsds.OpenDataset<FeatureClass>(shpName);
                    }
                    else
                    {
                        int gdbIdx = catalogPath.IndexOf(".gdb", StringComparison.OrdinalIgnoreCase);
                        if (gdbIdx >= 0)
                        {
                            var gdbRoot = catalogPath.Substring(0, gdbIdx + 4);
                            var relative = catalogPath.Length > gdbIdx + 4 ? catalogPath.Substring(gdbIdx + 4).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : string.Empty;
                            gdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(gdbRoot)));
                            featureClass = gdb.OpenDataset<FeatureClass>(relative);
                        }
                        else
                        {
                            var workspace = Path.GetDirectoryName(catalogPath);
                            var featureClassName = Path.GetFileNameWithoutExtension(catalogPath);
                            bool isGdb = workspace.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase);
                            if (isGdb)
                            {
                                gdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(workspace)));
                                featureClass = gdb.OpenDataset<FeatureClass>(featureClassName);
                            }
                            else
                            {
                                var conn = new FileSystemConnectionPath(new Uri(workspace), FileSystemDatastoreType.Shapefile);
                                fsds = new FileSystemDatastore(conn);
                                featureClass = fsds.OpenDataset<FeatureClass>(featureClassName + ".shp");
                            }
                        }
                    }

                    var shapeField = featureClass.GetDefinition().GetShapeField();

                // 按范围获取游标（通用接口）
                using var cursor = SelectionUtils.GetSelectionOrAllCursor(SelectedPolygonLayer, useSelection, new QueryFilter(), false);
                while (cursor.MoveNext())
                {
                    if (CancelRequested) break;
                    var feature = cursor.Current as Feature;
                    var polygon = feature?.GetShape() as Polygon;
                    if (polygon == null) continue;
                    var zdzhdm = GetStringSafe(feature, SelectedCodeFieldName);

                    foreach (var part in polygon.Parts)
                    {
                        MapPoint last = null;
                        foreach (var seg in part)
                        {
                            var sp = seg.StartPoint; var ep = seg.EndPoint;
                            // 构造线段（两点）
                            var line = ArcGIS.Core.Geometry.PolylineBuilderEx.CreatePolyline(new List<MapPoint> { sp, ep }, polygon.SpatialReference);
                            double length = GeometryEngine.Instance.Length(line);
                            length = BoundaryEdgeMeasurement.RoundLength(length, JZXLengthDecimals);

                            using var rowBuf = featureClass.CreateRowBuffer();
                            rowBuf[shapeField] = line;
                            TrySet(rowBuf, "BSM", globalBSM++);
                            TrySet(rowBuf, "ZDZHDM", zdzhdm);
                            TrySet(rowBuf, "YSDM", JZXYSDM);
                            TrySet(rowBuf, "JZXCD", length);
                            TrySet(rowBuf, "JZXLB", null);
                            TrySet(rowBuf, "JZXWZ", null);
                            TrySet(rowBuf, "JZXZ", null);
                            TrySet(rowBuf, "QSJXYSBH", null);
                            TrySet(rowBuf, "QSJXYS", null);
                            TrySet(rowBuf, "QSZYYSBH", null);
                            TrySet(rowBuf, "QSZYYS", null);
                            using var newRow = featureClass.CreateRow(rowBuf); newRow.Store();
                            last = ep;
                        }
                    }

                    processed++;
                    UpdateProgress(processed, total);
                }
                }
                finally
                {
                    featureClass?.Dispose();
                    gdb?.Dispose();
                    fsds?.Dispose();
                }
            }
            catch (Exception ex) { LogError($"生成界址线失败: {ex.Message}"); }
        }
    }
}
