using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Editing;
using XIAOFUTools.Shared;
using XIAOFUTools.Shared.Presentation;
using SharedFieldSelectionDialog = XIAOFUTools.Shared.Presentation.Dialogs.FieldSelectionDialog;

namespace XIAOFUTools.Features.Editing.NodeDistanceCheck
{
    internal partial class NodeDistanceCheckDockPaneViewModel
    {

        /// <summary>
        /// 刷新图层列表
        /// </summary>
        public void RefreshLayers()
        {
            LoadPolygonLayers();
        }

        /// <summary>
        /// 更新输出路径
        /// </summary>
        private void UpdateOutputPath()
        {
            string projectGDB = GetProjectGDBPath();
            string outputName;

            if (SelectedPolygonLayer != null)
            {
                outputName = $"{SelectedPolygonLayer.Name}_节点距离检查";
            }
            else
            {
                outputName = "节点距离检查结果";
            }

            if (!string.IsNullOrEmpty(projectGDB))
            {
                OutputPath = Path.Combine(projectGDB, outputName);
            }
            else
            {
                OutputPath = outputName;
            }
        }



        /// <summary>
        /// 选择保留字段
        /// </summary>
        private void SelectFields()
        {
            if (SelectedPolygonLayer == null) return;

            try
            {
                QueuedTask.Run(() =>
                {
                    try
                    {
                        using (var table = SelectedPolygonLayer.GetTable())
                        {
                            if (table != null)
                            {
                                var definition = table.GetDefinition();
                                var fields = definition.GetFields().ToList();

                                PresentationServices.UiThread.InvokeOrRun(() =>
                                {
                                    var selectedFields = SharedFieldSelectionDialog.Select(fields, SelectedFields);
                                    if (selectedFields is not null)
                                    {
                                        SelectedFields = selectedFields.ToList();
                                        LogInfo($"已选择 {SelectedFields.Count} 个保留字段");
                                    }
                                });
                            }
                            else
                            {
                                LogError("无法获取图层表格");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"获取字段列表失败: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                LogError($"选择字段时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示帮助信息
        /// </summary>
        private void ShowHelp()
        {
            string helpContent = "节点距离检查工具使用说明\n\n" +
                "功能描述：\n" +
                "检查面要素图层中相邻节点之间的距离，输出符合指定条件的线要素。\n\n" +
                "参数说明：\n" +
                "• 面要素图层：选择要检查的面要素图层\n" +
                "• 节点检查选项：选择距离比较条件（小于等于、小于、大于等于、大于、等于）\n" +
                "• 节点检查距离：设置距离阈值（单位：米）\n" +
                "• 输出线要素图层：指定输出结果的保存位置\n\n" +
                "操作步骤：\n" +
                "1. 选择要检查的面要素图层\n" +
                "2. 选择节点检查选项（如\"小于等于\"）\n" +
                "3. 设置节点检查距离（如1.0米）\n" +
                "4. 指定输出线要素图层路径\n" +
                "5. 点击\"开始\"按钮执行检查\n\n" +
                "输出结果：\n" +
                "• 源要素ID：原始面要素的ID\n" +
                "• 节点距离：相邻节点之间的实际距离\n" +
                "• 检查条件：使用的检查条件和阈值\n\n" +
                "注意事项：\n" +
                "• 只检查相邻节点之间的距离\n" +
                "• 输出的线要素连接符合条件的相邻节点对\n" +
                "• 距离单位为米\n" +
                "• 等于条件允许0.001米的浮点误差";

            PresentationServices.Dialogs.Show(helpContent, "节点距离检查工具帮助", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }

        /// <summary>
        /// 记录信息日志
        /// </summary>
        private void LogInfo(string message)
        {
            var logMessage = $"[{DateTime.Now:HH:mm:ss}] {message}";
            _logBuilder.AppendLine(logMessage);
            
            PresentationServices.UiThread.InvokeOrRun(() =>
            {
                LogContent = _logBuilder.ToString();
            });
        }

        /// <summary>
        /// 记录警告日志
        /// </summary>
        private void LogWarning(string message)
        {
            var logMessage = $"[{DateTime.Now:HH:mm:ss}] 警告: {message}";
            _logBuilder.AppendLine(logMessage);
            
            PresentationServices.UiThread.InvokeOrRun(() =>
            {
                LogContent = _logBuilder.ToString();
            });
        }

        /// <summary>
        /// 记录错误日志
        /// </summary>
        private void LogError(string message)
        {
            var logMessage = $"[{DateTime.Now:HH:mm:ss}] 错误: {message}";
            _logBuilder.AppendLine(logMessage);
            
            PresentationServices.UiThread.InvokeOrRun(() =>
            {
                LogContent = _logBuilder.ToString();
            });
        }

        /// <summary>
        /// 清除日志
        /// </summary>
        private void ClearLog()
        {
            _logBuilder.Clear();
            LogContent = "";
        }
    }
}
