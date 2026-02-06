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
using XIAOFUTools.Common;
using Word = Microsoft.Office.Interop.Word;

namespace XIAOFUTools.Tools.WordToPdf
{
    internal class WordToPdfDockPaneViewModel : INotifyPropertyChanged
    {
        #region 属性

        private string _inputFolder;
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

        private bool _traverseSubfolders = true;
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

        public bool ShowKeepOriginalStructure => !SaveToSourcePath && !string.IsNullOrWhiteSpace(OutputFolder);
        public bool ShowOutputFolder => !SaveToSourcePath;

        private double _progress;
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

        private static readonly string[] SupportedWordExtensions = { ".docx", ".doc", ".docm" };

        #endregion

        #region 命令

        public ICommand SelectInputFolderCommand { get; }
        public ICommand SelectOutputFolderCommand { get; }
        public ICommand StartConversionCommand { get; }
        public ICommand StopConversionCommand { get; }
        public ICommand HelpCommand { get; }

        #endregion

        #region 构造函数

        public WordToPdfDockPaneViewModel()
        {
            SelectInputFolderCommand = new RelayCommand(SelectInputFolder);
            SelectOutputFolderCommand = new RelayCommand(SelectOutputFolder);
            StartConversionCommand = new RelayCommand(async () => await StartConversionAsync(), () => !IsProcessing);
            StopConversionCommand = new RelayCommand(StopConversion, () => IsProcessing);
            HelpCommand = new RelayCommand(ShowHelp);

            LogMessage("Word批量转PDF工具已加载");
        }

        #endregion

        #region 方法

        private void SelectInputFolder()
        {
            var picked = PathDialogUtils.PickFolder("选择包含Word文件的输入文件夹", InputFolder);
            if (!string.IsNullOrWhiteSpace(picked))
            {
                InputFolder = picked;
                LogMessage($"已选择输入文件夹: {InputFolder}");
            }
        }

        private void SelectOutputFolder()
        {
            var picked = PathDialogUtils.PickFolder("选择输出文件夹", OutputFolder);
            if (!string.IsNullOrWhiteSpace(picked))
            {
                OutputFolder = picked;
                LogMessage($"已选择输出文件夹: {OutputFolder}");
            }
        }

        private async System.Threading.Tasks.Task StartConversionAsync()
        {
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
                await System.Threading.Tasks.Task.Run(() => ProcessWordFiles(_cancellationTokenSource.Token));
                
                if (_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    StatusText = "转换已取消";
                    LogMessage("转换已被用户取消");
                }
                else
                {
                    StatusText = "转换完成";
                    LogMessage("所有Word转PDF完成");
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("Word转PDF完成!", "完成");
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

        private void ProcessWordFiles(CancellationToken token)
        {
            SearchOption searchOption = TraverseSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var wordFiles = GetWordFilesInFolder(InputFolder, searchOption);

            if (wordFiles.Count == 0)
            {
                LogMessage("未找到Word文件");
                return;
            }

            LogMessage($"找到 {wordFiles.Count} 个Word文件，开始处理...");

            int totalFiles = wordFiles.Count;
            int processedFiles = 0;

            foreach (var wordFile in wordFiles)
            {
                if (token.IsCancellationRequested)
                    break;

                Word.Application wordApp = null;
                try
                {
                    string fileName = Path.GetFileName(wordFile);
                    LogMessage($"正在处理: {fileName}");

                    wordApp = new Word.Application();
                    wordApp.Visible = false;
                    wordApp.DisplayAlerts = Word.WdAlertLevel.wdAlertsNone;
                    wordApp.ScreenUpdating = false;

                    ConvertWordToPdf(wordApp, wordFile, token);
                    
                    LogMessage($"  - 完成: {fileName}");
                }
                catch (Exception ex)
                {
                    LogError($"处理文件 {Path.GetFileName(wordFile)} 时出错: {ex.Message}");
                }
                finally
                {
                    if (wordApp != null)
                    {
                        try
                        {
                            wordApp.Quit(SaveChanges: false);
                            System.Runtime.InteropServices.Marshal.ReleaseComObject(wordApp);
                            LogMessage("  - Word实例已释放");
                        }
                        catch (Exception ex)
                        {
                            LogError($"  - 释放Word实例失败: {ex.Message}");
                        }
                        wordApp = null;
                    }
                }

                processedFiles++;
                Progress = (double)processedFiles / totalFiles * 100;
                StatusText = $"已处理 {processedFiles}/{totalFiles} 个文件";
            }
        }

        private void ConvertWordToPdf(Word.Application wordApp, string wordFilePath, CancellationToken token)
        {
            Word.Document doc = null;

            try
            {
                doc = wordApp.Documents.Open(
                    FileName: wordFilePath,
                    ReadOnly: true,
                    AddToRecentFiles: false,
                    Visible: false);

                string pdfName = Path.GetFileNameWithoutExtension(wordFilePath);
                string outputPath = GetOutputPdfPath(Path.GetDirectoryName(wordFilePath), pdfName);

                doc.ExportAsFixedFormat(
                    outputPath,
                    Word.WdExportFormat.wdExportFormatPDF,
                    false,
                    Word.WdExportOptimizeFor.wdExportOptimizeForPrint,
                    Word.WdExportRange.wdExportAllDocument,
                    0,
                    0,
                    Word.WdExportItem.wdExportDocumentContent,
                    true,
                    true,
                    Word.WdExportCreateBookmarks.wdExportCreateHeadingBookmarks,
                    true,
                    true,
                    false);
            }
            finally
            {
                if (doc != null)
                {
                    try
                    {
                        doc.Close(false);
                        System.Runtime.InteropServices.Marshal.ReleaseComObject(doc);
                    }
                    catch
                    {
                    }
                    doc = null;
                }
            }
        }

        private List<string> GetWordFilesInFolder(string folder, SearchOption searchOption)
        {
            var wordFiles = new List<string>();

            foreach (var ext in SupportedWordExtensions)
            {
                try
                {
                    wordFiles.AddRange(Directory.GetFiles(folder, $"*{ext}", searchOption));
                }
                catch
                {
                }
            }

            wordFiles.Sort();

            return wordFiles;
        }

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
                    string relativePath = sourceFolderPath.Substring(InputFolder.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    outputDir = Path.Combine(OutputFolder, relativePath);
                }
                else
                {
                    outputDir = OutputFolder;
                }
            }

            Directory.CreateDirectory(outputDir);

            string outputPath = Path.Combine(outputDir, $"{pdfName}.pdf");
            int counter = 1;
            while (File.Exists(outputPath))
            {
                outputPath = Path.Combine(outputDir, $"{pdfName}_{counter}.pdf");
                counter++;
            }

            return outputPath;
        }

        private void StopConversion()
        {
            _cancellationTokenSource?.Cancel();
            LogMessage("正在停止转换...");
        }

        private void ShowHelp()
        {
            string helpMessage = "Word批量转PDF工具\n\n" +
                "功能说明:\n" +
                "1. 批量将Word文件转换为PDF格式\n" +
                "2. 支持docx、doc、docm格式\n" +
                "3. 支持保持原文件夹结构输出\n\n" +
                "使用步骤:\n" +
                "1. 选择包含Word文件的输入文件夹\n" +
                "2. 选择输出文件夹（或勾选保存在源路径）\n" +
                "3. 配置转换选项:\n" +
                "   - 是否按原结构输出\n" +
                "   - 是否遍历子文件夹\n" +
                "4. 点击开始按钮进行转换\n\n" +
                "注意事项:\n" +
                "- 需要安装Microsoft Word\n" +
                "- 处理大量文件可能需要较长时间\n" +
                "- 处理过程中可以点击停止按钮中止操作";

            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(helpMessage, "帮助");
        }

        private void LogMessage(string message)
        {
            try
            {
                LogText += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
            }
            catch
            {
            }
        }

        private void LogError(string error)
        {
            try
            {
                LogText += $"[{DateTime.Now:HH:mm:ss}] [错误] {error}\n";
            }
            catch
            {
            }
        }

        #endregion

        #region RelayCommand 实现

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
