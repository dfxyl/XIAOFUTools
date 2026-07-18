using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Core.CIM;
using XIAOFUTools.Shared;
using ArcGIS.Desktop.Framework.Dialogs;

namespace XIAOFUTools.Features.DataManagement.MapSheetsSmall
{
    internal partial class GenerateSmallMapSheetsDockPaneViewModel
    {

        private async Task RunAsync()
        {
            if (!CanRun) return;
            IsProcessing = true;
            AppendLog("开始生成图幅…");
            try
            {
                await QueuedTask.Run(async () =>
                {
                    // 根据模式获取范围
                    Envelope extent = null;
                    SpatialReference sr = null;
                    if (IsCustomMode)
                    {
                        if (!HasDrawnExtent)
                            throw new InvalidOperationException("请选择自定义范围（框选）后再运行。");
                        extent = _drawnExtent;
                        sr = _drawnExtent.SpatialReference;
                    }
                    else if (IsLayerMode)
                    {
                        if (SelectedPolygonLayer == null)
                            throw new InvalidOperationException("请选择一个面图层以使用图层范围。");
                        extent = SelectedPolygonLayer.QueryExtent();
                        sr = SelectedPolygonLayer.GetSpatialReference();
                    }
                    else // Map 视图范围
                    {
                        var mv = MapView.Active;
                        extent = mv?.Extent;
                        sr = extent?.SpatialReference;
                    }
                    if (extent == null || sr == null)
                        throw new InvalidOperationException("无法获取范围或空间参考。");

                    // 统一到 CGCS2000（EPSG:4490）下进行分幅与编号计算
                    var cgcs2000 = SpatialReferenceBuilder.CreateSpatialReference(4490);
                    var extentWgs = (Envelope)GeometryEngine.Instance.Project(extent, cgcs2000);

                    // 计算覆盖的百万分幅编码

                    // 创建输出要素类（GP创建 + 添加字段），然后打开以写入
                    using var outputFeatureClass = await CreateOutputFeatureClassAsync(OutputFeatureClassPath, sr);
                    var fc = outputFeatureClass.FeatureClass;

                    // 生成
                    var scaleCode = GetScaleCode(SelectedScaleName);
                    string[] mapCodes;

                    // 计算裁剪几何：
                    // 图层模式使用图层联合，其余使用矩形范围
                    Geometry clipGeom = IsLayerMode
                        ? BuildLayerUnionInCGCS2000(SelectedPolygonLayer, cgcs2000, extent, sr)
                        : PolygonBuilderEx.CreatePolygon(extentWgs);

                    var coverageExtentWgs = clipGeom?.Extent ?? extentWgs;
                    mapCodes = Compute100kCodes(coverageExtentWgs.XMin, coverageExtentWgs.XMax, coverageExtentWgs.YMin, coverageExtentWgs.YMax);

                    int created = 0;
                    foreach (var code in mapCodes)
                    {
                        if (scaleCode == null)
                        {
                            var extent100k = Get100kMapExtent(code);
                            // 百万（100k 基图幅）邻接
                            var neighbors100k = GetNeighborsFor100k(code);
                            if (InsertPolygon(extent100k, sr, fc, code,
                                neighbors100k.left, neighbors100k.right, neighbors100k.up, neighbors100k.low,
                                neighbors100k.upl, neighbors100k.upr, neighbors100k.lowl, neighbors100k.lowr,
                                clipGeom))
                                created++;
                        }
                        else
                        {
                            var (rows, cols) = GetRowColCount(scaleCode);
                            for (int r = 1; r <= rows; r++)
                            for (int c = 1; c <= cols; c++)
                            {
                                var e = GetExtentByScale(code, scaleCode, r, c);
                                // 仅插入与原范围相交的图幅
                                var eEnv = EnvelopeBuilderEx.CreateEnvelope(e.xmin, e.ymin, e.xmax, e.ymax, cgcs2000);
                                if (!eEnv.IsEmpty && eEnv.Intersects(coverageExtentWgs))
                                {
                                    var full = $"{code}{scaleCode}{r:000}{c:000}";
                                    var n = GetNeighborsForScale(code, scaleCode, r, c);
                                    if (InsertPolygon((e.xmin, e.xmax, e.ymin, e.ymax), sr, fc, full,
                                        n.left, n.right, n.up, n.low, n.upl, n.upr, n.lowl, n.lowr,
                                        clipGeom))
                                        created++;
                                }
                            }
                        }
                    }

                    AppendLog($"完成，生成图幅 {created} 个。");
                });
            }
            catch (Exception ex)
            {
                AppendLog($"错误: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
            }
        }
    }
}
