using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using Microsoft.Win32;
using XIAOFUTools.Common;
using Word = Microsoft.Office.Interop.Word;

namespace XIAOFUTools.Tools.DocumentBatchReplace
{
    internal class DocumentBatchReplaceDockPaneViewModel : INotifyPropertyChanged
    {
        private static readonly string[] SupportedWordExtensions = { ".docx", ".doc", ".docm" };

        private string _selectedInputFile;
        private string _outputFolder;
        private bool _saveAsCopy = true;
        private bool _traverseSubfolders = true;
        private bool _matchCase;
        private bool _matchWholeWord;
        private bool _matchByte;
        private bool _useWildcards;
        private double _progress;
        private bool _isProgressIndeterminate;
        private string _logText = string.Empty;
        private string _statusText = "等待开始";
        private bool _isProcessing;
        private CancellationTokenSource _cancellationTokenSource;

        public ObservableCollection<string> InputFiles { get; } = new();
        public ObservableCollection<ReplaceRuleItem> ReplaceRules { get; } = new();

        public string SelectedInputFile
        {
            get => _selectedInputFile;
            set
            {
                if (_selectedInputFile != value)
                {
                    _selectedInputFile = value;
                    OnPropertyChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string OutputFolder
        {
            get => _outputFolder;
            set
            {
                if (_outputFolder != value)
                {
                    _outputFolder = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool SaveAsCopy
        {
            get => _saveAsCopy;
            set
            {
                if (_saveAsCopy != value)
                {
                    _saveAsCopy = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(OverwriteOriginal));
                    OnPropertyChanged(nameof(ShowOutputFolder));
                }
            }
        }

        public bool OverwriteOriginal
        {
            get => !SaveAsCopy;
            set
            {
                SaveAsCopy = !value;
            }
        }

        public bool ShowOutputFolder => SaveAsCopy;

        public bool TraverseSubfolders
        {
            get => _traverseSubfolders;
            set
            {
                if (_traverseSubfolders != value)
                {
                    _traverseSubfolders = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool MatchCase
        {
            get => _matchCase;
            set
            {
                if (_matchCase != value)
                {
                    _matchCase = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool MatchWholeWord
        {
            get => _matchWholeWord;
            set
            {
                if (_matchWholeWord != value)
                {
                    _matchWholeWord = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool MatchByte
        {
            get => _matchByte;
            set
            {
                if (_matchByte != value)
                {
                    _matchByte = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool UseWildcards
        {
            get => _useWildcards;
            set
            {
                if (_useWildcards != value)
                {
                    _useWildcards = value;
                    OnPropertyChanged();
                }
            }
        }

        public string FileCountText => $"已添加 {InputFiles.Count} 个文件";

        public double Progress
        {
            get => _progress;
            set
            {
                if (Math.Abs(_progress - value) > 0.01)
                {
                    _progress = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsProgressIndeterminate
        {
            get => _isProgressIndeterminate;
            set
            {
                if (_isProgressIndeterminate != value)
                {
                    _isProgressIndeterminate = value;
                    OnPropertyChanged();
                }
            }
        }

        public string LogText
        {
            get => _logText;
            set
            {
                if (_logText != value)
                {
                    _logText = value;
                    OnPropertyChanged();
                }
            }
        }

        public string StatusText
        {
            get => _statusText;
            set
            {
                if (_statusText != value)
                {
                    _statusText = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                if (_isProcessing != value)
                {
                    _isProcessing = value;
                    OnPropertyChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public ICommand AddFilesCommand { get; }
        public ICommand AddFolderCommand { get; }
        public ICommand RemoveSelectedFileCommand { get; }
        public ICommand ClearFilesCommand { get; }
        public ICommand AddRuleCommand { get; }
        public ICommand RemoveRuleCommand { get; }
        public ICommand ClearRulesCommand { get; }
        public ICommand SelectOutputFolderCommand { get; }
        public ICommand StartReplaceCommand { get; }
        public ICommand StopReplaceCommand { get; }
        public ICommand HelpCommand { get; }

        public DocumentBatchReplaceDockPaneViewModel()
        {
            AddFilesCommand = new RelayCommand(_ => AddFiles());
            AddFolderCommand = new RelayCommand(_ => AddFolder());
            RemoveSelectedFileCommand = new RelayCommand(_ => RemoveSelectedFile(), _ => !string.IsNullOrWhiteSpace(SelectedInputFile));
            ClearFilesCommand = new RelayCommand(_ => ClearFiles(), _ => InputFiles.Count > 0);
            AddRuleCommand = new RelayCommand(_ => AddRule());
            RemoveRuleCommand = new RelayCommand(RemoveRule, CanRemoveRule);
            ClearRulesCommand = new RelayCommand(_ => ClearRules(), _ => ReplaceRules.Count > 0);
            SelectOutputFolderCommand = new RelayCommand(_ => SelectOutputFolder());
            StartReplaceCommand = new RelayCommand(async _ => await StartReplaceAsync(), _ => !IsProcessing);
            StopReplaceCommand = new RelayCommand(_ => StopReplace(), _ => IsProcessing);
            HelpCommand = new RelayCommand(_ => ShowHelp());

            InputFiles.CollectionChanged += OnInputFilesCollectionChanged;
            ReplaceRules.CollectionChanged += OnReplaceRulesCollectionChanged;

            ReplaceRules.Add(new ReplaceRuleItem());
            LogMessage("文档批量替换工具已加载");
        }

        private void OnInputFilesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(FileCountText));
            CommandManager.InvalidateRequerySuggested();
        }

        private void OnReplaceRulesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            CommandManager.InvalidateRequerySuggested();
        }

        public void AddInputPaths(IEnumerable<string> paths)
        {
            if (paths == null)
            {
                return;
            }

            int addedCount = 0;
            int skippedCount = 0;

            foreach (string rawPath in paths)
            {
                if (string.IsNullOrWhiteSpace(rawPath))
                {
                    skippedCount++;
                    continue;
                }

                string fullPath;
                try
                {
                    fullPath = Path.GetFullPath(rawPath);
                }
                catch
                {
                    skippedCount++;
                    continue;
                }

                if (File.Exists(fullPath))
                {
                    if (TryAddFile(fullPath))
                    {
                        addedCount++;
                    }
                    else
                    {
                        skippedCount++;
                    }

                    continue;
                }

                if (!Directory.Exists(fullPath))
                {
                    skippedCount++;
                    continue;
                }

                var searchOption = TraverseSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                foreach (string file in EnumerateWordFiles(fullPath, searchOption))
                {
                    if (TryAddFile(file))
                    {
                        addedCount++;
                    }
                    else
                    {
                        skippedCount++;
                    }
                }
            }

            if (addedCount > 0)
            {
                LogMessage($"已添加 {addedCount} 个文档文件");
            }

            if (skippedCount > 0)
            {
                LogMessage($"已忽略 {skippedCount} 个重复或不支持的路径");
            }
        }

        private void AddFiles()
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择需要替换的文档",
                Filter = "Word文档 (*.docx;*.doc;*.docm)|*.docx;*.doc;*.docm|所有文件 (*.*)|*.*",
                Multiselect = true,
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                AddInputPaths(dialog.FileNames);
            }
        }

        private void AddFolder()
        {
            string folder = PathDialogUtils.PickFolder("选择包含文档的文件夹", string.Empty);
            if (string.IsNullOrWhiteSpace(folder))
            {
                return;
            }

            AddInputPaths(new[] { folder });
        }

        private bool TryAddFile(string filePath)
        {
            string extension = Path.GetExtension(filePath);
            if (!SupportedWordExtensions.Any(ext => string.Equals(ext, extension, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            if (InputFiles.Any(path => string.Equals(path, filePath, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            InputFiles.Add(filePath);
            return true;
        }

        private static IEnumerable<string> EnumerateWordFiles(string folder, SearchOption searchOption)
        {
            var files = new List<string>();

            foreach (string extension in SupportedWordExtensions)
            {
                try
                {
                    files.AddRange(Directory.GetFiles(folder, $"*{extension}", searchOption));
                }
                catch
                {
                }
            }

            files.Sort(StringComparer.OrdinalIgnoreCase);
            return files;
        }

        private void RemoveSelectedFile()
        {
            if (string.IsNullOrWhiteSpace(SelectedInputFile))
            {
                return;
            }

            string selected = SelectedInputFile;
            InputFiles.Remove(selected);
            SelectedInputFile = InputFiles.FirstOrDefault();
            LogMessage($"已移除文件: {Path.GetFileName(selected)}");
        }

        private void ClearFiles()
        {
            if (InputFiles.Count == 0)
            {
                return;
            }

            InputFiles.Clear();
            SelectedInputFile = null;
            LogMessage("已清空所有待处理文件");
        }

        private void AddRule()
        {
            ReplaceRules.Add(new ReplaceRuleItem());
        }

        private bool CanRemoveRule(object parameter)
        {
            return parameter is ReplaceRuleItem && ReplaceRules.Count > 1;
        }

        private void RemoveRule(object parameter)
        {
            if (parameter is not ReplaceRuleItem rule)
            {
                return;
            }

            if (ReplaceRules.Count <= 1)
            {
                return;
            }

            ReplaceRules.Remove(rule);
        }

        private void ClearRules()
        {
            ReplaceRules.Clear();
            ReplaceRules.Add(new ReplaceRuleItem());
        }

        private void SelectOutputFolder()
        {
            string folder = PathDialogUtils.PickFolder("选择副本输出文件夹", OutputFolder);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                OutputFolder = folder;
                LogMessage($"已选择输出文件夹: {folder}");
            }
        }

        private async Task StartReplaceAsync()
        {
            if (InputFiles.Count == 0)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("请先添加需要处理的文档文件", "提示");
                return;
            }

            List<ReplaceRuleItem> activeRules = ReplaceRules
                .Where(rule => !string.IsNullOrWhiteSpace(rule.FindText))
                .Select(rule => new ReplaceRuleItem
                {
                    FindText = rule.FindText,
                    ReplaceText = rule.ReplaceText ?? string.Empty
                })
                .ToList();

            if (activeRules.Count == 0)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("请至少填写一条查找内容", "提示");
                return;
            }

            if (SaveAsCopy && string.IsNullOrWhiteSpace(OutputFolder))
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("请选择副本输出文件夹", "提示");
                return;
            }

            if (SaveAsCopy)
            {
                Directory.CreateDirectory(OutputFolder);
            }

            IsProcessing = true;
            Progress = 0;
            IsProgressIndeterminate = false;
            StatusText = "正在替换...";
            LogText = string.Empty;

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

            List<string> sourceFiles = InputFiles.ToList();

            try
            {
                await RunReplaceOnStaThreadAsync(sourceFiles, activeRules, _cancellationTokenSource.Token);

                if (_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    StatusText = "替换已取消";
                    LogMessage("替换已取消");
                }
                else
                {
                    StatusText = "替换完成";
                    LogMessage("所有文档替换完成");
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("文档批量替换完成", "完成");
                }
            }
            catch (Exception ex)
            {
                StatusText = "替换失败";
                LogError($"替换失败: {ex.Message}");
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"替换失败: {ex.Message}", "错误");
            }
            finally
            {
                IsProcessing = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private Task RunReplaceOnStaThreadAsync(IReadOnlyList<string> sourceFiles, IReadOnlyList<ReplaceRuleItem> rules, CancellationToken token)
        {
            var taskSource = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            var staThread = new Thread(() =>
            {
                try
                {
                    ReplaceDocuments(sourceFiles, rules, token);
                    taskSource.SetResult(null);
                }
                catch (Exception ex)
                {
                    taskSource.SetException(ex);
                }
            });

            staThread.IsBackground = true;
            staThread.SetApartmentState(ApartmentState.STA);
            staThread.Start();

            return taskSource.Task;
        }

        private void ReplaceDocuments(IReadOnlyList<string> sourceFiles, IReadOnlyList<ReplaceRuleItem> rules, CancellationToken token)
        {
            int total = sourceFiles.Count;
            int processed = 0;

            foreach (string sourceFile in sourceFiles)
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                string fileName = Path.GetFileName(sourceFile);

                try
                {
                    LogMessage($"正在处理: {fileName}");
                    string targetFile = SaveAsCopy ? CreateCopyForProcessing(sourceFile) : sourceFile;

                    Word.Application wordApp = null;
                    try
                    {
                        wordApp = CreateWordApplication();
                        ReplaceSingleDocument(wordApp, targetFile, rules, token);
                    }
                    finally
                    {
                        ReleaseWordApplication(wordApp);
                    }

                    if (!token.IsCancellationRequested)
                    {
                        LogMessage($"  - 完成: {Path.GetFileName(targetFile)}");
                    }
                }
                catch (COMException ex)
                {
                    LogError($"处理文件 {fileName} 时发生COM错误: {ex.Message}");
                }
                catch (Exception ex)
                {
                    LogError($"处理文件 {fileName} 时出错: {ex.Message}");
                }

                processed++;
                UpdateProgress(processed, total);
            }
        }

        private static Word.Application CreateWordApplication()
        {
            return new Word.Application
            {
                Visible = false,
                DisplayAlerts = Word.WdAlertLevel.wdAlertsNone,
                ScreenUpdating = false
            };
        }

        private static void ReleaseWordApplication(Word.Application wordApp)
        {
            if (wordApp == null)
            {
                return;
            }

            try
            {
                wordApp.Quit(SaveChanges: false);
            }
            catch
            {
            }

            try
            {
                Marshal.ReleaseComObject(wordApp);
            }
            catch
            {
            }
        }

        private void UpdateProgress(int processed, int total)
        {
            InvokeOnUi(() =>
            {
                Progress = total == 0 ? 0 : (double)processed / total * 100;
                StatusText = $"已处理 {processed}/{total} 个文件";
            });
        }

        private void ReplaceSingleDocument(Word.Application wordApp, string filePath, IReadOnlyList<ReplaceRuleItem> rules, CancellationToken token)
        {
            Word.Document document = null;

            try
            {
                document = wordApp.Documents.Open(
                    FileName: filePath,
                    ReadOnly: false,
                    AddToRecentFiles: false,
                    Visible: false);

                foreach (ReplaceRuleItem rule in rules)
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    ApplyRule(document, rule);
                }

                if (!token.IsCancellationRequested)
                {
                    document.Save();
                }
            }
            finally
            {
                if (document != null)
                {
                    try
                    {
                        document.Close(SaveChanges: false);
                    }
                    catch
                    {
                    }

                    try
                    {
                        Marshal.ReleaseComObject(document);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void ApplyRule(Word.Document document, ReplaceRuleItem rule)
        {
            dynamic find = document.Content.Find;
            find.ClearFormatting();
            find.Replacement.ClearFormatting();
            find.Text = rule.FindText;
            find.Replacement.Text = rule.ReplaceText ?? string.Empty;
            find.Forward = true;
            find.Wrap = Word.WdFindWrap.wdFindContinue;
            find.Format = false;
            find.MatchCase = MatchCase;
            find.MatchWholeWord = MatchWholeWord;
            find.MatchWildcards = UseWildcards;
            find.MatchSoundsLike = false;
            find.MatchAllWordForms = false;

            try
            {
                find.MatchByte = MatchByte;
            }
            catch
            {
            }

            find.Execute(Replace: Word.WdReplace.wdReplaceAll);
        }

        private string CreateCopyForProcessing(string sourceFile)
        {
            string fileName = Path.GetFileName(sourceFile);
            string outputPath = GetUniqueOutputPath(Path.Combine(OutputFolder, fileName));
            File.Copy(sourceFile, outputPath, overwrite: false);
            return outputPath;
        }

        private static string GetUniqueOutputPath(string targetPath)
        {
            if (!File.Exists(targetPath))
            {
                return targetPath;
            }

            string directory = Path.GetDirectoryName(targetPath) ?? string.Empty;
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(targetPath);
            string extension = Path.GetExtension(targetPath);

            int index = 1;
            string candidatePath;
            do
            {
                candidatePath = Path.Combine(directory, $"{fileNameWithoutExtension}_{index}{extension}");
                index++;
            }
            while (File.Exists(candidatePath));

            return candidatePath;
        }

        private void StopReplace()
        {
            _cancellationTokenSource?.Cancel();
            LogMessage("正在停止替换...");
        }

        private void ShowHelp()
        {
            const string helpMessage = "文档批量替换工具\n\n" +
                                       "功能说明:\n" +
                                       "1. 支持 .docx / .doc / .docm 多文档批量替换\n" +
                                       "2. 支持多条替换规则串行执行\n" +
                                       "3. 支持区分大小写、全字匹配、全/半角和通配符\n" +
                                       "4. 支持另存副本与覆盖原文件两种保存方式\n\n" +
                                       "使用步骤:\n" +
                                       "1. 添加文件或文件夹（可拖拽）\n" +
                                       "2. 配置替换规则和匹配选项\n" +
                                       "3. 选择保存方式并设置输出目录\n" +
                                       "4. 点击“开始替换”执行\n\n" +
                                       "注意:\n" +
                                       "- 运行前请关闭正在编辑的目标文档\n" +
                                       "- 建议先使用“另存为副本”验证替换结果\n" +
                                       "- 该功能依赖本机安装 Microsoft Word";

            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(helpMessage, "帮助");
        }

        private void LogMessage(string message)
        {
            InvokeOnUi(() =>
            {
                try
                {
                    LogText += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
                }
                catch
                {
                }
            });
        }

        private void LogError(string message)
        {
            InvokeOnUi(() =>
            {
                try
                {
                    LogText += $"[{DateTime.Now:HH:mm:ss}] [错误] {message}\n";
                }
                catch
                {
                }
            });
        }

        private static void InvokeOnUi(Action action)
        {
            if (action == null)
            {
                return;
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                dispatcher.Invoke(action);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private sealed class RelayCommand : ICommand
        {
            private readonly Action<object> _execute;
            private readonly Predicate<object> _canExecute;

            public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
            {
                _execute = execute ?? throw new ArgumentNullException(nameof(execute));
                _canExecute = canExecute;
            }

            public event EventHandler CanExecuteChanged
            {
                add => CommandManager.RequerySuggested += value;
                remove => CommandManager.RequerySuggested -= value;
            }

            public bool CanExecute(object parameter)
            {
                return _canExecute?.Invoke(parameter) ?? true;
            }

            public void Execute(object parameter)
            {
                _execute(parameter);
            }
        }
    }

    internal class ReplaceRuleItem : INotifyPropertyChanged
    {
        private string _findText = string.Empty;
        private string _replaceText = string.Empty;

        public string FindText
        {
            get => _findText;
            set
            {
                if (_findText != value)
                {
                    _findText = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ReplaceText
        {
            get => _replaceText;
            set
            {
                if (_replaceText != value)
                {
                    _replaceText = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
