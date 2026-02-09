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
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Tools.DataPivot
{
    public class PivotInputDataset
    {
        public FeatureLayer FeatureLayer { get; set; }

        public StandaloneTable StandaloneTable { get; set; }

        public string DisplayName { get; set; }

        public string Name => FeatureLayer?.Name ?? StandaloneTable?.Name ?? string.Empty;

        public override string ToString() => DisplayName ?? Name;

        public object ToGeoprocessingInput()
        {
            return (object)FeatureLayer ?? StandaloneTable;
        }

        public Table OpenTable()
        {
            if (FeatureLayer != null)
                return FeatureLayer.GetTable();

            return StandaloneTable?.GetTable();
        }
    }

    public class PivotFieldOption : PropertyChangedBase
    {
        private bool _isSelected;

        public string FieldName { get; set; }

        public string Alias { get; set; }

        public FieldType FieldType { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public string DisplayText => string.IsNullOrWhiteSpace(Alias) || Alias == FieldName
            ? $"{FieldName}（{GetFieldTypeText(FieldType)}）"
            : $"{FieldName}（{Alias}）（{GetFieldTypeText(FieldType)}）";

        public override string ToString() => DisplayText;

        private static string GetFieldTypeText(FieldType fieldType)
        {
            return fieldType switch
            {
                FieldType.Double => "双精度",
                FieldType.Single => "单精度",
                FieldType.Integer => "整型",
                FieldType.SmallInteger => "短整型",
                FieldType.BigInteger => "长整型",
                FieldType.String => "文本",
                FieldType.Date => "日期",
                FieldType.DateOnly => "仅日期",
                FieldType.TimeOnly => "仅时间",
                FieldType.TimestampOffset => "时间戳偏移",
                FieldType.GUID => "GUID",
                FieldType.GlobalID => "GlobalID",
                FieldType.OID => "OID",
                _ => fieldType.ToString()
            };
        }
    }

    internal class DataPivotDockPaneViewModel : PropertyChangedBase
    {
        private const string TempPivotField = "XFT_PIVOT_KEY";
        private const string TempValueField = "XFT_PIVOT_VALUE";

        private CancellationTokenSource _cts;

        private bool _isProcessing;
        private int _progress;
        private bool _isProgressIndeterminate;
        private string _statusMessage = "请选择输入表和透视参数。";
        private string _logContent = string.Empty;
        private PivotInputDataset _selectedInputDataset;
        private PivotFieldOption _selectedPivotField;
        private PivotFieldOption _selectedValueField;
        private string _selectedAggregationType;
        private string _outputGdbPath;
        private string _outputTableName = "TS_结果";

        public ObservableCollection<PivotInputDataset> InputDatasets { get; } = new ObservableCollection<PivotInputDataset>();

        public ObservableCollection<PivotFieldOption> RegionFields { get; } = new ObservableCollection<PivotFieldOption>();

        public ObservableCollection<PivotFieldOption> PivotFields { get; } = new ObservableCollection<PivotFieldOption>();

        public ObservableCollection<PivotFieldOption> ValueFields { get; } = new ObservableCollection<PivotFieldOption>();

        public ObservableCollection<string> AggregationTypes { get; } = new ObservableCollection<string>
        {
            "求和",
            "计数",
            "平均值",
            "最大值",
            "最小值",
            "中位数",
            "极差",
            "标准差",
            "方差"
        };

        public PivotInputDataset SelectedInputDataset
        {
            get => _selectedInputDataset;
            set
            {
                if (SetProperty(ref _selectedInputDataset, value))
                {
                    OutputTableName = BuildDefaultOutputTableName(value?.Name);
                    NotifyPropertyChanged(() => CanProcess);
                    LoadFieldOptions();
                }
            }
        }

        public PivotFieldOption SelectedPivotField
        {
            get => _selectedPivotField;
            set
            {
                if (SetProperty(ref _selectedPivotField, value))
                    NotifyPropertyChanged(() => CanProcess);
            }
        }

        public PivotFieldOption SelectedValueField
        {
            get => _selectedValueField;
            set
            {
                if (SetProperty(ref _selectedValueField, value))
                    NotifyPropertyChanged(() => CanProcess);
            }
        }

        public string SelectedAggregationType
        {
            get => _selectedAggregationType;
            set
            {
                if (SetProperty(ref _selectedAggregationType, value))
                    NotifyPropertyChanged(() => CanProcess);
            }
        }

        public string OutputGdbPath
        {
            get => _outputGdbPath;
            set
            {
                if (SetProperty(ref _outputGdbPath, value))
                {
                    NotifyPropertyChanged(() => OutputTablePath);
                    NotifyPropertyChanged(() => CanProcess);
                }
            }
        }

        public string OutputTableName
        {
            get => _outputTableName;
            set
            {
                if (SetProperty(ref _outputTableName, value))
                {
                    NotifyPropertyChanged(() => OutputTablePath);
                    NotifyPropertyChanged(() => CanProcess);
                }
            }
        }

        public string OutputTablePath
        {
            get
            {
                if (string.IsNullOrWhiteSpace(OutputGdbPath) || string.IsNullOrWhiteSpace(OutputTableName))
                    return string.Empty;

                return Path.Combine(OutputGdbPath.Trim(), OutputTableName.Trim());
            }
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                if (SetProperty(ref _isProcessing, value))
                    NotifyPropertyChanged(() => CanProcess);
            }
        }

        public int Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        public bool IsProgressIndeterminate
        {
            get => _isProgressIndeterminate;
            set => SetProperty(ref _isProgressIndeterminate, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public string LogContent
        {
            get => _logContent;
            set => SetProperty(ref _logContent, value);
        }

        public bool CanProcess =>
            !IsProcessing &&
            SelectedInputDataset != null &&
            SelectedPivotField != null &&
            SelectedValueField != null &&
            !string.IsNullOrWhiteSpace(SelectedAggregationType) &&
            RegionFields.Any(f => f.IsSelected) &&
            !string.IsNullOrWhiteSpace(OutputGdbPath) &&
            !string.IsNullOrWhiteSpace(OutputTableName);

        private ICommand _runCommand;
        public ICommand RunCommand => _runCommand ??= new RelayCommand(async () => await ExecuteAsync(), () => CanProcess);

        private ICommand _cancelCommand;
        public ICommand CancelCommand => _cancelCommand ??= new RelayCommand(Cancel, () => IsProcessing);

        private ICommand _refreshDatasetsCommand;
        public ICommand RefreshDatasetsCommand => _refreshDatasetsCommand ??= new RelayCommand(RefreshDatasets, () => !IsProcessing);

        private ICommand _browseOutputGdbCommand;
        public ICommand BrowseOutputGdbCommand => _browseOutputGdbCommand ??= new RelayCommand(BrowseOutputGdb, () => !IsProcessing);

        private ICommand _showHelpCommand;
        public ICommand ShowHelpCommand => _showHelpCommand ??= new RelayCommand(ShowHelp);

        public DataPivotDockPaneViewModel()
        {
            SelectedAggregationType = AggregationTypes[0];
            OutputGdbPath = GetProjectDefaultGdbPath();
            RefreshDatasets();
        }

        public void ResetOutputToProjectDefaultGdb()
        {
            var defaultGdb = GetProjectDefaultGdbPath();
            if (!string.IsNullOrWhiteSpace(defaultGdb))
                OutputGdbPath = defaultGdb;
        }

        public void RefreshDatasets()
        {
            LoadInputDatasets();
        }

        private async void LoadInputDatasets()
        {
            try
            {
                var datasets = await QueuedTask.Run(() =>
                {
                    var items = new List<PivotInputDataset>();
                    var map = MapView.Active?.Map;
                    if (map == null)
                        return items;

                    foreach (var layer in map.GetLayersAsFlattenedList().OfType<FeatureLayer>())
                    {
                        try
                        {
                            using var table = layer.GetTable();
                            if (table != null)
                            {
                                items.Add(new PivotInputDataset
                                {
                                    FeatureLayer = layer,
                                    DisplayName = $"{layer.Name}（要素图层）"
                                });
                            }
                        }
                        catch
                        {
                            // 忽略不可访问图层
                        }
                    }

                    foreach (var standaloneTable in map.GetStandaloneTablesAsFlattenedList())
                    {
                        try
                        {
                            using var table = standaloneTable.GetTable();
                            if (table != null)
                            {
                                items.Add(new PivotInputDataset
                                {
                                    StandaloneTable = standaloneTable,
                                    DisplayName = $"{standaloneTable.Name}（独立表）"
                                });
                            }
                        }
                        catch
                        {
                            // 忽略不可访问独立表
                        }
                    }

                    return items;
                });

                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    InputDatasets.Clear();
                    foreach (var item in datasets)
                        InputDatasets.Add(item);

                    if (InputDatasets.Count > 0)
                    {
                        SelectedInputDataset = InputDatasets[0];
                        LogInfo($"已加载 {InputDatasets.Count} 个可用输入表。");
                    }
                    else
                    {
                        SelectedInputDataset = null;
                        ClearFieldCollections();
                        LogWarning("未在当前地图中找到可用图层或独立表。");
                    }

                    StatusMessage = "请选择输入表和透视参数。";
                });
            }
            catch (Exception ex)
            {
                LogError($"加载输入表失败: {ex.Message}");
            }
        }

        private async void LoadFieldOptions()
        {
            try
            {
                if (SelectedInputDataset == null)
                {
                    ClearFieldCollections();
                    return;
                }

                var fields = await QueuedTask.Run(() =>
                {
                    var list = new List<PivotFieldOption>();
                    using var table = SelectedInputDataset.OpenTable();
                    if (table == null)
                        return list;

                    var definition = table.GetDefinition();
                    foreach (var field in definition.GetFields())
                    {
                        if (IsUnsupportedField(field.FieldType))
                            continue;

                        list.Add(new PivotFieldOption
                        {
                            FieldName = field.Name,
                            Alias = field.AliasName,
                            FieldType = field.FieldType
                        });
                    }

                    return list;
                });

                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    ClearFieldCollections();

                    foreach (var field in fields)
                    {
                        var regionField = CloneField(field);
                        regionField.PropertyChanged += RegionFieldOnPropertyChanged;
                        RegionFields.Add(regionField);

                        PivotFields.Add(CloneField(field));

                        if (IsValueFieldSupported(field.FieldType))
                            ValueFields.Add(CloneField(field));
                    }

                    if (RegionFields.Count > 0)
                        RegionFields[0].IsSelected = true;

                    SelectedPivotField = PivotFields.FirstOrDefault();
                    SelectedValueField = ValueFields.FirstOrDefault(IsNumericField) ?? ValueFields.FirstOrDefault();

                    NotifyPropertyChanged(() => CanProcess);
                });
            }
            catch (Exception ex)
            {
                LogError($"加载字段失败: {ex.Message}");
            }
        }

        private static PivotFieldOption CloneField(PivotFieldOption source)
        {
            return new PivotFieldOption
            {
                FieldName = source.FieldName,
                Alias = source.Alias,
                FieldType = source.FieldType
            };
        }

        private void ClearFieldCollections()
        {
            foreach (var field in RegionFields)
                field.PropertyChanged -= RegionFieldOnPropertyChanged;

            RegionFields.Clear();
            PivotFields.Clear();
            ValueFields.Clear();
            SelectedPivotField = null;
            SelectedValueField = null;
        }

        private void RegionFieldOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PivotFieldOption.IsSelected))
                NotifyPropertyChanged(() => CanProcess);
        }

        private static bool IsUnsupportedField(FieldType fieldType)
        {
            return fieldType == FieldType.Geometry ||
                   fieldType == FieldType.Blob ||
                   fieldType == FieldType.Raster ||
                   fieldType == FieldType.XML;
        }

        private static bool IsValueFieldSupported(FieldType fieldType)
        {
            return fieldType != FieldType.Geometry &&
                   fieldType != FieldType.Blob &&
                   fieldType != FieldType.Raster &&
                   fieldType != FieldType.XML &&
                   fieldType != FieldType.OID &&
                   fieldType != FieldType.GlobalID;
        }

        private static bool IsNumericField(PivotFieldOption option)
        {
            if (option == null)
                return false;

            return option.FieldType == FieldType.Double ||
                   option.FieldType == FieldType.Single ||
                   option.FieldType == FieldType.Integer ||
                   option.FieldType == FieldType.SmallInteger ||
                   option.FieldType == FieldType.BigInteger;
        }

        private static string GetProjectDefaultGdbPath()
        {
            return Project.Current?.DefaultGeodatabasePath ?? string.Empty;
        }

        private static string BuildDefaultOutputTableName(string inputName)
        {
            var name = string.IsNullOrWhiteSpace(inputName) ? "结果" : inputName.Trim();
            return $"TS_{name}";
        }

        private string ResolveTemporaryWorkspacePath()
        {
            if (!string.IsNullOrWhiteSpace(OutputGdbPath) &&
                OutputGdbPath.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase) &&
                Directory.Exists(OutputGdbPath))
            {
                return OutputGdbPath;
            }

            var projectDefaultGdb = GetProjectDefaultGdbPath();
            if (!string.IsNullOrWhiteSpace(projectDefaultGdb) &&
                projectDefaultGdb.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase) &&
                Directory.Exists(projectDefaultGdb))
            {
                return projectDefaultGdb;
            }

            return OutputGdbPath;
        }

        private async Task ExecuteAsync()
        {
            if (!CanProcess)
            {
                StatusMessage = "请先完善输入参数。";
                return;
            }

            string tempTable = null;
            string summaryTable = null;

            try
            {
                IsProcessing = true;
                IsProgressIndeterminate = false;
                Progress = 0;
                StatusMessage = "正在执行数据透视...";
                LogContent = string.Empty;

                _cts?.Dispose();
                _cts = new CancellationTokenSource();

                var regionFields = RegionFields.Where(f => f.IsSelected).Select(f => f.FieldName).ToList();
                var regionFieldsText = string.Join(";", regionFields);
                var statsType = GetStatisticsType(SelectedAggregationType);
                var statsOutputField = $"{statsType}_{TempValueField}";
                var caseFields = string.Join(";", regionFields.Concat(new[] { TempPivotField }));
                var outputPath = OutputTablePath;
                var tempWorkspace = ResolveTemporaryWorkspacePath();

                if (string.IsNullOrWhiteSpace(tempWorkspace) || !Directory.Exists(tempWorkspace))
                {
                    LogError("临时工作空间不可用，请检查输出数据库或项目默认数据库。");
                    return;
                }

                var runId = DateTime.Now.ToString("yyyyMMddHHmmssfff");

                tempTable = Path.Combine(tempWorkspace, $"XFT_PivotTemp_{runId}");
                summaryTable = Path.Combine(tempWorkspace, $"XFT_PivotSummary_{runId}");

                LogInfo($"输入表: {SelectedInputDataset.Name}");
                LogInfo($"区域字段: {regionFieldsText}");
                LogInfo($"透视字段: {SelectedPivotField.FieldName}");
                LogInfo($"数值字段: {SelectedValueField.FieldName}");
                LogInfo($"汇总方式: {SelectedAggregationType}");
                LogInfo($"输出表: {outputPath}");
                LogInfo($"临时工作空间: {tempWorkspace}");

                var envNoMap = Geoprocessing.MakeEnvironmentArray("overwriteoutput", "True", "addOutputsToMap", "False");
                var envAddToMap = Geoprocessing.MakeEnvironmentArray("overwriteoutput", "True", "addOutputsToMap", "True");

                Progress = 10;
                StatusMessage = "正在复制输入表...";
                var copyRowsParams = Geoprocessing.MakeValueArray(SelectedInputDataset.ToGeoprocessingInput(), tempTable);
                var copyRowsResult = await Geoprocessing.ExecuteToolAsync("management.CopyRows", copyRowsParams, envNoMap, _cts.Token);
                if (!HandleGpResult(copyRowsResult, "复制输入表失败"))
                    return;

                Progress = 25;
                StatusMessage = "正在准备透视字段...";
                var addPivotFieldParams = Geoprocessing.MakeValueArray(tempTable, TempPivotField, "TEXT", null, null, 255, "透视字段");
                var addPivotFieldResult = await Geoprocessing.ExecuteToolAsync("management.AddField", addPivotFieldParams, envNoMap, _cts.Token);
                if (!HandleGpResult(addPivotFieldResult, "创建临时透视字段失败"))
                    return;

                var pivotCodeBlock =
                    "def pivot_key(value):\n" +
                    "    if value is None:\n" +
                    "        return \"空值\"\n" +
                    "    text = str(value).strip()\n" +
                    "    return text if text != \"\" else \"空值\"\n";
                var calcPivotFieldParams = Geoprocessing.MakeValueArray(
                    tempTable,
                    TempPivotField,
                    $"pivot_key(!{SelectedPivotField.FieldName}!)",
                    "PYTHON3",
                    pivotCodeBlock);
                var calcPivotFieldResult = await Geoprocessing.ExecuteToolAsync("management.CalculateField", calcPivotFieldParams, envNoMap, _cts.Token);
                if (!HandleGpResult(calcPivotFieldResult, "计算透视字段失败"))
                    return;

                Progress = 45;
                StatusMessage = "正在准备数值字段...";
                var addValueFieldParams = Geoprocessing.MakeValueArray(tempTable, TempValueField, "DOUBLE", null, null, null, "透视数值");
                var addValueFieldResult = await Geoprocessing.ExecuteToolAsync("management.AddField", addValueFieldParams, envNoMap, _cts.Token);
                if (!HandleGpResult(addValueFieldResult, "创建临时数值字段失败"))
                    return;

                var valueCodeBlock =
                    "def to_num(value):\n" +
                    "    try:\n" +
                    "        if value is None:\n" +
                    "            return 0\n" +
                    "        text = str(value).strip()\n" +
                    "        if text == \"\":\n" +
                    "            return 0\n" +
                    "        return float(text)\n" +
                    "    except:\n" +
                    "        return 0\n";
                var calcValueFieldParams = Geoprocessing.MakeValueArray(
                    tempTable,
                    TempValueField,
                    $"to_num(!{SelectedValueField.FieldName}!)",
                    "PYTHON3",
                    valueCodeBlock);
                var calcValueFieldResult = await Geoprocessing.ExecuteToolAsync("management.CalculateField", calcValueFieldParams, envNoMap, _cts.Token);
                if (!HandleGpResult(calcValueFieldResult, "计算数值字段失败"))
                    return;

                Progress = 65;
                StatusMessage = "正在汇总统计...";
                var statisticsParams = Geoprocessing.MakeValueArray(tempTable, summaryTable, $"{TempValueField} {statsType}", caseFields);
                var statisticsResult = await Geoprocessing.ExecuteToolAsync("analysis.Statistics", statisticsParams, envNoMap, _cts.Token);
                if (!HandleGpResult(statisticsResult, "统计汇总失败"))
                    return;

                Progress = 85;
                StatusMessage = "正在生成透视表...";
                var pivotTableParams = Geoprocessing.MakeValueArray(summaryTable, regionFieldsText, TempPivotField, statsOutputField, outputPath);
                var pivotTableResult = await Geoprocessing.ExecuteToolAsync("management.PivotTable", pivotTableParams, envAddToMap, _cts.Token);
                if (!HandleGpResult(pivotTableResult, "创建透视表失败"))
                    return;

                Progress = 100;
                StatusMessage = "数据透视完成。";
                LogInfo("数据透视执行成功。");
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "操作已取消。";
                LogWarning("用户取消了数据透视操作。");
            }
            catch (Exception ex)
            {
                LogError($"执行失败: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
                IsProgressIndeterminate = false;
                await DeleteTempTableAsync(summaryTable);
                await DeleteTempTableAsync(tempTable);

                _cts?.Dispose();
                _cts = null;
            }
        }

        private bool HandleGpResult(IGPResult result, string failedMessage)
        {
            if (result == null)
            {
                LogError(failedMessage);
                return false;
            }

            foreach (var message in result.Messages)
            {
                if (!string.IsNullOrWhiteSpace(message.Text))
                    LogInfo(message.Text);
            }

            if (result.IsFailed)
            {
                LogError(failedMessage);
                return false;
            }

            return true;
        }

        private async Task DeleteTempTableAsync(string tablePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(tablePath))
                    return;

                var deleteParams = Geoprocessing.MakeValueArray(tablePath);
                await Geoprocessing.ExecuteToolAsync("management.Delete", deleteParams);
            }
            catch
            {
                // 清理失败可忽略
            }
        }

        private static string GetStatisticsType(string aggregationType)
        {
            return aggregationType switch
            {
                "求和" => "SUM",
                "计数" => "COUNT",
                "平均值" => "MEAN",
                "最大值" => "MAX",
                "最小值" => "MIN",
                "中位数" => "MEDIAN",
                "极差" => "RANGE",
                "标准差" => "STD",
                "方差" => "VARIANCE",
                _ => "SUM"
            };
        }

        private void BrowseOutputGdb()
        {
            try
            {
                var dialog = new OpenItemDialog
                {
                    Title = "选择输出地理数据库",
                    MultiSelect = false,
                    Filter = ItemFilters.Geodatabases,
                    InitialLocation = string.IsNullOrWhiteSpace(OutputGdbPath) ? Project.Current?.HomeFolderPath : OutputGdbPath
                };

                if (dialog.ShowDialog() == true && dialog.Items.Any())
                {
                    OutputGdbPath = dialog.Items.First().Path;
                    LogInfo($"输出数据库: {OutputGdbPath}");
                }
            }
            catch (Exception ex)
            {
                LogError($"选择输出数据库失败: {ex.Message}");
            }
        }

        private void Cancel()
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
                StatusMessage = "正在取消，请稍候...";
            }
        }

        private void ShowHelp()
        {
            var help =
                "数据透视工具使用说明\n\n" +
                "功能：\n" +
                "将输入表按区域字段分组后，对透视字段展开列，并对数值字段进行汇总统计。\n\n" +
                "参数说明：\n" +
                "1. 输入表：选择地图中的要素图层或独立表。\n" +
                "2. 区域字段：分组字段，可多选。\n" +
                "3. 透视字段：用于展开列。空值会自动归并为“空值”。\n" +
                "4. 数值字段：用于汇总计算，非数值内容会按 0 处理。\n" +
                "5. 汇总方式：支持求和、计数、平均值、最大值、最小值、中位数、极差、标准差、方差。\n" +
                "6. 输出设置：选择输出 GDB 和输出表名。\n\n" +
                "操作步骤：\n" +
                "1) 选择输入表；\n" +
                "2) 勾选区域字段并设置透视字段/数值字段；\n" +
                "3) 选择汇总方式和输出位置；\n" +
                "4) 点击“开始”执行。";

            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(help, "数据透视帮助");
        }

        private void LogInfo(string message)
        {
            AppendLog(message);
        }

        private void LogWarning(string message)
        {
            AppendLog($"警告: {message}");
        }

        private void LogError(string message)
        {
            AppendLog($"错误: {message}");
            StatusMessage = message;
        }

        private void AppendLog(string message)
        {
            var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                LogContent += line + Environment.NewLine;
            });
        }
    }
}
