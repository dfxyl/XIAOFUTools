using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ArcGIS.Desktop.Framework.Contracts;
using XIAOFUTools.Common;

namespace XIAOFUTools.Tools.DataProcessing.MdbBatchToGdb
{
    internal class MdbBatchToGdbViewModel : PropertyChangedBase
    {
        private readonly ArcGisProMdbToGdbConverter _converter = new ArcGisProMdbToGdbConverter();

        private readonly RelayCommand _browseInputFolderCommand;
        private readonly RelayCommand _browseOutputFolderCommand;
        private readonly RelayCommand _refreshMdbListCommand;
        private readonly RelayCommand _selectAllCommand;
        private readonly RelayCommand _invertSelectionCommand;
        private readonly RelayCommand _clearSelectionCommand;
        private readonly RelayCommand _runCommand;
        private readonly RelayCommand _cancelCommand;
        private readonly RelayCommand _showHelpCommand;

        private CancellationTokenSource _cancellationTokenSource;

        private string _inputFolderPath = string.Empty;
        private bool _includeSubfolders = true;
        private bool _saveToSourcePath = true;
        private string _outputFolderPath = string.Empty;
        private bool _isProcessing;
        private bool _isScanning;
        private int _progress;
        private string _logText = string.Empty;
        private string _selectionSummary = "已选择 0 / 0";

        public MdbBatchToGdbViewModel()
        {
            MdbItems = new ObservableCollection<MdbFileItem>();

            _browseInputFolderCommand = new RelayCommand(BrowseInputFolder, () => !IsBusy);
            _browseOutputFolderCommand = new RelayCommand(BrowseOutputFolder, () => !IsBusy && !SaveToSourcePath);
            _refreshMdbListCommand = new RelayCommand(async () => await RefreshMdbListAsync(), () => CanRefresh);
            _selectAllCommand = new RelayCommand(SelectAll, () => CanEditSelection);
            _invertSelectionCommand = new RelayCommand(InvertSelection, () => CanEditSelection);
            _clearSelectionCommand = new RelayCommand(ClearSelection, () => CanEditSelection);
            _runCommand = new RelayCommand(async () => await RunAsync(), () => CanRun);
            _cancelCommand = new RelayCommand(Cancel, () => IsProcessing);
            _showHelpCommand = new RelayCommand(ShowHelp);

            string defaultWorkspace = PathDialogUtils.GetProjectDefaultGdb();
            string defaultOutputFolder = Path.GetDirectoryName(defaultWorkspace);
            OutputFolderPath = string.IsNullOrWhiteSpace(defaultOutputFolder)
                ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                : defaultOutputFolder;

            AppendInfo("工具已加载，请先选择包含 MDB 的文件夹。");
        }

        public ICommand BrowseInputFolderCommand => _browseInputFolderCommand;

        public ICommand BrowseOutputFolderCommand => _browseOutputFolderCommand;

        public ICommand RefreshMdbListCommand => _refreshMdbListCommand;

        public ICommand SelectAllCommand => _selectAllCommand;

        public ICommand InvertSelectionCommand => _invertSelectionCommand;

        public ICommand ClearSelectionCommand => _clearSelectionCommand;

        public ICommand RunCommand => _runCommand;

        public ICommand CancelCommand => _cancelCommand;

        public ICommand ShowHelpCommand => _showHelpCommand;

        public ObservableCollection<MdbFileItem> MdbItems { get; }

        public string InputFolderPath
        {
            get => _inputFolderPath;
            set
            {
                if (SetProperty(ref _inputFolderPath, value))
                {
                    NotifyStatePropertiesChanged();
                    RaiseCommandCanExecuteChanged();
                }
            }
        }

        public bool IncludeSubfolders
        {
            get => _includeSubfolders;
            set
            {
                if (SetProperty(ref _includeSubfolders, value))
                {
                    NotifyStatePropertiesChanged();
                    RaiseCommandCanExecuteChanged();

                    if (!IsBusy && Directory.Exists(InputFolderPath))
                    {
                        _ = RefreshMdbListAsync();
                    }
                }
            }
        }

        public bool SaveToSourcePath
        {
            get => _saveToSourcePath;
            set
            {
                if (SetProperty(ref _saveToSourcePath, value))
                {
                    NotifyPropertyChanged(() => ShowOutputFolder);
                    NotifyStatePropertiesChanged();
                    RaiseCommandCanExecuteChanged();
                }
            }
        }

        public bool ShowOutputFolder => !SaveToSourcePath;

        public string OutputFolderPath
        {
            get => _outputFolderPath;
            set
            {
                if (SetProperty(ref _outputFolderPath, value))
                {
                    NotifyStatePropertiesChanged();
                    RaiseCommandCanExecuteChanged();
                }
            }
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                if (SetProperty(ref _isProcessing, value))
                {
                    NotifyPropertyChanged(() => IsBusy);
                    NotifyStatePropertiesChanged();
                    RaiseCommandCanExecuteChanged();
                }
            }
        }

        public bool IsBusy => IsProcessing || _isScanning;

        public int Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        public string LogText
        {
            get => _logText;
            set => SetProperty(ref _logText, value);
        }

        public string SelectionSummary
        {
            get => _selectionSummary;
            set => SetProperty(ref _selectionSummary, value);
        }

        public bool CanRefresh => !IsBusy && !string.IsNullOrWhiteSpace(InputFolderPath);

        public bool CanRun
            => !IsBusy
               && HasSelection()
               && !string.IsNullOrWhiteSpace(InputFolderPath)
               && (SaveToSourcePath || !string.IsNullOrWhiteSpace(OutputFolderPath));

        private bool CanEditSelection => !IsBusy && MdbItems.Count > 0;

        private void BrowseInputFolder()
        {
            string pickedPath = PathDialogUtils.PickFolder("选择需要遍历的文件夹", InputFolderPath);
            if (string.IsNullOrWhiteSpace(pickedPath))
            {
                return;
            }

            InputFolderPath = pickedPath;
            AppendInfo($"已选择输入文件夹: {pickedPath}");

            if (string.IsNullOrWhiteSpace(OutputFolderPath))
            {
                OutputFolderPath = pickedPath;
            }

            _ = RefreshMdbListAsync();
        }

        private void BrowseOutputFolder()
        {
            string pickedPath = PathDialogUtils.PickFolder("选择输出文件夹", OutputFolderPath);
            if (string.IsNullOrWhiteSpace(pickedPath))
            {
                return;
            }

            if (pickedPath.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase))
            {
                pickedPath = Path.GetDirectoryName(pickedPath) ?? pickedPath;
            }

            OutputFolderPath = pickedPath;
            AppendInfo($"已选择输出文件夹: {pickedPath}");
        }

        private async Task RefreshMdbListAsync()
        {
            if (IsBusy)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(InputFolderPath))
            {
                AppendWarning("请先选择输入文件夹。");
                return;
            }

            if (!Directory.Exists(InputFolderPath))
            {
                AppendError("输入文件夹不存在。");
                ClearItems();
                return;
            }

            SetScanning(true);
            Progress = 0;

            try
            {
                AppendInfo($"开始扫描 MDB: {InputFolderPath}");
                List<string> files = await Task.Run(() => MdbFileDiscovery.EnumerateMdbFiles(InputFolderPath, IncludeSubfolders));
                List<string> sortedFiles = files.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();

                ReplaceItems(sortedFiles);
                Progress = sortedFiles.Count > 0 ? 100 : 0;
                AppendInfo($"扫描完成，共找到 {sortedFiles.Count} 个 MDB。");
            }
            catch (Exception ex)
            {
                AppendError($"扫描失败: {ex.Message}");
            }
            finally
            {
                SetScanning(false);
            }
        }

        private void SelectAll()
        {
            foreach (MdbFileItem item in MdbItems)
            {
                item.IsSelected = true;
            }

            UpdateSelectionSummary();
            RaiseCommandCanExecuteChanged();
        }

        private void InvertSelection()
        {
            foreach (MdbFileItem item in MdbItems)
            {
                item.IsSelected = !item.IsSelected;
            }

            UpdateSelectionSummary();
            RaiseCommandCanExecuteChanged();
        }

        private void ClearSelection()
        {
            foreach (MdbFileItem item in MdbItems)
            {
                item.IsSelected = false;
            }

            UpdateSelectionSummary();
            RaiseCommandCanExecuteChanged();
        }

        private async Task RunAsync()
        {
            if (!CanRun)
            {
                return;
            }

            List<MdbFileItem> selectedItems = MdbItems
                .Where(item => item.IsSelected)
                .ToList();
            if (selectedItems.Count == 0)
            {
                AppendWarning("没有选中任何 MDB。\n");
                return;
            }

            if (!SaveToSourcePath)
            {
                try
                {
                    string outputFolder = OutputFolderPath;
                    if (!string.IsNullOrWhiteSpace(outputFolder)
                        && outputFolder.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase))
                    {
                        outputFolder = Path.GetDirectoryName(outputFolder) ?? outputFolder;
                    }

                    Directory.CreateDirectory(outputFolder);
                }
                catch (Exception ex)
                {
                    AppendError($"无法创建输出目录: {ex.Message}");
                    return;
                }
            }

            var plans = MdbConversionPlanner.BuildPlans(
                selectedItems,
                SaveToSourcePath,
                InputFolderPath,
                OutputFolderPath,
                AppendWarning);
            if (plans.Count == 0)
            {
                AppendWarning("没有可执行的转换任务。");
                return;
            }

            var existingOutputs = plans
                .Where(plan => Directory.Exists(plan.OutputPath) || File.Exists(plan.OutputPath))
                .ToList();

            bool overwriteExisting = false;
            if (existingOutputs.Count > 0)
            {
                var result = MessageBox.Show(
                    $"检测到 {existingOutputs.Count} 个输出已存在，是否覆盖？\n选择“否”将跳过这些任务。",
                    "确认覆盖",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                overwriteExisting = result == MessageBoxResult.Yes;
                if (!overwriteExisting)
                {
                    AppendWarning("已存在输出将被跳过。");
                }
            }

            IsProcessing = true;
            Progress = 0;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                await Task.Run(() => ExecuteConversionBatchV2(plans, overwriteExisting, _cancellationTokenSource.Token));

                if (_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    AppendWarning("操作已取消。");
                }
                else
                {
                    AppendInfo("批量转换已完成。");
                    MessageBox.Show("MDB 批量转 GDB 完成。", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (OperationCanceledException)
            {
                AppendWarning("操作已取消。");
            }
            catch (Exception ex)
            {
                AppendError($"批量转换失败: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private void ExecuteConversionBatch(IReadOnlyList<MdbConversionPlan> plans, bool overwriteExisting, CancellationToken token)
        {
            int total = plans.Count;
            int completed = 0;

            foreach (MdbConversionPlan plan in plans)
            {
                token.ThrowIfCancellationRequested();

                UpdateItemStatus(plan.Item, "转换中");
                AppendInfo($"[{completed + 1}/{total}] {plan.Item.Name} -> {plan.OutputPath}");

                try
                {
                    bool outputExists = Directory.Exists(plan.OutputPath) || File.Exists(plan.OutputPath);
                    if (outputExists)
                    {
                        if (!overwriteExisting)
                        {
                            UpdateItemStatus(plan.Item, "已跳过(已存在)");
                            AppendWarning($"跳过已存在输出: {plan.OutputPath}");
                            continue;
                        }

                        DeleteOutput(plan.OutputPath);
                        AppendInfo($"已删除旧输出: {plan.OutputPath}");
                    }

                    _converter.Convert(plan.Item.FullPath, plan.OutputPath, message => AppendInfo($"  {message}"), token);

                    UpdateItemStatus(plan.Item, "完成");
                    AppendInfo($"完成: {plan.OutputPath}");
                }
                catch (OperationCanceledException)
                {
                    UpdateItemStatus(plan.Item, "已取消");
                    throw;
                }
                catch (Exception ex)
                {
                    UpdateItemStatus(plan.Item, "失败");
                    AppendError($"{plan.Item.Name} 转换失败: {ex.Message}");
                }
                finally
                {
                    completed++;
                    UpdateProgress(completed, total);
                }
            }
        }

        private void ExecuteConversionBatchV2(IReadOnlyList<MdbConversionPlan> plans, bool overwriteExisting, CancellationToken token)
        {
            int total = plans.Count;
            int completed = 0;
            var runnablePlans = new List<MdbConversionPlan>();
            var syncRoot = new object();

            foreach (MdbConversionPlan plan in plans)
            {
                token.ThrowIfCancellationRequested();

                try
                {
                    bool outputExists = Directory.Exists(plan.OutputPath) || File.Exists(plan.OutputPath);
                    if (outputExists)
                    {
                        if (!overwriteExisting)
                        {
                            UpdateItemStatus(plan.Item, "宸茶烦杩?宸插瓨鍦?");
                            AppendWarning($"璺宠繃宸插瓨鍦ㄨ緭鍑? {plan.OutputPath}");
                            completed++;
                            UpdateProgress(completed, total);
                            continue;
                        }

                        DeleteOutput(plan.OutputPath);
                    AppendInfo($"Deleted old output: {plan.OutputPath}");
                    }

                    UpdateItemStatus(plan.Item, "Pending");
                    runnablePlans.Add(plan);
                }
                catch (Exception ex)
                {
                    UpdateItemStatus(plan.Item, "Failed");
                    AppendError($"{plan.Item.Name} conversion failed: {ex.Message}");
                    completed++;
                    UpdateProgress(completed, total);
                }
            }

            if (runnablePlans.Count == 0)
            {
                return;
            }

            _converter.ConvertBatch(
                runnablePlans,
                evt =>
                {
                    MdbConversionPlan plan = runnablePlans[evt.Index];
                    switch (evt.Kind)
                    {
                        case BatchConversionEventKind.Started:
                            UpdateItemStatus(plan.Item, "Running");
                            AppendInfo($"[{evt.Index + 1}/{runnablePlans.Count}] {plan.Item.Name} -> {plan.OutputPath}");
                            break;

                        case BatchConversionEventKind.Completed:
                            UpdateItemStatus(plan.Item, "Done");
                            AppendInfo($"Done: {plan.OutputPath}");
                            lock (syncRoot)
                            {
                                completed++;
                                UpdateProgress(completed, total);
                            }
                            break;

                        case BatchConversionEventKind.Failed:
                            UpdateItemStatus(plan.Item, "Failed");
                            AppendError($"{plan.Item.Name} conversion failed: {evt.Message}");
                            lock (syncRoot)
                            {
                                completed++;
                                UpdateProgress(completed, total);
                            }
                            break;
                    }
                },
                message => AppendInfo($"  {message}"),
                token);
        }

        private void ReplaceItems(IEnumerable<string> mdbFiles)
        {
            foreach (MdbFileItem item in MdbItems)
            {
                item.PropertyChanged -= OnItemPropertyChanged;
            }

            MdbItems.Clear();
            foreach (string mdbPath in mdbFiles)
            {
                var item = new MdbFileItem
                {
                    IsSelected = true,
                    Name = Path.GetFileNameWithoutExtension(mdbPath),
                    FullPath = mdbPath,
                    RelativePath = MdbFileDiscovery.BuildRelativePath(InputFolderPath, mdbPath)
                };

                item.PropertyChanged += OnItemPropertyChanged;
                MdbItems.Add(item);
            }

            UpdateSelectionSummary();
            NotifyStatePropertiesChanged();
            RaiseCommandCanExecuteChanged();
        }

        private void ClearItems()
        {
            foreach (MdbFileItem item in MdbItems)
            {
                item.PropertyChanged -= OnItemPropertyChanged;
            }

            MdbItems.Clear();
            UpdateSelectionSummary();
            NotifyStatePropertiesChanged();
            RaiseCommandCanExecuteChanged();
        }

        private void OnItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!string.Equals(e.PropertyName, nameof(MdbFileItem.IsSelected), StringComparison.Ordinal))
            {
                return;
            }

            UpdateSelectionSummary();
            NotifyStatePropertiesChanged();
            RaiseCommandCanExecuteChanged();
        }

        private void UpdateSelectionSummary()
        {
            int total = MdbItems.Count;
            int selected = MdbItems.Count(item => item.IsSelected);
            SelectionSummary = $"已选择 {selected} / {total}";
        }

        private bool HasSelection()
        {
            return MdbItems.Any(item => item.IsSelected);
        }

        private static void DeleteOutput(string outputPath)
        {
            if (Directory.Exists(outputPath))
            {
                Directory.Delete(outputPath, true);
                return;
            }

            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }

        private void Cancel()
        {
            if (!IsProcessing)
            {
                return;
            }

            _cancellationTokenSource?.Cancel();
            AppendWarning("正在请求停止...");
        }

        private void ShowHelp()
        {
            const string helpText =
                "MDB批量转GDB 工具说明\n\n" +
                "1. 选择输入文件夹后，工具会自动扫描并列出所有 MDB。\n" +
                "2. 可勾选“遍历子文件夹”控制是否递归扫描。\n" +
                "3. 列表支持按需勾选需要转换的 MDB。\n" +
                "4. 输出支持两种模式：\n" +
                "   - 输出到源路径：每个 MDB 在原目录生成同名 GDB。\n" +
                "   - 指定输出文件夹：统一输出到同一目录。\n" +
                "5. 转换使用 ArcGIS Pro 自带 Python 引擎，保留要素数据集与表结构。\n\n" +
                "注意：转换 MDB 需系统安装 Access Database Engine。";

            MessageBox.Show(helpText, "MDB批量转GDB 工具说明", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SetScanning(bool scanning)
        {
            if (_isScanning == scanning)
            {
                return;
            }

            _isScanning = scanning;
            NotifyPropertyChanged(() => IsBusy);
            NotifyStatePropertiesChanged();
            RaiseCommandCanExecuteChanged();
        }

        private void UpdateItemStatus(MdbFileItem item, string status)
        {
            ExecuteOnUiThread(() => item.Status = status);
        }

        private void UpdateProgress(int completed, int total)
        {
            int value = total <= 0 ? 0 : (int)Math.Round((double)completed / total * 100);
            ExecuteOnUiThread(() => Progress = value);
        }

        private void NotifyStatePropertiesChanged()
        {
            NotifyPropertyChanged(() => CanRun);
            NotifyPropertyChanged(() => CanRefresh);
        }

        private void RaiseCommandCanExecuteChanged()
        {
            _browseInputFolderCommand.RaiseCanExecuteChanged();
            _browseOutputFolderCommand.RaiseCanExecuteChanged();
            _refreshMdbListCommand.RaiseCanExecuteChanged();
            _selectAllCommand.RaiseCanExecuteChanged();
            _invertSelectionCommand.RaiseCanExecuteChanged();
            _clearSelectionCommand.RaiseCanExecuteChanged();
            _runCommand.RaiseCanExecuteChanged();
            _cancelCommand.RaiseCanExecuteChanged();
        }

        private void AppendInfo(string message)
        {
            AppendLog(message);
        }

        private void AppendWarning(string message)
        {
            AppendLog("警告: " + message);
        }

        private void AppendError(string message)
        {
            AppendLog("错误: " + message);
        }

        private void AppendLog(string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {message}\n";
            ExecuteOnUiThread(() => LogText += line);
        }

        private static void ExecuteOnUiThread(Action action)
        {
            if (action == null)
            {
                return;
            }

            if (Application.Current?.Dispatcher?.CheckAccess() == true)
            {
                action();
            }
            else
            {
                Application.Current?.Dispatcher?.Invoke(action);
            }
        }
    }
}
