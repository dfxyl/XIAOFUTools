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

        private async Task RunAsync()
        {
            if (!HasSelectedLayer) return;
            IsProcessing = true;
            CancelRequested = false;
            Progress = 0;
            IsProgressIndeterminate = false;
            StatusMessage = "处理中...";
            AppendLog("开始旋转...");
            // 运行前输出当前配置摘要
            AppendLog($"配置：图层 = {SelectedLayer?.Name}");
            AppendLog($"配置：角度模式 = {(UseConstantAngle ? "统一角度" : "按字段角度")}; 方向 = {AngleDirection}");
            if (UseConstantAngle) AppendLog($"配置：统一角度(度) = {ConstantAngleDegrees}");
            else AppendLog($"配置：角度字段 = {SelectedAngleField}");
            AppendLog($"配置：线锚点 = {LineAnchor}; 面锚点 = {PolygonAnchor}");
            AppendLog($"配置：{SelectionInfo}");

            await QueuedTask.Run(async () =>
            {
                try
                {
                    var layer = SelectedLayer;
                    var fc = layer.GetFeatureClass();

                    // 准备游标
                    bool useSelection = layer.SelectionCount > 0;
                    RowCursor cursor = useSelection ? layer.GetSelection().Search(null, false) : fc.Search(null, false);

                    using (cursor)
                    {
                        var editOp = new ArcGIS.Desktop.Editing.EditOperation { Name = "旋转图形[线/面]" };
                        int processed = 0;
                        int total = 0;

                        // 先统计总数
                        total = useSelection ? layer.SelectionCount : (int)fc.GetCount();

                        while (cursor.MoveNext())
                        {
                            if (CancelRequested) break;
                            using (var row = cursor.Current)
                            {
                                var feature = row as Feature;
                                var shape = feature?.GetShape();
                                if (shape == null || shape.IsEmpty)
                                    continue;

                                double angleDeg = GetAngleDegreesForRow(row);
                                if (Math.Abs(angleDeg) < 1e-12)
                                {
                                    processed++;
                                    continue;
                                }
                                double angleRad = angleDeg * Math.PI / 180.0;

                                Geometry rotated = null;
                                if (shape is Polyline pl)
                                {
                                    var anchor = GetPolylineAnchorPoint(pl, LineAnchor);
                                    if (anchor != null)
                                        rotated = GeometryEngine.Instance.Rotate(pl, anchor, angleRad);
                                }
                                else if (shape is Polygon pg)
                                {
                                    var anchor = GetPolygonAnchorPoint(pg, PolygonAnchor);
                                    if (anchor != null)
                                        rotated = GeometryEngine.Instance.Rotate(pg, anchor, angleRad);
                                }

                                if (rotated != null)
                                {
                                    editOp.Modify(layer, row.GetObjectID(), rotated);
                                }

                                processed++;
                                int prog = total > 0 ? (int)(processed * 100.0 / total) : 0;
                                PresentationServices.UiThread.Post(() => { Progress = prog; StatusMessage = $"已处理 {processed}/{total}"; });
                            }
                        }

                        if (!CancelRequested)
                        {
                            var result = await editOp.ExecuteAsync();
                            PresentationServices.UiThread.Post(() =>
                            {
                                if (result)
                                {
                                    AppendLog("旋转完成");
                                    StatusMessage = "完成";
                                    Progress = 100;
                                    UpdateSelectionInfo();
                                }
                                else
                                {
                                    AppendLog("旋转失败：编辑操作未执行");
                                    StatusMessage = "失败";
                                }
                            });
                        }
                        else
                        {
                            PresentationServices.UiThread.Post(() => { AppendLog("已取消"); StatusMessage = "已取消"; });
                        }
                    }
                }
                catch (Exception ex)
                {
                    PresentationServices.UiThread.Post(() => { AppendLog("错误: " + ex.Message); StatusMessage = "错误"; });
                }
                finally
                {
                    PresentationServices.UiThread.Post(() => { IsProcessing = false; Progress = 0; });
                }
            });
        }
    }
}
