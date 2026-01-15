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
using PDFtoImage;
using SkiaSharp;

namespace XIAOFUTools.Tools.PdfToImages
{
    /// <summary>
    /// PDF批量转图片停靠窗格视图模型
    /// </summary>
    internal class PdfToImagesDockPaneViewModel : DockPane, INotifyPropertyChanged
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

        private bool _createSeparateFolder = true;
        /// <summary>
        /// 是否单独创建文件夹（每一个PDF一个文件夹）
        /// </summary>
        public bool CreateSeparateFolder
        {
            get => _createSeparateFolder;
            set
            {
                if (_createSeparateFolder != value)
                {
                    _createSeparateFolder = value;
                    OnPropertyChanged(nameof(CreateSeparateFolder));
                }
            }
        }

        private bool _traverseSubfolders;
        /// <summary>
        /// 是否遍历文件夹
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

        private int _resolution = 300;
        /// <summary>
        /// 分辨率
        /// </summary>
        public int Resolution
        {
            get => _resolution;
            set
            {
                if (_resolution != value)
                {
                    _resolution = value;
                    OnPropertyChanged(nameof(Resolution));
                }
            }
        }

        private string _outputFormat = "JPG";
        /// <summary>
        /// 输出格式
        /// </summary>
        public string OutputFormat
        {
            get => _outputFormat;
            set
            {
                if (_outputFormat != value)
                {
                    _outputFormat = value;
                    OnPropertyChanged(nameof(OutputFormat));
                }
            }
        }

        /// <summary>
        /// 输出格式选项
        /// </summary>
        public List<string> OutputFormats { get; } = new List<string> { "JPG", "PNG", "WEBP", "JPEG" };

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

        public PdfToImagesDockPaneViewModel()
        {
            SelectInputFolderCommand = new RelayCommand(SelectInputFolder);
            SelectOutputFolderCommand = new RelayCommand(SelectOutputFolder);
            StartConversionCommand = new RelayCommand(async () => await StartConversionAsync(), () => !IsProcessing);
            StopConversionCommand = new RelayCommand(StopConversion, () => IsProcessing);
            HelpCommand = new RelayCommand(ShowHelp);

            LogMessage("PDF批量转图片工具已加载");
        }

        #endregion

        #region 方法

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

            if (Resolution <= 0)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("请输入有效的分辨率值", "错误");
                return;
            }

            IsProcessing = true;
            _cancellationTokenSource = new CancellationTokenSource();
            Progress = 0;
            IsProgressIndeterminate = false;
            LogText = "";
            StatusText = "开始转换...";

            try
            {
                await QueuedTask.Run(() => ProcessPdfFiles(_cancellationTokenSource.Token));
                
                if (_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    StatusText = "转换已取消";
                    LogMessage("转换已被用户取消");
                }
                else
                {
                    StatusText = "转换完成";
                    LogMessage("所有PDF文件转换完成");
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("PDF转图片完成!", "完成");
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
        /// 处理PDF文件（简化并行版本）
        /// </summary>
        private void ProcessPdfFiles(CancellationToken token)
        {
            // 获取所有PDF文件
            SearchOption searchOption = TraverseSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var pdfFiles = Directory.GetFiles(InputFolder, "*.pdf", searchOption).ToList();

            if (pdfFiles.Count == 0)
            {
                LogMessage("未找到PDF文件");
                return;
            }

            LogMessage($"找到 {pdfFiles.Count} 个PDF文件，开始并行处理...");

            int totalFiles = pdfFiles.Count;
            int processedFiles = 0;
            object progressLock = new object();

            // 使用Parallel.ForEach进行并行处理
            Parallel.ForEach(pdfFiles, new ParallelOptions 
            { 
                CancellationToken = token,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            }, pdfFile =>
            {
                if (token.IsCancellationRequested)
                    return;

                try
                {
                    LogMessage($"正在处理: {Path.GetFileName(pdfFile)}");
                    ProcessSinglePdf(pdfFile, token);
                    
                    // 线程安全更新进度
                    lock (progressLock)
                    {
                        processedFiles++;
                        Progress = (double)processedFiles / totalFiles * 100;
                        StatusText = $"已处理 {processedFiles}/{totalFiles} 个文件";
                    }
                }
                catch (Exception ex)
                {
                    LogError($"处理文件 {Path.GetFileName(pdfFile)} 时出错: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 处理单个PDF文件
        /// </summary>
        private void ProcessSinglePdf(string pdfFile, CancellationToken token)
        {
            // 确定输出目录
            string outputDir;
            if (SaveToSourcePath)
            {
                outputDir = Path.GetDirectoryName(pdfFile);
            }
            else
            {
                if (KeepOriginalStructure)
                {
                    // 保持原始目录结构
                    string relativePath = Path.GetDirectoryName(pdfFile).Substring(InputFolder.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    outputDir = Path.Combine(OutputFolder, relativePath);
                }
                else
                {
                    outputDir = OutputFolder;
                }
            }

            // 如果需要为每个PDF创建单独文件夹
            if (CreateSeparateFolder)
            {
                string pdfNameWithoutExt = Path.GetFileNameWithoutExtension(pdfFile);
                outputDir = Path.Combine(outputDir, pdfNameWithoutExt);
            }

            // 确保输出目录存在
            Directory.CreateDirectory(outputDir);

            // 使用PDFtoImage转换PDF（基于PDFium，无需Ghostscript）
            // 使用Stream方式读取以避免文件名特殊字符问题
            // 使用用户设置的DPI参数来控制输出尺寸和清晰度
            var options = new RenderOptions
            {
                Dpi = Resolution  // 使用用户设置的分辨率
            };

            // 确定图片格式
            SKEncodedImageFormat imageFormat = OutputFormat.ToUpper() switch
            {
                "JPG" => SKEncodedImageFormat.Jpeg,
                "JPEG" => SKEncodedImageFormat.Jpeg,
                "PNG" => SKEncodedImageFormat.Png,
                "WEBP" => SKEncodedImageFormat.Webp,
                _ => SKEncodedImageFormat.Jpeg
            };

            int quality = (imageFormat == SKEncodedImageFormat.Jpeg) ? 90 : 100;
            int pageNumber = 1;

            // 使用文件流读取PDF，避免路径特殊字符问题
            using (var pdfStream = File.OpenRead(pdfFile))
            {
                var images = Conversion.ToImages(pdfStream, options: options);

                foreach (var image in images)
                {
                    if (token.IsCancellationRequested)
                        break;

                    string extension = OutputFormat.ToLower();
                    string outputFileName = $"{Path.GetFileNameWithoutExtension(pdfFile)}_page{pageNumber:D3}.{extension}";
                    string outputPath = Path.Combine(outputDir, outputFileName);

                    // 保存图片
                    using (var stream = File.Create(outputPath))
                    {
                        image.Encode(stream, imageFormat, quality);
                    }

                    image.Dispose();
                    LogMessage($"  - 已保存第 {pageNumber} 页: {outputFileName}");
                    pageNumber++;
                }
            }

            LogMessage($"完成处理: {Path.GetFileName(pdfFile)} (共 {pageNumber - 1} 页)");
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

            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(helpMessage, "帮助");
        }

        /// <summary>
        /// 记录消息（简化版本）
        /// </summary>
        private void LogMessage(string message)
        {
            try
            {
                LogText += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
            }
            catch
            {
                // 忽略UI更新异常，避免阻塞处理
            }
        }

        /// <summary>
        /// 记录错误（简化版本）
        /// </summary>
        private void LogError(string error)
        {
            try
            {
                LogText += $"[{DateTime.Now:HH:mm:ss}] [错误] {error}\n";
            }
            catch
            {
                // 忽略UI更新异常，避免阻塞处理
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
