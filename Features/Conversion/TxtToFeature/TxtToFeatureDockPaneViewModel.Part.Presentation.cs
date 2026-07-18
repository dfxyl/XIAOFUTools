using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Geometry;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.Conversion.TxtToFeature
{
    public partial class TxtToFeatureDockPaneViewModel
    {

        /// <summary>
        /// 选择输入文件夹
        /// </summary>
        private void SelectInputFolder()
        {
            var picked = PathDialogUtils.PickFolder("选择包含TXT文件的输入文件夹", InputFolder);
            if (!string.IsNullOrWhiteSpace(picked))
            {
                InputFolder = picked;
                LogMessage($"已选择输入文件夹: {InputFolder}");
            }
        }

        /// <summary>
        /// 选择输出文件夹
        /// </summary>
        private void SelectOutputFolder()
        {
            var picked = PathDialogUtils.PickFolder("选择输出文件夹", OutputFolder);
            if (!string.IsNullOrWhiteSpace(picked))
            {
                OutputFolder = picked;
                LogMessage($"已选择输出文件夹: {OutputFolder}");
            }
        }

        /// <summary>
        /// 选择坐标系
        /// </summary>
        private void SelectCoordinateSystem()
        {
            try
            {
                var selectedSpatialRef = CoordinateSystemSelector.ShowCoordinateSystemDialog();
                if (selectedSpatialRef != null)
                {
                    SelectedSpatialReference = selectedSpatialRef;
                    LogMessage($"已选择坐标系: {SelectedSpatialReference.Name}");
                }
            }
            catch (Exception ex)
            {
                LogError($"选择坐标系时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 开始转换
        /// </summary>
        private async Task StartConversionAsync()
        {
            if (!await ValidateInputsAsync())
                return;

            IsProcessing = true;
            IsProgressIndeterminate = true;
            Progress = 0;
            LogText = "";
            StatusText = "正在处理...";
            CancelRequested = false;

            try
            {
                LogMessage("开始转换过程...");
                LogMessage($"输入文件夹: {InputFolder}");
                LogMessage($"输出文件夹: {OutputFolder}");
                LogMessage($"坐标系: {SelectedCoordinateSystemName}");
                LogMessage($"字段配置: {FieldNames}");

                await ProcessTxtFilesAsync();

                // 清理可能的临时图层
                await QueuedTask.Run(() => CleanupTemporaryLayers());

                LogMessage("转换完成！");
                StatusText = "转换完成";
            }
            catch (Exception ex)
            {
                LogError($"转换过程中出错: {ex.Message}");
                LogError($"异常类型: {ex.GetType().Name}");
                LogError($"堆栈跟踪: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    LogError($"内部异常: {ex.InnerException.Message}");
                }
                StatusText = "转换失败";
            }
            finally
            {
                IsProcessing = false;
                IsProgressIndeterminate = false;
                Progress = 0;
            }
        }

        /// <summary>
        /// 停止转换
        /// </summary>
        private void StopConversion()
        {
            CancelRequested = true;
            LogMessage("正在停止转换...");
        }

        /// <summary>
        /// 显示帮助
        /// </summary>
        private void ShowHelp()
        {
            var helpText = @"TXT转SHP工具使用说明：

1. 选择包含TXT文件的输入文件夹
2. 选择输出文件夹
3. 选择目标坐标系
4. 配置字段名称（用逗号分隔）
5. 设置转换选项
6. 点击开始进行转换

支持的文本格式：
- 属性描述部分：包含坐标系、投影等信息
- 地块坐标部分：包含点号、环号、Y坐标、X坐标";

            PresentationServices.Dialogs.Show(helpText, "帮助");
        }

        /// <summary>
        /// 记录消息
        /// </summary>
        private void LogMessage(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogText += $"[{timestamp}] {message}\n";
            StatusText = message;
        }

        /// <summary>
        /// 记录错误
        /// </summary>
        private void LogError(string error)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogText += $"[{timestamp}] 错误: {error}\n";
            StatusText = $"错误: {error}";
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
