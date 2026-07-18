using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Framework.Events;
using Microsoft.Win32;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.Conversion.FeatureToTxt
{
    internal partial class FeatureToTxtDockPaneViewModel
    {

        /// <summary>
        /// 刷新图层列表
        /// </summary>
        public void RefreshLayers()
        {
            StatusMessage = "正在刷新图层列表...";
            LogInfo("开始刷新图层列表");
            LogInfo($"当前PolygonLayers集合状态: {(PolygonLayers == null ? "null" : $"已初始化，包含{PolygonLayers.Count}个项目")}");

            LoadPolygonLayers();
        }

        /// <summary>
        /// 上移/下移当前选中输出字段
        /// </summary>
        private void MoveSelectedOutputField(int delta)
        {
            if (OutputFields == null || SelectedOutputField == null) return;
            var index = OutputFields.IndexOf(SelectedOutputField);
            if (index < 0) return;
            var newIndex = index + delta;
            if (newIndex < 0 || newIndex >= OutputFields.Count) return;
            OutputFields.Move(index, newIndex);
            StatusMessage = $"已移动字段 '{SelectedOutputField.FieldName}' 到位置 {newIndex + 1}";
        }

        /// <summary>
        /// 浏览输出路径
        /// </summary>
        private void BrowseOutputPath()
        {
            var picked = PathDialogUtils.PickFolder("选择输出文件夹", OutputPath);
            if (!string.IsNullOrWhiteSpace(picked))
            {
                OutputPath = picked;
            }
        }

        /// <summary>
        /// 取消操作
        /// </summary>
        private void Cancel()
        {
            CancelRequested = true;
            _executionCancellation?.Cancel();
            StatusMessage = "正在取消操作...";
        }

        /// <summary>
        /// 打开头部信息配置对话框
        /// </summary>
        private void OpenHeaderConfigDialog()
        {
            try
            {
                if (_dialogService.ShowHeaderConfiguration())
                {
                    LoadHeaderConfigs();
                    StatusMessage = "头部配置已保存";
                    LogInfo("头部配置对话框已关闭，配置已保存");
                }
                else
                {
                    LogInfo("头部配置对话框已取消");
                }
            }
            catch (Exception ex)
            {
                LogError($"打开头部配置对话框失败: {ex.Message}");
                StatusMessage = $"打开头部配置对话框失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 打开字段配置对话框
        /// </summary>
        private void OpenFieldConfigDialog()
        {
            try
            {
                if (_dialogService.ShowFieldConfiguration(this))
                {
                    NotifyPropertyChanged(() => OutputFieldsCount);
                    StatusMessage = $"字段配置已更新，共 {OutputFieldsCount} 个字段";
                    LogInfo("字段配置对话框已关闭，配置已保存");
                }
                else
                {
                    LogInfo("字段配置对话框已取消");
                }
            }
            catch (Exception ex)
            {
                LogError($"打开字段配置对话框失败: {ex.Message}");
                StatusMessage = $"打开字段配置对话框失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 显示帮助信息
        /// </summary>
        private void ShowHelp()
        {
            var helpContent = @"要素类转TXT工具帮助

功能描述：
将面要素图层转换为TXT格式的坐标文件，支持合并导出和分块导出两种模式。

参数说明：
• 要素图层(面)：选择需要转换的面要素图层
• 输出文件夹：指定生成的TXT文件保存文件夹
• 输出字段配置：自定义输出字段的顺序和内容
• 选项设置：配置输出格式和坐标处理选项

文件命名规则：
• 合并导出（未勾选分块导出）：使用图层名称作为文件名
  例如：图层名为""地块数据"" → 生成""地块数据.txt""
• 分块导出（勾选分块导出）：图层名称 + 序号
  例如：图层名为""地块数据"" → 生成""地块数据_000001.txt""、""地块数据_000002.txt""等

导出模式：
• 合并导出：所有要素的坐标写入一个TXT文件
• 分块导出：每个要素生成一个单独的TXT文件

字段配置操作：
• 拖拽：可以拖拽字段块调整输出顺序
• 右键：右键点击可添加图层中的实际字段
• 删除：点击字段块右上角的×按钮删除字段
• 特殊字段：点数、图形类型、逗号、@等

选项设置：
• 是否输出闭合点：控制是否输出多边形的闭合点
• 内环1起编：内环坐标点从1开始编号
• 是否闭合点续编：闭合点是否继续编号
• 分块导出：控制是否每个要素单独导出为一个文件
• XY互换：交换X和Y坐标的输出顺序
• 前缀：坐标点编号的前缀文本
• 小数位数：坐标值的小数位数

操作步骤：
1. 选择要转换的面要素图层
2. 选择输出文件夹
3. 配置输出字段和选项
4. 选择导出模式（是否勾选分块导出）
5. 点击""生成TXT""开始转换

注意事项：
• 确保选择的图层包含面要素
• 输出文件夹必须存在且有写入权限
• 文件名会自动移除非法字符
• 大量要素转换可能需要较长时间
• 转换过程中可以点击""停止""按钮取消操作";

            PresentationServices.Dialogs.Show(helpContent, "要素类转TXT工具帮助");
        }

        /// <summary>
        /// 地图选择变化事件处理：仅刷新当前视图的选择信息
        /// </summary>
        private void OnMapSelectionChanged(MapSelectionChangedEventArgs args)
        {
            UpdateSelectionInfo();
        }

        /// <summary>
        /// 刷新所选图层的选择信息（HasSelectionInLayer、SelectedCount、SelectionInfoText）
        /// </summary>
        private void UpdateSelectionInfo()
        {
            Task.Run(async () =>
            {
                var info = await SelectionUtils.GetSelectionInfoAsync(SelectedPolygonLayer);
                PresentationServices.UiThread.Invoke(() =>
                {
                    HasSelectionInLayer = info.HasSelection;
                    SelectedCount = info.Count;
                    SelectionInfoText = info.InfoText ?? (info.HasSelection ? $"已选 {info.Count}" : "全部要素");
                });
            });
        }

        /// <summary>
        /// 记录信息日志（优化版本，减少UI更新频率）
        /// </summary>
        private void LogInfo(string message, bool forceUpdate = false)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logMessage = $"[{timestamp}] {message}";

            // 对于大量数据处理，减少UI更新频率（每20条更新一次，除非强制）
            if (forceUpdate || _logUpdateCounter % 20 == 0)
            {
                PresentationServices.UiThread.PostBackground(() =>
                {
                    LogContent += logMessage + Environment.NewLine;
                });
            }

            _logUpdateCounter++;
        }

        /// <summary>
        /// 记录警告日志
        /// </summary>
        private void LogWarning(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logMessage = $"[{timestamp}] 警告: {message}";

            PresentationServices.UiThread.PostBackground(() =>
            {
                LogContent += logMessage + Environment.NewLine;
            });
        }

        /// <summary>
        /// 记录错误日志
        /// </summary>
        private void LogError(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logMessage = $"[{timestamp}] 错误: {message}";

            PresentationServices.UiThread.Post(() =>
            {
                LogContent += logMessage + Environment.NewLine;
            });
        }
    }
}
