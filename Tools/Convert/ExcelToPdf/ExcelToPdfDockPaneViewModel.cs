using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using XIAOFUTools.Common;
using Excel = Microsoft.Office.Interop.Excel;

namespace XIAOFUTools.Tools.ExcelToPdf
{
    internal class ExcelToPdfDockPaneViewModel : INotifyPropertyChanged
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

        private bool _separateWorksheets;
        public bool SeparateWorksheets
        {
            get => _separateWorksheets;
            set
            {
                if (_separateWorksheets != value)
                {
                    _separateWorksheets = value;
                    OnPropertyChanged(nameof(SeparateWorksheets));
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

        private int _selectedPageOrientation;
        public int SelectedPageOrientation
        {
            get => _selectedPageOrientation;
            set
            {
                if (_selectedPageOrientation != value)
                {
                    _selectedPageOrientation = value;
                    OnPropertyChanged(nameof(SelectedPageOrientation));
                }
            }
        }

        private int _selectedPageSize;
        public int SelectedPageSize
        {
            get => _selectedPageSize;
            set
            {
                if (_selectedPageSize != value)
                {
                    _selectedPageSize = value;
                    OnPropertyChanged(nameof(SelectedPageSize));
                }
            }
        }

        private int _selectedPrintLayout;
        public int SelectedPrintLayout
        {
            get => _selectedPrintLayout;
            set
            {
                if (_selectedPrintLayout != value)
                {
                    _selectedPrintLayout = value;
                    OnPropertyChanged(nameof(SelectedPrintLayout));
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

        private static readonly string[] SupportedExcelExtensions = { ".xlsx", ".xls", ".xlsm" };

        #endregion

        #region 命令

        public ICommand SelectInputFolderCommand { get; }
        public ICommand SelectOutputFolderCommand { get; }
        public ICommand StartConversionCommand { get; }
        public ICommand StopConversionCommand { get; }
        public ICommand HelpCommand { get; }

        #endregion

        #region 构造函数

        public ExcelToPdfDockPaneViewModel()
        {
            SelectInputFolderCommand = new RelayCommand(SelectInputFolder);
            SelectOutputFolderCommand = new RelayCommand(SelectOutputFolder);
            StartConversionCommand = new RelayCommand(async () => await StartConversionAsync(), () => !IsProcessing);
            StopConversionCommand = new RelayCommand(StopConversion, () => IsProcessing);
            HelpCommand = new RelayCommand(ShowHelp);

            LogMessage("Excel批量转PDF工具已加载");
        }

        #endregion

        #region 方法

        private void SelectInputFolder()
        {
            var picked = PathDialogUtils.PickFolder("选择包含Excel文件的输入文件夹", InputFolder);
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
                await System.Threading.Tasks.Task.Run(() => ProcessExcelFiles(_cancellationTokenSource.Token));
                
                if (_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    StatusText = "转换已取消";
                    LogMessage("转换已被用户取消");
                }
                else
                {
                    StatusText = "转换完成";
                    LogMessage("所有Excel转PDF完成");
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("Excel转PDF完成!", "完成");
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

        private void ProcessExcelFiles(CancellationToken token)
        {
            SearchOption searchOption = TraverseSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var excelFiles = GetExcelFilesInFolder(InputFolder, searchOption);

            if (excelFiles.Count == 0)
            {
                LogMessage("未找到Excel文件");
                return;
            }

            LogMessage($"找到 {excelFiles.Count} 个Excel文件，开始处理...");

            int totalFiles = excelFiles.Count;
            int processedFiles = 0;

            Excel.Application excelApp = null;

            try
            {
                excelApp = new Excel.Application();
                excelApp.Visible = false;
                excelApp.DisplayAlerts = false;
                excelApp.ScreenUpdating = false;

                foreach (var excelFile in excelFiles)
                {
                    if (token.IsCancellationRequested)
                        break;

                    try
                    {
                        string fileName = Path.GetFileName(excelFile);
                        LogMessage($"正在处理: {fileName}");

                        ConvertExcelToPdf(excelApp, excelFile, token);
                        
                        LogMessage($"  - 完成: {fileName}");
                    }
                    catch (Exception ex)
                    {
                        LogError($"处理文件 {Path.GetFileName(excelFile)} 时出错: {ex.Message}");
                    }

                    processedFiles++;
                    Progress = (double)processedFiles / totalFiles * 100;
                    StatusText = $"已处理 {processedFiles}/{totalFiles} 个文件";
                }
            }
            finally
            {
                try { if (excelApp != null) { excelApp.Quit(); Marshal.ReleaseComObject(excelApp); } } catch { }
            }
        }

        private void ConvertExcelToPdf(Excel.Application excelApp, string excelFilePath, CancellationToken token)
        {
            Excel.Workbook workbook = null;
            Excel.Workbooks workbooks = null;

            try
            {
                workbooks = excelApp.Workbooks;
                workbook = workbooks.Open(excelFilePath);

                ApplyPageSettings(workbook);

                if (SeparateWorksheets)
                {
                    Excel.Sheets sheets = workbook.Worksheets;
                    try
                    {
                        for (int i = 1; i <= sheets.Count; i++)
                        {
                            if (token.IsCancellationRequested)
                                break;

                            Excel.Worksheet sheet = (Excel.Worksheet)sheets[i];
                            try
                            {
                                string pdfName = $"{Path.GetFileNameWithoutExtension(excelFilePath)}_{sheet.Name}";
                                string outputPath = GetOutputPdfPath(Path.GetDirectoryName(excelFilePath), pdfName);

                                sheet.ExportAsFixedFormat(
                                    Excel.XlFixedFormatType.xlTypePDF,
                                    outputPath,
                                    Excel.XlFixedFormatQuality.xlQualityStandard,
                                    true,
                                    false);

                                LogMessage($"    - 工作表 '{sheet.Name}' 已导出");
                            }
                            catch (Exception ex)
                            {
                                LogError($"    - 导出工作表 '{sheet.Name}' 失败: {ex.Message}");
                            }
                            finally
                            {
                                Marshal.ReleaseComObject(sheet);
                            }
                        }
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(sheets);
                    }
                }
                else
                {
                    string pdfName = Path.GetFileNameWithoutExtension(excelFilePath);
                    string outputPath = GetOutputPdfPath(Path.GetDirectoryName(excelFilePath), pdfName);

                    workbook.ExportAsFixedFormat(
                        Excel.XlFixedFormatType.xlTypePDF,
                        outputPath,
                        Excel.XlFixedFormatQuality.xlQualityStandard,
                        true,
                        false);
                }
            }
            finally
            {
                try { if (workbook != null) { workbook.Close(false); Marshal.ReleaseComObject(workbook); } } catch { }
                try { if (workbooks != null) Marshal.ReleaseComObject(workbooks); } catch { }
            }
        }

        private void ApplyPageSettings(Excel.Workbook workbook)
        {
            Excel.Sheets sheets = workbook.Worksheets;
            try
            {
                for (int i = 1; i <= sheets.Count; i++)
                {
                    Excel.Worksheet sheet = (Excel.Worksheet)sheets[i];
                    try
                    {
                        if (SelectedPageOrientation == 1)
                            sheet.PageSetup.Orientation = Excel.XlPageOrientation.xlLandscape;
                        else if (SelectedPageOrientation == 2)
                            sheet.PageSetup.Orientation = Excel.XlPageOrientation.xlPortrait;

                        if (SelectedPageSize == 1)
                            sheet.PageSetup.PaperSize = Excel.XlPaperSize.xlPaperA4;
                        else if (SelectedPageSize == 2)
                            sheet.PageSetup.PaperSize = Excel.XlPaperSize.xlPaperA3;

                        if (SelectedPrintLayout == 1)
                            sheet.PageSetup.Zoom = false;
                        else if (SelectedPrintLayout == 2)
                        {
                            sheet.PageSetup.Zoom = false;
                            sheet.PageSetup.FitToPagesWide = 1;
                            sheet.PageSetup.FitToPagesTall = 1;
                        }
                        else if (SelectedPrintLayout == 3)
                        {
                            sheet.PageSetup.Zoom = false;
                            sheet.PageSetup.FitToPagesWide = 1;
                            sheet.PageSetup.FitToPagesTall = false;
                        }
                        else if (SelectedPrintLayout == 4)
                        {
                            sheet.PageSetup.Zoom = false;
                            sheet.PageSetup.FitToPagesWide = false;
                            sheet.PageSetup.FitToPagesTall = 1;
                        }
                    }
                    catch
                    {
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(sheet);
                    }
                }
            }
            finally
            {
                Marshal.ReleaseComObject(sheets);
            }
        }

        private List<string> GetExcelFilesInFolder(string folder, SearchOption searchOption)
        {
            var excelFiles = new List<string>();

            foreach (var ext in SupportedExcelExtensions)
            {
                try
                {
                    excelFiles.AddRange(Directory.GetFiles(folder, $"*{ext}", searchOption));
                }
                catch
                {
                }
            }

            excelFiles.Sort();

            return excelFiles;
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
            string helpMessage = "Excel批量转PDF工具\n\n" +
                "功能说明:\n" +
                "1. 批量将Excel文件转换为PDF格式\n" +
                "2. 支持xlsx、xls、xlsm格式\n" +
                "3. 支持工作表分开或整个工作簿导出\n" +
                "4. 可自定义页面方向、大小和打印布局\n\n" +
                "使用步骤:\n" +
                "1. 选择包含Excel文件的输入文件夹\n" +
                "2. 选择输出文件夹（或勾选保存在源路径）\n" +
                "3. 配置转换选项:\n" +
                "   - 是否按原结构输出\n" +
                "   - 是否单独工作表分开\n" +
                "   - 是否遍历子文件夹\n" +
                "4. 设置页面选项（页面方向、大小、打印布局）\n" +
                "5. 点击开始按钮进行转换\n\n" +
                "注意事项:\n" +
                "- 需要安装Microsoft Excel\n" +
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
