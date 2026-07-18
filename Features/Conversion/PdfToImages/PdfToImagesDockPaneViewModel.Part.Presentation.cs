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

namespace XIAOFUTools.Features.Conversion.PdfToImages
{
    internal partial class PdfToImagesDockPaneViewModel
    {

        /// <summary>
        /// 选择输入文件夹
        /// </summary>
        private void SelectInputFolder()
        {
            var picked = PathDialogUtils.PickFolder("选择包含PDF文件的输入文件夹", InputFolder);
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
            if (string.IsNullOrWhiteSpace(InputFolder) || !_conversionService.DirectoryExists(InputFolder))
            {
                PresentationServices.Dialogs.Show("请选择有效的输入文件夹", "错误");
                return;
            }

            if (!SaveToSourcePath && (string.IsNullOrWhiteSpace(OutputFolder) || !_conversionService.DirectoryExists(OutputFolder)))
            {
                PresentationServices.Dialogs.Show("请选择有效的输出文件夹或勾选保存在源路径", "错误");
                return;
            }

            if (Resolution <= 0)
            {
                PresentationServices.Dialogs.Show("请输入有效的分辨率值", "错误");
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
                await Task.Run(() => ProcessPdfFiles(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
                
                if (_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    StatusText = "转换已取消";
                    LogMessage("转换已被用户取消");
                }
                else
                {
                    StatusText = "转换完成";
                    LogMessage("所有PDF文件转换完成");
                    PresentationServices.Dialogs.Show("PDF转图片完成!", "完成");
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
            string helpMessage = @"PDF批量转图片工具

功能说明:
1. 批量将PDF文件转换为图片格式（JPG/PNG/WEBP/JPEG）
2. 支持多页PDF，每页转换为一张图片
3. 支持自定义输出尺寸和清晰度（通过DPI参数控制）
4. 多线程并行处理，大幅提升转换速度

使用步骤:
1. 选择包含PDF文件的输入文件夹
2. 选择输出文件夹（或勾选保存在源路径）
3. 配置转换选项:
   - 是否按原结构输出（仅当设置输出文件夹时可用）
   - 是否单独创建文件夹（默认勾选，每个PDF创建一个文件夹）
   - 是否遍历文件夹（处理子文件夹中的PDF）
   - 图片质量（DPI值，控制输出尺寸和清晰度，默认300 DPI）
   - 输出格式（JPG/PNG/WEBP/JPEG）
4. 点击开始按钮进行转换

技术说明:
- 本工具使用PDFium引擎（Google Chrome同款），无需安装Ghostscript
- 多线程并行处理，根据CPU核心数自动调整并发数，大幅提升处理速度
- DPI参数控制输出图片的尺寸和清晰度，数值越高图片越大越清晰
- 300 DPI是印刷标准分辨率，可保持PDF在CDR等设计软件中的原始物理尺寸
- 72 DPI为屏幕显示分辨率，输出尺寸较小
- 如需更高清晰度，可使用600 DPI或更高

注意事项:
- 多线程并行处理显著提升转换速度，特别适合批量处理
- 转换大量PDF时CPU使用率会较高，这是正常现象
- 确保有足够的磁盘空间存储输出图片
- 处理过程中可以点击停止按钮中止操作";

            PresentationServices.Dialogs.Show(helpMessage, "帮助");
        }

        /// <summary>
        /// 记录消息（简化版本）
        /// </summary>
        private void LogMessage(string message)
        {
            PresentationServices.UiThread.Post(() =>
            {
                LogText += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
            });
        }

        /// <summary>
        /// 记录错误（简化版本）
        /// </summary>
        private void LogError(string error)
        {
            PresentationServices.UiThread.Post(() =>
            {
                LogText += $"[{DateTime.Now:HH:mm:ss}] [错误] {error}\n";
            });
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
