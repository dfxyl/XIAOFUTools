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
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using XIAOFUTools.Common;

namespace XIAOFUTools.Tools.DataProcessing.BatchMergeShp
{
    /// <summary>
    /// SHP 列表项。
    /// </summary>
    public sealed class ShpMergeItem : PropertyChangedBase
    {
        private bool _isSelected = true;

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public string Name { get; set; } = string.Empty;
        public string GeometryType { get; set; } = "未知";
        public string GeometryKind { get; set; } = "Unknown";
        public string Directory { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
    }

    /// <summary>
    /// 批量合并 SHP 视图模型。
    /// </summary>
    internal class BatchMergeShpViewModel : PropertyChangedBase
    {
        private readonly RelayCommand _browseInputFolderCommand;
        private readonly RelayCommand _refreshShpListCommand;
        private readonly RelayCommand _browseOutputPathCommand;
        private readonly RelayCommand _selectAllCommand;
        private readonly RelayCommand _invertSelectionCommand;
        private readonly RelayCommand _clearSelectionCommand;
        private readonly RelayCommand _selectPointCommand;
        private readonly RelayCommand _selectLineCommand;
        private readonly RelayCommand _selectPolygonCommand;
        private readonly RelayCommand _runCommand;
        private readonly RelayCommand _cancelCommand;
        private readonly RelayCommand _showHelpCommand;

        private CancellationTokenSource _cancellationTokenSource;
        private string _inputFolderPath = string.Empty;
        private bool _includeSubfolders = true;
        private string _outputPath = string.Empty;
        private bool _addSourceFileField = true;
        private bool _isProcessing;
        private bool _isScanning;
        private int _progress;
        private string _logText = string.Empty;
        private string _selectionSummary = "已选择 0 / 0";

        public BatchMergeShpViewModel()
        {
            ShpItems = new ObservableCollection<ShpMergeItem>();

            _browseInputFolderCommand = new RelayCommand(BrowseInputFolder, () => !IsBusy);
            _refreshShpListCommand = new RelayCommand(async () => await RefreshShpListAsync(), () => CanRefresh);
            _browseOutputPathCommand = new RelayCommand(BrowseOutputPath, () => !IsBusy);
            _selectAllCommand = new RelayCommand(SelectAll, () => CanEditSelection);
            _invertSelectionCommand = new RelayCommand(InvertSelection, () => CanEditSelection);
            _clearSelectionCommand = new RelayCommand(ClearSelection, () => CanEditSelection);
            _selectPointCommand = new RelayCommand(() => SelectByGeometryKind("Point", "点"), () => CanEditSelection);
            _selectLineCommand = new RelayCommand(() => SelectByGeometryKind("Polyline", "线"), () => CanEditSelection);
            _selectPolygonCommand = new RelayCommand(() => SelectByGeometryKind("Polygon", "面"), () => CanEditSelection);
            _runCommand = new RelayCommand(async () => await RunAsync(), () => CanRun);
            _cancelCommand = new RelayCommand(Cancel, () => IsProcessing);
            _showHelpCommand = new RelayCommand(ShowHelp);

            OutputPath = OutputDatasetUtils.NormalizeOutputPath(Path.Combine(PathDialogUtils.GetProjectDefaultGdb(), "MergedSHP"), "MergedSHP");
            AppendInfo("工具已加载，选择输入文件夹后将自动扫描 SHP。");
        }

        public ICommand BrowseInputFolderCommand => _browseInputFolderCommand;
        public ICommand RefreshShpListCommand => _refreshShpListCommand;
        public ICommand BrowseOutputPathCommand => _browseOutputPathCommand;
        public ICommand SelectAllCommand => _selectAllCommand;
        public ICommand InvertSelectionCommand => _invertSelectionCommand;
        public ICommand ClearSelectionCommand => _clearSelectionCommand;
        public ICommand SelectPointCommand => _selectPointCommand;
        public ICommand SelectLineCommand => _selectLineCommand;
        public ICommand SelectPolygonCommand => _selectPolygonCommand;
        public ICommand RunCommand => _runCommand;
        public ICommand CancelCommand => _cancelCommand;
        public ICommand ShowHelpCommand => _showHelpCommand;

        public ObservableCollection<ShpMergeItem> ShpItems { get; }

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
                        _ = RefreshShpListAsync();
                    }
                }
            }
        }

        public string OutputPath
        {
            get => _outputPath;
            set
            {
                if (SetProperty(ref _outputPath, value))
                {
                    NotifyStatePropertiesChanged();
                    RaiseCommandCanExecuteChanged();
                }
            }
        }

        public bool AddSourceFileField
        {
            get => _addSourceFileField;
            set => SetProperty(ref _addSourceFileField, value);
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

        public bool CanRun => !IsBusy
                              && !string.IsNullOrWhiteSpace(OutputPath)
                              && HasValidGeometrySelection();

        private bool CanEditSelection => !IsBusy && ShpItems.Count > 0;

        private void BrowseInputFolder()
        {
            var pickedPath = PathDialogUtils.PickFolder("选择需要遍历的文件夹", InputFolderPath);
            if (string.IsNullOrWhiteSpace(pickedPath))
            {
                return;
            }

            InputFolderPath = pickedPath;
            AppendInfo($"已选择输入文件夹: {pickedPath}");

            if (string.IsNullOrWhiteSpace(OutputPath))
            {
                OutputPath = Path.Combine(pickedPath, "MergedSHP.shp");
            }

            _ = RefreshShpListAsync();
        }

        private async Task RefreshShpListAsync()
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
                AppendInfo($"开始扫描: {InputFolderPath}");
                var files = await Task.Run(() =>
                {
                    var list = SafeEnumerateShpFiles(InputFolderPath, IncludeSubfolders);
                    return list.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
                });

                ReplaceItems(files);
                Progress = files.Count > 0 ? 100 : 0;
                AppendInfo($"扫描完成，共找到 {files.Count} 个 SHP。");
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

        private void BrowseOutputPath()
        {
            string initialLocation = null;
            if (!string.IsNullOrWhiteSpace(OutputPath))
            {
                if (OutputPath.IndexOf(".gdb", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    initialLocation = OutputPath;
                }
                else
                {
                    initialLocation = Path.GetDirectoryName(OutputPath);
                }
            }

            if (string.IsNullOrWhiteSpace(initialLocation))
            {
                initialLocation = string.IsNullOrWhiteSpace(InputFolderPath)
                    ? PathDialogUtils.GetProjectDefaultGdb()
                    : InputFolderPath;
            }

            var selectedPath = PathDialogUtils.PickSaveFeatureClassPath("选择合并输出位置", initialLocation);
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return;
            }

            OutputPath = OutputDatasetUtils.NormalizeOutputPath(selectedPath, "MergedSHP");
            AppendInfo($"输出位置: {OutputPath}");
        }

        private void SelectAll()
        {
            foreach (var item in ShpItems)
            {
                item.IsSelected = true;
            }

            UpdateSelectionSummary();
            RaiseCommandCanExecuteChanged();
        }

        private void InvertSelection()
        {
            foreach (var item in ShpItems)
            {
                item.IsSelected = !item.IsSelected;
            }

            UpdateSelectionSummary();
            RaiseCommandCanExecuteChanged();
        }

        private void ClearSelection()
        {
            foreach (var item in ShpItems)
            {
                item.IsSelected = false;
            }

            UpdateSelectionSummary();
            RaiseCommandCanExecuteChanged();
        }

        private void SelectByGeometryKind(string geometryKind, string geometryName)
        {
            var count = 0;
            foreach (var item in ShpItems)
            {
                var isMatch = string.Equals(item.GeometryKind, geometryKind, StringComparison.OrdinalIgnoreCase);
                item.IsSelected = isMatch;
                if (isMatch)
                {
                    count++;
                }
            }

            UpdateSelectionSummary();
            NotifyStatePropertiesChanged();
            RaiseCommandCanExecuteChanged();

            AppendInfo($"快捷勾选“{geometryName}”完成，共 {count} 个。");
        }

        private async Task RunAsync()
        {
            if (!CanRun)
            {
                return;
            }

            var selectedItems = ShpItems.Where(item => item.IsSelected).ToList();
            if (selectedItems.Count == 0)
            {
                AppendWarning("没有选中任何 SHP。");
                return;
            }

            var unknownGeometryItems = selectedItems
                .Where(item => string.Equals(item.GeometryKind, "Unknown", StringComparison.OrdinalIgnoreCase))
                .Select(item => item.Name)
                .ToList();
            if (unknownGeometryItems.Count > 0)
            {
                var sample = string.Join("、", unknownGeometryItems.Take(10));
                MessageBox.Show(
                    $"存在无法识别几何类型的 SHP，不能继续合并。\n{sample}",
                    "提示",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                AppendError($"存在未知几何类型: {sample}");
                return;
            }

            var geometryGroups = selectedItems
                .GroupBy(item => item.GeometryKind, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (geometryGroups.Count > 1)
            {
                var detail = string.Join("\n", geometryGroups.Select(group =>
                    $"{GetGeometryDisplayName(group.Key)}: {group.Count()} 个"));
                MessageBox.Show(
                    $"所选 SHP 几何类型不一致，无法合并。\n{detail}",
                    "提示",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                AppendError($"几何类型不一致，已阻止运行。{detail.Replace('\n', ' ')}");
                return;
            }

            IsProcessing = true;
            Progress = 0;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                var normalizedOutput = OutputDatasetUtils.NormalizeOutputPath(OutputPath, "MergedSHP");
                OutputPath = normalizedOutput;

                var outputInfo = OutputDatasetUtils.ParseOutputPath(normalizedOutput, "MergedSHP");
                if (!outputInfo.IsGdb && !Directory.Exists(outputInfo.OutPathWorkspace))
                {
                    Directory.CreateDirectory(outputInfo.OutPathWorkspace);
                }

                var exists = await QueuedTask.Run(() => OutputDatasetUtils.Exists(outputInfo));
                if (exists)
                {
                    var overwrite = MessageBox.Show(
                        $"输出已存在，是否覆盖？\n{outputInfo.CatalogPath}",
                        "确认覆盖",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (overwrite != MessageBoxResult.Yes)
                    {
                        AppendWarning("用户取消了输出覆盖，操作已停止。");
                        return;
                    }

                    AppendInfo("正在删除已存在输出...");
                    var deleteParams = Geoprocessing.MakeValueArray(outputInfo.CatalogPath);
                    var deleteResult = await Geoprocessing.ExecuteToolAsync("Delete_management", deleteParams, null, _cancellationTokenSource.Token);
                    if (deleteResult.IsFailed)
                    {
                        throw new InvalidOperationException("删除旧输出失败: " + GetGpMessageText(deleteResult));
                    }
                }

                Progress = 20;
                AppendInfo($"开始合并，输入数量: {selectedItems.Count}");

                var mergeInputs = string.Join(";", selectedItems.Select(item => item.FullPath));
                var addSourceInfo = AddSourceFileField ? "ADD_SOURCE_INFO" : "NO_SOURCE_INFO";
                var mergeParams = Geoprocessing.MakeValueArray(mergeInputs, outputInfo.CatalogPath, null, addSourceInfo);
                var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

                var mergeResult = await Geoprocessing.ExecuteToolAsync(
                    "Merge_management",
                    mergeParams,
                    environments,
                    _cancellationTokenSource.Token);

                if (mergeResult.IsFailed)
                {
                    throw new InvalidOperationException(GetGpMessageText(mergeResult));
                }

                Progress = 80;

                if (AddSourceFileField)
                {
                    await CreateSourceFileNameFieldAsync(outputInfo.CatalogPath, _cancellationTokenSource.Token);
                }

                Progress = 100;
                AppendInfo($"合并完成: {outputInfo.CatalogPath}");
            }
            catch (OperationCanceledException)
            {
                AppendWarning("操作已取消。");
            }
            catch (Exception ex)
            {
                AppendError($"合并失败: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
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

        private async Task CreateSourceFileNameFieldAsync(string outputCatalogPath, CancellationToken token)
        {
            var fieldNames = await GetFieldNameSetAsync(outputCatalogPath);
            if (!fieldNames.Contains("MERGE_SRC"))
            {
                AppendWarning("未找到 MERGE_SRC 字段，跳过“源文件名”字段创建。");
                return;
            }

            bool isShapefile = outputCatalogPath.EndsWith(".shp", StringComparison.OrdinalIgnoreCase);
            var newFieldName = BuildAvailableFieldName(fieldNames, isShapefile ? "SRC_FILE" : "SourceFile", isShapefile ? 10 : 64);

            AppendInfo($"创建源文件名字段: {newFieldName}");
            var addFieldParams = Geoprocessing.MakeValueArray(outputCatalogPath, newFieldName, "TEXT", null, null, 254, "源文件名");
            var addFieldResult = await Geoprocessing.ExecuteToolAsync("AddField_management", addFieldParams, null, token);
            if (addFieldResult.IsFailed)
            {
                AppendWarning($"创建源文件名字段失败: {GetGpMessageText(addFieldResult)}");
                return;
            }

            var expression = "GetName(!MERGE_SRC!)";
            var codeBlock = @"import os
def GetName(path):
    if path is None:
        return None
    text = str(path).strip()
    if not text:
        return None
    text = text.split(';')[0]
    base = os.path.basename(text)
    name, _ = os.path.splitext(base)
    return name";
            var calcFieldParams = Geoprocessing.MakeValueArray(outputCatalogPath, newFieldName, expression, "PYTHON3", codeBlock);
            var calcFieldResult = await Geoprocessing.ExecuteToolAsync("CalculateField_management", calcFieldParams, null, token);
            if (calcFieldResult.IsFailed)
            {
                AppendWarning($"写入源文件名字段失败: {GetGpMessageText(calcFieldResult)}");
                return;
            }

            AppendInfo("源文件名字段创建并写入完成。");
        }

        private static List<string> SafeEnumerateShpFiles(string rootPath, bool includeSubfolders)
        {
            var results = new List<string>();
            var pending = new Stack<string>();
            pending.Push(rootPath);

            while (pending.Count > 0)
            {
                var current = pending.Pop();
                try
                {
                    results.AddRange(Directory.EnumerateFiles(current, "*.shp", SearchOption.TopDirectoryOnly));
                }
                catch
                {
                    // Ignore inaccessible folders and continue scanning.
                }

                if (!includeSubfolders)
                {
                    continue;
                }

                try
                {
                    foreach (var subDirectory in Directory.EnumerateDirectories(current))
                    {
                        pending.Push(subDirectory);
                    }
                }
                catch
                {
                    // Ignore inaccessible folders and continue scanning.
                }
            }

            return results;
        }

        private void ReplaceItems(IEnumerable<string> shpFiles)
        {
            foreach (var item in ShpItems)
            {
                item.PropertyChanged -= OnItemPropertyChanged;
            }

            ShpItems.Clear();
            foreach (var shpPath in shpFiles)
            {
                var item = new ShpMergeItem
                {
                    IsSelected = true,
                    Name = Path.GetFileNameWithoutExtension(shpPath),
                    Directory = Path.GetDirectoryName(shpPath) ?? string.Empty,
                    FullPath = shpPath
                };
                item.GeometryKind = GetGeometryKind(shpPath);
                item.GeometryType = GetGeometryDisplayName(item.GeometryKind);

                item.PropertyChanged += OnItemPropertyChanged;
                ShpItems.Add(item);
            }

            UpdateSelectionSummary();
            NotifyStatePropertiesChanged();
            RaiseCommandCanExecuteChanged();
        }

        private void ClearItems()
        {
            foreach (var item in ShpItems)
            {
                item.PropertyChanged -= OnItemPropertyChanged;
            }

            ShpItems.Clear();
            UpdateSelectionSummary();
            NotifyStatePropertiesChanged();
            RaiseCommandCanExecuteChanged();
        }

        private void OnItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!string.Equals(e.PropertyName, nameof(ShpMergeItem.IsSelected), StringComparison.Ordinal))
            {
                return;
            }

            UpdateSelectionSummary();
            NotifyStatePropertiesChanged();
            RaiseCommandCanExecuteChanged();
        }

        private void UpdateSelectionSummary()
        {
            var total = ShpItems.Count;
            var selected = ShpItems.Count(item => item.IsSelected);
            SelectionSummary = $"已选择 {selected} / {total}";
        }

        private bool HasValidGeometrySelection()
        {
            var selected = ShpItems.Where(item => item.IsSelected).ToList();
            if (selected.Count == 0)
            {
                return false;
            }

            if (selected.Any(item => string.Equals(item.GeometryKind, "Unknown", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            return selected
                .Select(item => item.GeometryKind)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() == 1;
        }

        private static string GetGeometryKind(string shpPath)
        {
            try
            {
                using var stream = new FileStream(shpPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (stream.Length < 36)
                {
                    return "Unknown";
                }

                stream.Seek(32, SeekOrigin.Begin);
                var shapeTypeBuffer = new byte[4];
                var bytesRead = stream.Read(shapeTypeBuffer, 0, shapeTypeBuffer.Length);
                if (bytesRead != shapeTypeBuffer.Length)
                {
                    return "Unknown";
                }

                int shapeType = BitConverter.ToInt32(shapeTypeBuffer, 0);
                return shapeType switch
                {
                    1 or 11 or 21 => "Point",
                    3 or 13 or 23 => "Polyline",
                    5 or 15 or 25 => "Polygon",
                    8 or 18 or 28 => "Multipoint",
                    31 => "Multipatch",
                    _ => "Unknown"
                };
            }
            catch
            {
                return "Unknown";
            }
        }

        private static string GetGeometryDisplayName(string geometryKind)
        {
            return geometryKind switch
            {
                "Point" => "点",
                "Polyline" => "线",
                "Polygon" => "面",
                "Multipoint" => "多点",
                "Multipatch" => "多面体",
                _ => "未知"
            };
        }

        private async Task<HashSet<string>> GetFieldNameSetAsync(string catalogPath)
        {
            return await QueuedTask.Run(() =>
            {
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (catalogPath.EndsWith(".shp", StringComparison.OrdinalIgnoreCase))
                {
                    var folder = Path.GetDirectoryName(catalogPath) ?? string.Empty;
                    var shpName = Path.GetFileName(catalogPath);
                    var connectionPath = new FileSystemConnectionPath(new Uri(folder), FileSystemDatastoreType.Shapefile);
                    using var datastore = new FileSystemDatastore(connectionPath);
                    using var featureClass = datastore.OpenDataset<FeatureClass>(shpName);
                    foreach (var field in featureClass.GetDefinition().GetFields())
                    {
                        names.Add(field.Name);
                    }

                    return names;
                }

                int gdbIndex = catalogPath.IndexOf(".gdb", StringComparison.OrdinalIgnoreCase);
                if (gdbIndex < 0)
                {
                    return names;
                }

                var gdbRoot = catalogPath.Substring(0, gdbIndex + 4);
                var relativePath = catalogPath.Substring(gdbIndex + 4).TrimStart('\\', '/').Replace('/', '\\');
                using var geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(gdbRoot)));
                using var featureClassInGdb = geodatabase.OpenDataset<FeatureClass>(relativePath);
                foreach (var field in featureClassInGdb.GetDefinition().GetFields())
                {
                    names.Add(field.Name);
                }

                return names;
            });
        }

        private static string BuildAvailableFieldName(ISet<string> existingFieldNames, string baseName, int maxLength)
        {
            var normalizedBase = new string((baseName ?? string.Empty)
                .Where(ch => char.IsLetterOrDigit(ch) || ch == '_')
                .ToArray());

            if (string.IsNullOrWhiteSpace(normalizedBase))
            {
                normalizedBase = "SRC_FILE";
            }

            if (normalizedBase.Length > maxLength)
            {
                normalizedBase = normalizedBase.Substring(0, maxLength);
            }

            var candidate = normalizedBase;
            int suffix = 1;
            while (existingFieldNames.Contains(candidate))
            {
                var suffixText = "_" + suffix;
                int prefixLength = Math.Max(1, maxLength - suffixText.Length);
                var prefix = normalizedBase.Length > prefixLength
                    ? normalizedBase.Substring(0, prefixLength)
                    : normalizedBase;
                candidate = prefix + suffixText;
                suffix++;
            }

            return candidate;
        }

        private static string GetGpMessageText(IGPResult result)
        {
            try
            {
                if (result?.Messages != null && result.Messages.Any())
                {
                    return string.Join("; ", result.Messages);
                }
            }
            catch
            {
                // ignored
            }

            return "未知错误";
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

        private void NotifyStatePropertiesChanged()
        {
            NotifyPropertyChanged(() => CanRun);
            NotifyPropertyChanged(() => CanRefresh);
        }

        private void RaiseCommandCanExecuteChanged()
        {
            _browseInputFolderCommand.RaiseCanExecuteChanged();
            _refreshShpListCommand.RaiseCanExecuteChanged();
            _browseOutputPathCommand.RaiseCanExecuteChanged();
            _selectAllCommand.RaiseCanExecuteChanged();
            _invertSelectionCommand.RaiseCanExecuteChanged();
            _clearSelectionCommand.RaiseCanExecuteChanged();
            _selectPointCommand.RaiseCanExecuteChanged();
            _selectLineCommand.RaiseCanExecuteChanged();
            _selectPolygonCommand.RaiseCanExecuteChanged();
            _runCommand.RaiseCanExecuteChanged();
            _cancelCommand.RaiseCanExecuteChanged();
        }

        private void ShowHelp()
        {
            const string helpText =
                "批量合并 SHP 工具说明\n\n" +
                "1. 选择输入文件夹后会自动扫描并列出所有 SHP。\n" +
                "2. 可勾选“读取子文件夹”决定是否递归遍历。\n" +
                "3. 列表默认全选，可手动全选/反选/清空。\n" +
                "4. 合并使用 Merge 工具，自动进行字段并集。\n" +
                "5. 勾选“创建源文件名字段”时，会基于 MERGE_SRC 写入源文件名。\n" +
                "6. 输出位置支持 GDB 要素类或 Shapefile。\n\n" +
                "注意：输入 SHP 的几何类型需要一致。";

            MessageBox.Show(helpText, "批量合并 SHP 工具说明", MessageBoxButton.OK, MessageBoxImage.Information);
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
            var line = $"[{DateTime.Now:HH:mm:ss}] {message}\n";
            if (Application.Current?.Dispatcher?.CheckAccess() == true)
            {
                LogText += line;
            }
            else
            {
                Application.Current?.Dispatcher?.Invoke(() => LogText += line);
            }
        }
    }
}
