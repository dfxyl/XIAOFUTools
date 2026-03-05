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
using XIAOFUTools.Common;
using PdfSharp.Pdf;
using PdfSharp.Drawing;
using System.Drawing;

namespace XIAOFUTools.Tools.ImagesToPdf
{
    /// <summary>
    /// 图片批量转PDF停靠窗格视图模型
    /// </summary>
    internal class ImagesToPdfDockPaneViewModel : INotifyPropertyChanged
    {
        #region 属性

        private string _inputFolder;
        /// <summary>
        /// 输入文件夹
        /// </summary>
        public string InputFolder
        {
            get => _inputFolder;
            set
            {
                if (_inputFolder != value)
                {
                    _inputFolder = value;
                    OnPropertyChanged(nameof(InputFolder));
                }
            }
        }

        private string _outputFolder;
        /// <summary>
        /// 输出文件夹
        /// </summary>
        public string OutputFolder
        {
            get => _outputFolder;
            set
            {
                if (_outputFolder != value)
                {
                    _outputFolder = value;
                    OnPropertyChanged(nameof(OutputFolder));
                    OnPropertyChanged(nameof(ShowKeepOriginalStructure));
                    OnPropertyChanged(nameof(ShowOutputFolder));
                }
            }
        }

        private bool _saveToSourcePath = true;
        /// <summary>
        /// 是否保存在源路径
        /// </summary>
        public bool SaveToSourcePath
        {
            get => _saveToSourcePath;
            set
            {
                if (_saveToSourcePath != value)
                {
                    _saveToSourcePath = value;
                    OnPropertyChanged(nameof(SaveToSourcePath));
                    OnPropertyChanged(nameof(ShowKeepOriginalStructure));
                    OnPropertyChanged(nameof(ShowOutputFolder));
                }
            }
        }

        private bool _keepOriginalStructure;
        /// <summary>
        /// 是否按原结构输出
        /// </summary>
        public bool KeepOriginalStructure
        {
            get => _keepOriginalStructure;
            set
            {
                if (_keepOriginalStructure != value)
                {
                    _keepOriginalStructure = value;
                    OnPropertyChanged(nameof(KeepOriginalStructure));
                }
            }
        }

        private bool _mergeImages = true;
        /// <summary>
        /// 是否合并图片（一个子文件夹的图片合并为一个PDF）
        /// </summary>
        public bool MergeImages
        {
            get => _mergeImages;
            set
            {
                if (_mergeImages != value)
                {
                    _mergeImages = value;
                    OnPropertyChanged(nameof(MergeImages));
                }
            }
        }

        private bool _traverseSubfolders = true;
        /// <summary>
        /// 是否遍历子文件夹
        /// </summary>
        public bool TraverseSubfolders
        {
            get => _traverseSubfolders;
            set
            {
                if (_traverseSubfolders != value)
                {
                    _traverseSubfolders = value;
                    OnPropertyChanged(nameof(TraverseSubfolders));
                }
            }
        }

        private int _dpi = 300;
        /// <summary>
        /// DPI设置（控制PDF页面物理尺寸）
        /// </summary>
        public int Dpi
        {
            get => _dpi;
            set
            {
                if (_dpi != value)
                {
                    _dpi = value;
                    OnPropertyChanged(nameof(Dpi));
                }
            }
        }

        /// <summary>
        /// 是否显示按原结构输出选项（有输出文件夹且未勾选保存在源路径时显示）
        /// </summary>
        public bool ShowKeepOriginalStructure => !SaveToSourcePath && !string.IsNullOrWhiteSpace(OutputFolder);

        /// <summary>
        /// 是否显示输出文件夹选择（未勾选保存在源路径时显示）
        /// </summary>
        public bool ShowOutputFolder => !SaveToSourcePath;

        private double _progress;
        /// <summary>
        /// 进度
        /// </summary>
        public double Progress
        {
            get => _progress;
            set
            {
                if (Math.Abs(_progress - value) > 0.01)
                {
                    _progress = value;
                    OnPropertyChanged(nameof(Progress));
                }
            }
        }

        private bool _isProgressIndeterminate;
        /// <summary>
        /// 是否不确定进度
        /// </summary>
        public bool IsProgressIndeterminate
        {
            get => _isProgressIndeterminate;
            set
            {
                if (_isProgressIndeterminate != value)
                {
                    _isProgressIndeterminate = value;
                    OnPropertyChanged(nameof(IsProgressIndeterminate));
                }
            }
        }

        private string _logText;
        /// <summary>
        /// 日志文本
        /// </summary>
        public string LogText
        {
            get => _logText;
            set
            {
                if (_logText != value)
                {
                    _logText = value;
                    OnPropertyChanged(nameof(LogText));
                }
            }
        }

        private string _statusText;
        /// <summary>
        /// 状态文本
        /// </summary>
        public string StatusText
        {
            get => _statusText;
            set
            {
                if (_statusText != value)
                {
                    _statusText = value;
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        private bool _isProcessing;
        /// <summary>
        /// 是否正在处理
        /// </summary>
        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                if (_isProcessing != value)
                {
                    _isProcessing = value;
                    OnPropertyChanged(nameof(IsProcessing));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private CancellationTokenSource _cancellationTokenSource;

        // 支持的图片格式
        private static readonly string[] SupportedImageExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff" };

        #endregion

        #region 命令

        /// <summary>
        /// 选择输入文件夹命令
        /// </summary>
        public ICommand SelectInputFolderCommand { get; }

        /// <summary>
        /// 选择输出文件夹命令
        /// </summary>
        public ICommand SelectOutputFolderCommand { get; }

        /// <summary>
        /// 开始转换命令
        /// </summary>
        public ICommand StartConversionCommand { get; }

        /// <summary>
        /// 停止转换命令
        /// </summary>
        public ICommand StopConversionCommand { get; }

        /// <summary>
        /// 帮助命令
        /// </summary>
        public ICommand HelpCommand { get; }

        #endregion

        #region 构造函数

        public ImagesToPdfDockPaneViewModel()
        {
            SelectInputFolderCommand = new RelayCommand(SelectInputFolder);
            SelectOutputFolderCommand = new RelayCommand(SelectOutputFolder);
            StartConversionCommand = new RelayCommand(async () => await StartConversionAsync(), () => !IsProcessing);
            StopConversionCommand = new RelayCommand(StopConversion, () => IsProcessing);
            HelpCommand = new RelayCommand(ShowHelp);

            LogMessage("图片批量转PDF工具已加载");
        }

        #endregion

        #region 方法

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
            if (string.IsNullOrWhiteSpace(InputFolder) || !Directory.Exists(InputFolder))
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("请选择有效的输入文件夹", "错误");
                return;
            }

            if (!SaveToSourcePath && (string.IsNullOrWhiteSpace(OutputFolder) || !Directory.Exists(OutputFolder)))
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("请选择有效的输出文件夹或勾选保存在源路径", "错误");
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
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("图片转PDF完成!", "完成");
                }
            }
            catch (Exception ex)
            {
                StatusText = "转换失败";
                LogError($"转换失败: {ex.Message}");
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"转换失败: {ex.Message}", "错误");
            }
            finally
            {
                IsProcessing = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        /// <summary>
        /// 处理图片文件
        /// </summary>
        private void ProcessImages(CancellationToken token)
        {
            if (MergeImages)
            {
                // 合并模式：按文件夹分组处理
                ProcessImagesInMergeMode(token);
            }
            else
            {
                // 非合并模式：每张图片单独生成PDF
                ProcessImagesInSeparateMode(token);
            }
        }

        /// <summary>
        /// 合并模式处理图片
        /// </summary>
        private void ProcessImagesInMergeMode(CancellationToken token)
        {
            // 获取所有文件夹
            List<string> folders = new List<string>();
            
            if (TraverseSubfolders)
            {
                // 获取所有子文件夹
                folders.AddRange(Directory.GetDirectories(InputFolder, "*", SearchOption.AllDirectories));
            }
            
            // 总是包含根文件夹
            folders.Insert(0, InputFolder);

            LogMessage($"找到 {folders.Count} 个文件夹，开始处理...");

            int totalFolders = folders.Count;
            int processedFolders = 0;

            foreach (var folder in folders)
            {
                if (token.IsCancellationRequested)
                    break;

                // 获取当前文件夹中的所有图片（不包括子文件夹）
                var imageFiles = GetImageFilesInFolder(folder, SearchOption.TopDirectoryOnly);

                if (imageFiles.Count == 0)
                {
                    processedFolders++;
                    Progress = (double)processedFolders / totalFolders * 100;
                    continue;
                }

                try
                {
                    string folderName = folder == InputFolder ? Path.GetFileName(InputFolder) : Path.GetFileName(folder);
                    LogMessage($"正在处理文件夹: {folderName} ({imageFiles.Count} 张图片)");

                    // 确定输出路径，使用文件夹名称作为PDF文件名
                    string pdfFileName = folderName ?? "output";
                    string outputPath = GetOutputPdfPath(folder, pdfFileName);
                    
                    // 创建PDF并添加所有图片
                    CreatePdfFromImages(imageFiles, outputPath, token);
                    
                    LogMessage($"完成处理: {folderName} -> {Path.GetFileName(outputPath)}");
                }
                catch (Exception ex)
                {
                    LogError($"处理文件夹 {Path.GetFileName(folder)} 时出错: {ex.Message}");
                }

                processedFolders++;
                Progress = (double)processedFolders / totalFolders * 100;
                StatusText = $"已处理 {processedFolders}/{totalFolders} 个文件夹";
            }
        }

        /// <summary>
        /// 非合并模式处理图片
        /// </summary>
        private void ProcessImagesInSeparateMode(CancellationToken token)
        {
            // 获取所有图片文件
            SearchOption searchOption = TraverseSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var imageFiles = GetImageFilesInFolder(InputFolder, searchOption);

            if (imageFiles.Count == 0)
            {
                LogMessage("未找到图片文件");
                return;
            }

            LogMessage($"找到 {imageFiles.Count} 个图片文件，开始处理...");

            int totalFiles = imageFiles.Count;
            int processedFiles = 0;

            foreach (var imageFile in imageFiles)
            {
                if (token.IsCancellationRequested)
                    break;

                try
                {
                    string imageName = Path.GetFileName(imageFile);
                    string pdfName = Path.GetFileNameWithoutExtension(imageFile);
                    LogMessage($"正在处理: {imageName} -> {pdfName}.pdf");

                    // 确定输出路径（使用图片名称作为PDF文件名）
                    string outputPath = GetOutputPdfPath(Path.GetDirectoryName(imageFile), pdfName);
                    
                    // 创建PDF（单张图片）
                    CreatePdfFromImages(new List<string> { imageFile }, outputPath, token);
                    
                    LogMessage($"  - 已生成: {Path.GetFileName(outputPath)}");
                }
                catch (Exception ex)
                {
                    LogError($"处理文件 {Path.GetFileName(imageFile)} 时出错: {ex.Message}");
                }

                processedFiles++;
                Progress = (double)processedFiles / totalFiles * 100;
                StatusText = $"已处理 {processedFiles}/{totalFiles} 个文件";
            }
        }

        /// <summary>
        /// 获取文件夹中的所有图片文件
        /// </summary>
        private List<string> GetImageFilesInFolder(string folder, SearchOption searchOption)
        {
            var imageFiles = new List<string>();

            foreach (var ext in SupportedImageExtensions)
            {
                try
                {
                    imageFiles.AddRange(Directory.GetFiles(folder, $"*{ext}", searchOption));
                }
                catch
                {
                    // 忽略访问拒绝等异常
                }
            }

            // 按文件名排序
            imageFiles.Sort();

            return imageFiles;
        }

        /// <summary>
        /// 从图片创建PDF
        /// </summary>
        private void CreatePdfFromImages(List<string> imageFiles, string outputPath, CancellationToken token)
        {
            if (imageFiles == null || imageFiles.Count == 0)
                return;

            // 确保输出目录存在
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            PdfDocument document = null;

            try
            {
                document = new PdfDocument();
                document.Info.Title = Path.GetFileNameWithoutExtension(outputPath);
                document.Info.Creator = "XIAOFUTools";

                for (int i = 0; i < imageFiles.Count; i++)
                {
                    if (token.IsCancellationRequested)
                        break;

                    try
                    {
                        string imageFile = imageFiles[i];
                        
                        // 读取图片获取尺寸
                        using (var img = System.Drawing.Image.FromFile(imageFile))
                        {
                            // 获取图片像素尺寸
                            float pixelWidth = img.Width;
                            float pixelHeight = img.Height;
                            
                            // 使用用户设置的DPI计算PDF页面的物理尺寸（点为单位，72点=1英寸）
                            // 公式: 页面尺寸(点) = 像素尺寸 / 设置DPI * 72
                            double pageWidth = pixelWidth / Dpi * 72.0;
                            double pageHeight = pixelHeight / Dpi * 72.0;
                            
                            // 创建新页面
                            PdfPage page = document.AddPage();
                            page.Width = XUnit.FromPoint(pageWidth);
                            page.Height = XUnit.FromPoint(pageHeight);
                            
                            // 在页面上绘制图片
                            using (XGraphics gfx = XGraphics.FromPdfPage(page))
                            using (XImage xImage = XImage.FromFile(imageFile))
                            {
                                // 图片填充整个页面
                                gfx.DrawImage(xImage, 0, 0, pageWidth, pageHeight);
                            }
                            
                            // 记录图片信息
                            LogMessage($"  第{i + 1}页: {Path.GetFileName(imageFile)} - 像素: {pixelWidth:F0}x{pixelHeight:F0}, 页面: {pageWidth:F2}x{pageHeight:F2}点 ({pageWidth/72.0:F2}x{pageHeight/72.0:F2}英寸)");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"添加图片 {Path.GetFileName(imageFiles[i])} 到PDF时出错: {ex.Message}");
                    }
                }

                // 保存PDF文档
                if (document.PageCount > 0)
                {
                    document.Save(outputPath);
                }
            }
            finally
            {
                document?.Dispose();
            }
        }

        /// <summary>
        /// 获取输出PDF路径
        /// </summary>
        private string GetOutputPdfPath(string sourceFolderPath, string pdfName)
        {
            string outputDir;

            if (SaveToSourcePath)
            {
                outputDir = sourceFolderPath;
            }
            else
            {
                if (KeepOriginalStructure)
                {
                    // 保持原始目录结构
                    string relativePath = sourceFolderPath.Substring(InputFolder.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    outputDir = Path.Combine(OutputFolder, relativePath);
                }
                else
                {
                    outputDir = OutputFolder;
                }
            }

            // 确保输出目录存在
            Directory.CreateDirectory(outputDir);

            // 生成唯一的文件名
            string outputPath = Path.Combine(outputDir, $"{pdfName}.pdf");
            int counter = 1;
            while (File.Exists(outputPath))
            {
                outputPath = Path.Combine(outputDir, $"{pdfName}_{counter}.pdf");
                counter++;
            }

            return outputPath;
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

            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(helpMessage, "帮助");
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

        #endregion

        #region RelayCommand 实现

        /// <summary>
        /// 简单的命令实现
        /// </summary>
        private class RelayCommand : ICommand
        {
            private readonly Action _execute;
            private readonly Func<bool> _canExecute;

            public RelayCommand(Action execute, Func<bool> canExecute = null)
            {
                _execute = execute ?? throw new ArgumentNullException(nameof(execute));
                _canExecute = canExecute;
            }

            public event EventHandler CanExecuteChanged
            {
                add { CommandManager.RequerySuggested += value; }
                remove { CommandManager.RequerySuggested -= value; }
            }

            public bool CanExecute(object parameter)
            {
                return _canExecute?.Invoke() ?? true;
            }

            public void Execute(object parameter)
            {
                _execute();
            }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
