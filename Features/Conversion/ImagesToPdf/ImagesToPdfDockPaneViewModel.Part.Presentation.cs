using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.Conversion.ImagesToPdf
{
    internal partial class ImagesToPdfDockPaneViewModel
    {

        /// <summary>
        /// 选择输入文件夹
        /// </summary>
        private void SelectInputFolder()
        {
            var picked = PathDialogUtils.PickFolder("选择包含图片文件的输入文件夹", InputFolder);
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
        /// 开始转换
        /// </summary>
        private async Task StartConversionAsync()
        {
            // 验证输入
            if (string.IsNullOrWhiteSpace(InputFolder) || !_fileStore.DirectoryExists(InputFolder))
            {
                PresentationServices.Dialogs.Show("请选择有效的输入文件夹", "错误");
                return;
            }

            if (!SaveToSourcePath && (string.IsNullOrWhiteSpace(OutputFolder) || !_fileStore.DirectoryExists(OutputFolder)) )
            {
                PresentationServices.Dialogs.Show("请选择有效的输出文件夹或勾选保存在源路径", "错误");
                return;
            }

            IsProcessing = true;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            Progress = 0;
            IsProgressIndeterminate = false;
            LogText = "";
            StatusText = "开始转换...";

            try
            {
                await QueuedTask.Run(() => ProcessImages(_cancellationTokenSource.Token));
                
                if (_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    StatusText = "转换已取消";
                    LogMessage("转换已被用户取消");
                }
                else
                {
                    StatusText = "转换完成";
                    LogMessage("所有图片转PDF完成");
                    PresentationServices.Dialogs.Show("图片转PDF完成!", "完成");
                }
            }
            catch (Exception ex)
            {
                StatusText = "转换失败";
                LogError($"转换失败: {ex.Message}");
                PresentationServices.Dialogs.Show($"转换失败: {ex.Message}", "错误");
            }
            finally
            {
                IsProcessing = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        /// <summary>
        /// 停止转换
        /// </summary>
        private void StopConversion()
        {
            _cancellationTokenSource?.Cancel();
            LogMessage("正在停止转换...");
        }

        /// <summary>
        /// 显示帮助
        /// </summary>
        private void ShowHelp()
        {
            string helpMessage = "图片批量转PDF工具\n\n" +
                "功能说明:\n" +
                "1. 批量将图片文件转换为PDF格式\n" +
                "2. 自动识别图片大小，PDF页面与图片尺寸完全一致\n" +
                "3. 支持合并模式和单独模式\n" +
                "4. 支持多种图片格式（JPG/PNG/BMP/GIF/TIF等）\n\n" +
                "使用步骤:\n" +
                "1. 选择包含图片文件的输入文件夹\n" +
                "2. 选择输出文件夹（或勾选保存在源路径）\n" +
                "3. 配置转换选项:\n" +
                "   - 是否按原结构输出（仅当设置输出文件夹时可用）\n" +
                "   - 是否合并（默认勾选，将每个子文件夹的图片合并为一个PDF）\n" +
                "   - 是否遍历子文件夹（默认勾选，处理所有子文件夹）\n" +
                "4. 点击开始按钮进行转换\n\n" +
                "转换模式说明:\n" +
                "【合并模式】（推荐）\n" +
                "- 将每个文件夹中的所有图片合并为一个PDF文件\n" +
                "- 适合批量处理多个项目，每个项目一个文件夹\n" +
                "- PDF文件名自动使用所在文件夹的名称\n\n" +
                "【单独模式】\n" +
                "- 每张图片生成一个独立的PDF文件\n" +
                "- 适合需要单独管理每张图片的场景\n" +
                "- PDF文件名与原图片名相同\n\n" +
                "DPI设置说明:\n" +
                "- 300 DPI（推荐）: 印刷标准分辨率，PDF页面尺寸合理\n" +
                "  例如: 1920x1080像素图片 → 6.4×3.6英寸的PDF页面\n" +
                "- 72 DPI: 屏幕显示分辨率，PDF页面尺寸较大\n" +
                "  例如: 1920x1080像素图片 → 26.7×15英寸的PDF页面\n" +
                "- 自定义DPI: 可根据需要设置任意值控制PDF尺寸\n\n" +
                "技术特点:\n" +
                "- 根据设置的DPI统一计算PDF页面物理尺寸\n" +
                "- 支持常见图片格式: JPG, PNG, BMP, GIF, TIF等\n" +
                "- 按文件名自动排序，确保PDF页面顺序正确\n" +
                "- 自动处理重名文件，避免覆盖\n" +
                "- 与PDF转图片工具配合使用，尺寸保持一致\n\n" +
                "注意事项:\n" +
                "- 确保有足够的磁盘空间存储输出PDF\n" +
                "- 大量图片转换可能需要较长时间\n" +
                "- 处理过程中可以点击停止按钮中止操作";

            PresentationServices.Dialogs.Show(helpMessage, "帮助");
        }

        /// <summary>
        /// 记录消息
        /// </summary>
        private void LogMessage(string message)
        {
            try
            {
                LogText += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
            }
            catch
            {
                // 忽略UI更新异常
            }
        }

        /// <summary>
        /// 记录错误
        /// </summary>
        private void LogError(string error)
        {
            try
            {
                LogText += $"[{DateTime.Now:HH:mm:ss}] [错误] {error}\n";
            }
            catch
            {
                // 忽略UI更新异常
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
