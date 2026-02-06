using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Text;
using System.Globalization;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Core.Geoprocessing;

namespace XIAOFUTools.Tools.BatchLayerClip
{
    /// <summary>
    /// 按字段批量裁剪要素图层视图模型
    /// </summary>
    internal class BatchLayerClipViewModel : PropertyChangedBase
    {
        #region 属性

        // 取消操作标志
        private bool _cancelRequested = false;
        public bool CancelRequested
        {
            get => _cancelRequested;
            set
            {
                SetProperty(ref _cancelRequested, value);
            }
        }
        
        // 是否正在处理
        private bool _isProcessing = false;
        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                SetProperty(ref _isProcessing, value);
                NotifyPropertyChanged(() => CanProcess);
            }
        }
        
        // 是否可以处理
        public bool CanProcess => !IsProcessing;

        // 要素图层列表
        private ObservableCollection<FeatureLayer> _featureLayers;
        public ObservableCollection<FeatureLayer> FeatureLayers
        {
            get => _featureLayers;
            set
            {
                SetProperty(ref _featureLayers, value);
            }
        }

        // 选中的要素图层
        private FeatureLayer _selectedFeatureLayer;
        public FeatureLayer SelectedFeatureLayer
        {
            get => _selectedFeatureLayer;
            set
            {
                SetProperty(ref _selectedFeatureLayer, value);
                UpdateFieldNames();
                NotifyPropertyChanged(() => HasSelectedLayer);
            }
        }

        // 是否有选中图层
        public bool HasSelectedLayer => SelectedFeatureLayer != null;

        // 字段名称列表
        private ObservableCollection<string> _fieldNames;
        public ObservableCollection<string> FieldNames
        {
            get => _fieldNames;
            set
            {
                SetProperty(ref _fieldNames, value);
            }
        }

        // 选中的字段
        private string _selectedField;
        public string SelectedField
        {
            get => _selectedField;
            set
            {
                SetProperty(ref _selectedField, value);
            }
        }

        // 输出文件夹
        private string _outputFolder;
        public string OutputFolder
        {
            get => _outputFolder;
            set
            {
                SetProperty(ref _outputFolder, value);
            }
        }

        // 是否创建子文件夹
        private bool _createSubFolder;
        public bool CreateSubFolder
        {
            get => _createSubFolder;
            set
            {
                SetProperty(ref _createSubFolder, value);
            }
        }

        // 状态信息
        private string _statusMessage;
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                SetProperty(ref _statusMessage, value);
            }
        }

        // 日志内容
        private string _logContent;
        public string LogContent
        {
            get => _logContent;
            set
            {
                SetProperty(ref _logContent, value);
            }
        }

        // 日志构建器
        private StringBuilder _logBuilder = new StringBuilder();

        // 进度
        private int _progress;
        public int Progress
        {
            get => _progress;
            set
            {
                SetProperty(ref _progress, value);
            }
        }

        // 是否为不确定进度
        private bool _isProgressIndeterminate;
        public bool IsProgressIndeterminate
        {
            get => _isProgressIndeterminate;
            set
            {
                SetProperty(ref _isProgressIndeterminate, value);
            }
        }

        // 当前地理处理任务的取消源
        private CancellationTokenSource _geoprocessingCancellationSource;

        #endregion

        #region 命令

        // 浏览文件夹命令
        private ICommand _browseFolderCommand;
        public ICommand BrowseFolderCommand
        {
            get
            {
                return _browseFolderCommand ?? (_browseFolderCommand = new RelayCommand(() =>
                {
                    try
                    {
                        // OpenItemDialog必须在UI线程上创建和显示
                        // 使用ArcGIS Pro的OpenItemDialog选择文件夹
                        var initialLocation = Project.Current?.HomeFolderPath ?? OutputFolder;
                        if (!Directory.Exists(initialLocation))
                        {
                            initialLocation = GetProjectFolderPath();
                        }

                        // 创建并显示对话框（必须在UI线程进行）
                        var openItemDialog = new OpenItemDialog
                        {
                            Title = "选择输出文件夹",
                            InitialLocation = initialLocation,
                            MultiSelect = false,
                            Filter = ItemFilters.Folders // 仅允许选择文件夹
                        };

                        // 显示对话框并获取结果
                        bool? dialogResult = openItemDialog.ShowDialog();

                        if (dialogResult == true)
                        {
                            // 获取选中的文件夹
                            var selectedItem = openItemDialog.Items.FirstOrDefault();
                            if (selectedItem != null)
                            {
                                // 更新输出文件夹路径
                                OutputFolder = selectedItem.Path;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // 错误也必须在UI线程显示
                        MessageBox.Show($"选择文件夹出错: {ex.Message}", "错误");
                    }
                }));
            }
        }

        // 停止命令
        private ICommand _cancelCommand;
        public ICommand CancelCommand
        {
            get
            {
                return _cancelCommand ?? (_cancelCommand = new RelayCommand(() =>
                {
                    // 设置取消标志停止当前处理
                    CancelRequested = true;
                    StatusMessage = "正在取消操作...";
                    LogWarning("用户请求取消操作");
                    _geoprocessingCancellationSource?.Cancel();
                }, () => IsProcessing));
            }
        }

        // 运行命令
        private ICommand _runCommand;
        public ICommand RunCommand
        {
            get
            {
                return _runCommand ?? (_runCommand = new RelayCommand(Execute, () => CanExecute() && CanProcess));
            }
        }
        
        // 帮助命令
        private ICommand _showHelpCommand;
        public ICommand ShowHelpCommand
        {
            get
            {
                return _showHelpCommand ?? (_showHelpCommand = new RelayCommand(() => ShowHelp()));
            }
        }

        // 刷新图层命令
        private ICommand _refreshLayersCommand;
        public ICommand RefreshLayersCommand
        {
            get
            {
                return _refreshLayersCommand ?? (_refreshLayersCommand = new RelayCommand(() => RefreshLayers()));
            }
        }

        #endregion

        /// <summary>
        /// 构造函数
        /// </summary>
        public BatchLayerClipViewModel()
        {
            // 初始化属性
            FeatureLayers = new ObservableCollection<FeatureLayer>();
            FieldNames = new ObservableCollection<string>();
            
            // 设置输出文件夹为当前项目文件夹
            string projectFolder = GetProjectFolderPath();
            OutputFolder = projectFolder;
            
            CreateSubFolder = true;
            StatusMessage = "请选择要素图层及分组字段。";
            LogContent = "";
            Progress = 0;
            IsProgressIndeterminate = false;

            // 加载要素图层
            LoadFeatureLayers();
        }
        
        /// <summary>
        /// 获取当前项目文件夹路径
        /// </summary>
        private string GetProjectFolderPath()
        {
            string projectFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments); // 默认路径
            
            try
            {
                // 尝试获取当前项目路径
                var project = Project.Current;
                if (project != null && !string.IsNullOrEmpty(project.Path))
                {
                    // 获取项目文件夹路径
                    projectFolder = System.IO.Path.GetDirectoryName(project.Path);
                    
                    // 如果项目文件夹存在，使用此路径
                    if (Directory.Exists(projectFolder))
                    {
                        return projectFolder;
                    }
                }
                return projectFolder;
            }
            catch (Exception ex)
            {
                // 如果无法获取项目路径，使用默认文档路径
                System.Diagnostics.Debug.WriteLine($"获取项目路径失败: {ex.Message}");
                return projectFolder;
            }
        }
        
        /// <summary>
        /// 刷新图层列表（公共方法）
        /// </summary>
        public void RefreshLayers()
        {
            LoadFeatureLayers();
        }

        /// <summary>
        /// 加载要素图层
        /// </summary>
        private void LoadFeatureLayers()
        {
            QueuedTask.Run(() =>
            {
                try 
                {
                    // 获取所有图层的临时列表
                    var tempLayers = new List<FeatureLayer>();
                    var map = MapView.Active?.Map;
                    
                    if (map != null)
                    {
                        var layers = map.GetLayersAsFlattenedList().OfType<FeatureLayer>().ToList();
                        tempLayers.AddRange(layers);
                    }
                    
                    // 在UI线程更新图层列表
                    System.Windows.Application.Current.Dispatcher.Invoke(() => 
                    {
                        // 清空图层列表
                        FeatureLayers.Clear();
                        
                        // 添加图层
                        foreach (var layer in tempLayers)
                        {
                            FeatureLayers.Add(layer);
                        }
                        
                        // 如果有图层，默认选择第一个
                        if (FeatureLayers.Count > 0)
                        {
                            SelectedFeatureLayer = FeatureLayers[0];
                        }
                    });
                }
                catch (Exception ex)
                {
                    // 确保在UI线程显示错误信息
                    System.Windows.Application.Current.Dispatcher.Invoke(() => 
                    {
                        StatusMessage = $"加载图层出错: {ex.Message}";
                    });
                }
            });
        }

        /// <summary>
        /// 更新字段名称列表
        /// </summary>
        private void UpdateFieldNames()
        {
            // 在UI线程清空字段列表
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                FieldNames.Clear();
            });
            
            // 如果没有选择图层，直接返回
            if (SelectedFeatureLayer == null)
                return;
                
            // 使用QueuedTask在后台线程执行
            QueuedTask.Run(() =>
            {
                try
                {
                    // 创建临时列表存储字段
                    var tempFields = new List<string>();
                    
                    using (var table = SelectedFeatureLayer.GetTable())
                    {
                        var definition = table.GetDefinition();
                        var fields = definition.GetFields();
                        
                        foreach (var field in fields)
                        {
                            // 只添加文本、整数和双精度字段作为分组字段
                            if (field.FieldType == FieldType.String || 
                                field.FieldType == FieldType.Integer ||
                                field.FieldType == FieldType.SmallInteger ||
                                field.FieldType == FieldType.Double ||
                                field.FieldType == FieldType.Single)
                            {
                                // 格式化字段显示名称为"字段名称(别名)"
                                string displayName;
                                if (string.IsNullOrEmpty(field.AliasName) || field.Name.Equals(field.AliasName, StringComparison.OrdinalIgnoreCase))
                                {
                                    displayName = field.Name;
                                }
                                else
                                {
                                    displayName = $"{field.Name}({field.AliasName})";
                                }
                                tempFields.Add(displayName);
                            }
                        }
                    }
                    
                    // 在UI线程更新字段列表
                    System.Windows.Application.Current.Dispatcher.Invoke(() => 
                    {
                        // 将临时列表中的字段添加到字段名称列表
                        foreach (var field in tempFields)
                        {
                            FieldNames.Add(field);
                        }
                        
                        // 如果有字段，默认选择第一个
                        if (FieldNames.Count > 0)
                        {
                            SelectedField = FieldNames[0];
                        }
                    });
                }
                catch (Exception ex)
                {
                    // 确保在UI线程显示错误信息
                    System.Windows.Application.Current.Dispatcher.Invoke(() => 
                    {
                        StatusMessage = $"加载字段出错: {ex.Message}";
                        LogError($"加载字段出错: {ex.Message}");
                    });
                }
            });
        }

        /// <summary>
        /// 确定命令是否可以执行
        /// </summary>
        private bool CanExecute()
        {
            return SelectedFeatureLayer != null && 
                   !string.IsNullOrEmpty(SelectedField) && 
                   !string.IsNullOrEmpty(OutputFolder) && 
                   Directory.Exists(OutputFolder);
        }

        /// <summary>
        /// 执行批量裁剪操作
        /// </summary>
        private async void Execute()
        {
            if (SelectedFeatureLayer == null || string.IsNullOrWhiteSpace(SelectedField))
                return;

            CancelRequested = false;
            IsProcessing = true;
            StatusMessage = "正在处理...";
            ClearLog();
            Progress = 0;
            IsProgressIndeterminate = true;

            var layer = SelectedFeatureLayer;
            string fieldName = GetActualFieldName(SelectedField);

            try
            {
                LogInfo($"===== 开始批量裁剪操作 - {DateTime.Now} =====");
                LogInfo($"选择的图层: {layer.Name}");
                LogInfo($"选择的分组字段: {SelectedField}");
                LogInfo($"输出文件夹: {OutputFolder}");
                LogInfo($"是否创建子文件夹: {(CreateSubFolder ? "是" : "否")}");

                var preparationResult = await QueuedTask.Run(() => PrepareFieldGroups(layer, fieldName));
                LogInfo($"已生成 {preparationResult.Groups.Count} 个分组，准备导出...");

                var exportedCount = await ProcessGroupsAsync(layer, preparationResult);

                if (CancelRequested)
                {
                    LogWarning("操作已取消");
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        StatusMessage = "操作已取消";
                    });
                }
                else
                {
                    LogInfo($"===== 批量裁剪完成 - {DateTime.Now} =====");
                    LogInfo($"共导出 {exportedCount} 个分组至 {OutputFolder}");

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        Progress = 100;
                        StatusMessage = $"导出完成! 共导出 {exportedCount} 个分组。";
                    });
                }
            }
            catch (OperationCanceledException)
            {
                LogWarning("操作已取消");
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessage = "操作已取消";
                });
            }
            catch (Exception ex)
            {
                string errorMsg = $"处理出错: {ex.Message}";
                LogError(errorMsg);
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessage = errorMsg;
                    Progress = 0;
                });
            }
            finally
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    IsProcessing = false;
                    IsProgressIndeterminate = false;
                    if (CancelRequested && Progress < 100)
                    {
                        Progress = 0;
                    }
                });
            }
        }

        private string GetActualFieldName(string selectedField)
        {
            if (string.IsNullOrWhiteSpace(selectedField))
                throw new InvalidOperationException("请先选择分组字段。");

            int aliasIndex = selectedField.IndexOf("(", StringComparison.Ordinal);
            if (aliasIndex > 0)
            {
                string actual = selectedField.Substring(0, aliasIndex).Trim();
                if (!string.IsNullOrWhiteSpace(actual))
                    return actual;
            }

            return selectedField.Trim();
        }

        private GroupPreparationResult PrepareFieldGroups(FeatureLayer layer, string fieldName)
        {
            var result = new GroupPreparationResult { FieldName = fieldName };

            using (var table = layer.GetTable())
            {
                var definition = table.GetDefinition();
                var targetField = definition.GetFields().FirstOrDefault(f => f.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
                if (targetField == null)
                    throw new InvalidOperationException($"字段 {fieldName} 不存在。");

                result.FieldType = targetField.FieldType;

                var groups = new Dictionary<FieldGroupKey, FieldGroupInfo>();
                var nameUsage = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                using (var cursor = table.Search(null, false))
                {
                    while (cursor.MoveNext())
                    {
                        if (CancelRequested)
                            throw new OperationCanceledException();

                        using (var row = cursor.Current)
                        {
                            var rawValue = row[fieldName];
                            var key = new FieldGroupKey(rawValue, targetField.FieldType);

                            if (!groups.TryGetValue(key, out var info))
                            {
                                var displayValue = FormatGroupDisplayValue(rawValue);
                                var safeName = BuildUniqueGroupName(displayValue, nameUsage);

                                info = new FieldGroupInfo
                                {
                                    Key = key,
                                    DisplayValue = displayValue,
                                    OutputName = safeName,
                                    FeatureCount = 0
                                };
                                groups[key] = info;
                            }

                            info.FeatureCount++;
                        }
                    }
                }

                result.Groups = groups.Values
                    .OrderBy(g => g.DisplayValue, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            }

            return result;
        }

        private async Task<int> ProcessGroupsAsync(FeatureLayer layer, GroupPreparationResult preparationResult)
        {
            if (preparationResult.Groups.Count == 0)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessage = "未找到可导出的分组";
                    IsProgressIndeterminate = false;
                    Progress = 0;
                });
                return 0;
            }

            var envSettings = Geoprocessing.MakeEnvironmentArray("addOutputsToMap", "False", "overwriteoutput", "True");
            int total = preparationResult.Groups.Count;
            int currentIndex = 0;
            int completedCount = 0;

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                IsProgressIndeterminate = false;
                Progress = 0;
            });

            foreach (var group in preparationResult.Groups)
            {
                if (CancelRequested)
                {
                    LogWarning("操作已取消，停止处理剩余分组");
                    break;
                }

                currentIndex++;
                int progressValue = (int)((double)currentIndex / total * 100);
                string statusText = $"正在导出 {group.DisplayValue} ({currentIndex}/{total})...";

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessage = statusText;
                    Progress = progressValue;
                });

                string outputFolder = OutputFolder;
                if (CreateSubFolder)
                {
                    outputFolder = Path.Combine(OutputFolder, group.OutputName);
                    if (!Directory.Exists(outputFolder))
                    {
                        Directory.CreateDirectory(outputFolder);
                        LogInfo($"创建目录: {outputFolder}");
                    }
                }

                string shapeName = group.OutputName;
                string whereClause = BuildWhereClause(preparationResult.FieldName, preparationResult.FieldType, group);

                LogInfo($"开始导出分组: {group.DisplayValue} (要素 {group.FeatureCount})");
                LogInfo($"导出文件名: {shapeName}");
                LogInfo($"查询条件: {whereClause}");

                bool success = await ExportGroupAsync(layer, outputFolder, shapeName, whereClause, envSettings);
                if (success)
                {
                    completedCount++;
                }
            }

            return completedCount;
        }

        private async Task<bool> ExportGroupAsync(FeatureLayer layer, string outputFolder, string shapeName, string whereClause, IEnumerable<KeyValuePair<string, string>> environmentSettings)
        {
            var exportParams = Geoprocessing.MakeValueArray(
                layer,
                outputFolder,
                shapeName,
                whereClause,
                "");

            using (var cts = new CancellationTokenSource())
            {
                _geoprocessingCancellationSource = cts;

                try
                {
                    var gpResult = await Geoprocessing.ExecuteToolAsync(
                        "conversion.FeatureClassToFeatureClass",
                        exportParams,
                        environmentSettings,
                        cts.Token,
                        null,
                        GPExecuteToolFlags.GPThread);

                    if (gpResult == null)
                    {
                        LogError($"导出失败: GP 结果为空 ({shapeName})");
                        return false;
                    }

                    if (gpResult.IsFailed)
                    {
                        LogError($"导出失败: {shapeName}");
                        foreach (var msg in gpResult.Messages)
                        {
                            switch (msg.Type)
                            {
                                case GPMessageType.Error:
                                    LogError($"GP错误: {msg.Text}");
                                    break;
                                case GPMessageType.Warning:
                                    LogWarning($"GP警告: {msg.Text}");
                                    break;
                                default:
                                    LogInfo($"GP信息: {msg.Text}");
                                    break;
                            }
                        }
                        return false;
                    }

                    foreach (var msg in gpResult.Messages)
                    {
                        if (msg.Type == GPMessageType.Warning)
                            LogWarning($"GP警告: {msg.Text}");
                    }

                    LogInfo($"导出成功: {shapeName}");
                    return true;
                }
                catch (OperationCanceledException)
                {
                    LogWarning($"地理处理任务已取消: {shapeName}");
                    return false;
                }
                catch (Exception ex)
                {
                    if (CancelRequested)
                    {
                        LogWarning($"由于用户取消，跳过 {shapeName}");
                        return false;
                    }

                    LogError($"导出要素出错: {ex.Message}");
                    return false;
                }
                finally
                {
                    _geoprocessingCancellationSource = null;
                }
            }
        }

        private string BuildWhereClause(string fieldName, FieldType fieldType, FieldGroupInfo group)
        {
            if (group.Key.IsNull)
                return $"{fieldName} IS NULL";

            switch (fieldType)
            {
                case FieldType.String:
                    var textValue = group.Key.Value?.ToString() ?? string.Empty;
                    var escaped = textValue.Replace("'", "''");
                    return $"{fieldName} = '{escaped}'";
                case FieldType.Integer:
                case FieldType.SmallInteger:
                    var longValue = Convert.ToInt64(group.Key.Value, CultureInfo.InvariantCulture);
                    return $"{fieldName} = {longValue}";
                case FieldType.Double:
                case FieldType.Single:
                    var doubleValue = Convert.ToDouble(group.Key.Value, CultureInfo.InvariantCulture);
                    return $"{fieldName} = {doubleValue.ToString("R", CultureInfo.InvariantCulture)}";
                default:
                    var fallback = group.Key.Value?.ToString()?.Replace("'", "''") ?? string.Empty;
                    return $"{fieldName} = '{fallback}'";
            }
        }

        private string BuildUniqueGroupName(string displayValue, Dictionary<string, int> usageTracker)
        {
            var baseName = SanitizeFileName(displayValue);

            if (!usageTracker.TryGetValue(baseName, out var count))
            {
                usageTracker[baseName] = 1;
                return baseName;
            }

            count++;
            usageTracker[baseName] = count;
            return $"{baseName}_{count}";
        }

        private string FormatGroupDisplayValue(object value)
        {
            var displayValue = value?.ToString();
            return string.IsNullOrWhiteSpace(displayValue) ? "未知" : displayValue;
        }

        private sealed class GroupPreparationResult
        {
            public string FieldName { get; set; } = string.Empty;
            public FieldType FieldType { get; set; }
            public List<FieldGroupInfo> Groups { get; set; } = new List<FieldGroupInfo>();
        }

        private sealed class FieldGroupInfo
        {
            public FieldGroupKey Key { get; set; }
            public string DisplayValue { get; set; } = string.Empty;
            public string OutputName { get; set; } = string.Empty;
            public int FeatureCount { get; set; }
        }

        private readonly struct FieldGroupKey : IEquatable<FieldGroupKey>
        {
            public FieldGroupKey(object rawValue, FieldType fieldType)
            {
                FieldType = fieldType;

                if (rawValue == null || rawValue == DBNull.Value)
                {
                    IsNull = true;
                    Value = null;
                    return;
                }

                IsNull = false;

                switch (fieldType)
                {
                    case FieldType.Integer:
                    case FieldType.SmallInteger:
                        Value = Convert.ToInt64(rawValue, CultureInfo.InvariantCulture);
                        break;
                    case FieldType.Double:
                    case FieldType.Single:
                        Value = Convert.ToDouble(rawValue, CultureInfo.InvariantCulture);
                        break;
                    default:
                        Value = rawValue.ToString();
                        break;
                }
            }

            public FieldType FieldType { get; }
            public object Value { get; }
            public bool IsNull { get; }

            public bool Equals(FieldGroupKey other)
            {
                if (FieldType != other.FieldType || IsNull != other.IsNull)
                    return false;

                if (IsNull)
                    return true;

                return Value?.Equals(other.Value) ?? other.Value == null;
            }

            public override bool Equals(object obj) => obj is FieldGroupKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = (int)FieldType;
                    hash = (hash * 397) ^ (IsNull ? 1 : 0);
                    hash = (hash * 397) ^ (Value?.GetHashCode() ?? 0);
                    return hash;
                }
            }
        }

        /// <summary>
        /// 清理文件名中的非法字符，确保文件名有效
        /// </summary>
        private string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return "未知";
            }

            // 替换文件名中的非法字符
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                fileName = fileName.Replace(c, '_');
            }

            // 替换其他可能导致问题的字符（保持最小替换）
            fileName = fileName.Replace("\\", "_");
            fileName = fileName.Replace("/", "_");
            fileName = fileName.Replace(":", "_");
            fileName = fileName.Replace("*", "_");
            fileName = fileName.Replace("?", "_");
            fileName = fileName.Replace("\"", "_");
            fileName = fileName.Replace("<", "_");
            fileName = fileName.Replace(">", "_");
            fileName = fileName.Replace("|", "_");

            // 移除多余的下划线
            while (fileName.Contains("__"))
            {
                fileName = fileName.Replace("__", "_");
            }

            // 移除文件名开头和末尾的下划线
            fileName = fileName.Trim('_');

            // 如果文件名为空，使用默认值
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = "未知";
            }

            return fileName;
        }

        /// <summary>
        /// 清除日志
        /// </summary>
        private void ClearLog()
        {
            _logBuilder.Clear();
            LogContent = "";
        }

        /// <summary>
        /// 添加信息日志
        /// </summary>
        private void LogInfo(string message)
        {
            AddLogEntry($"[信息] {message}");
        }

        /// <summary>
        /// 添加错误日志
        /// </summary>
        private void LogError(string message)
        {
            AddLogEntry($"[错误] {message}");
        }

        /// <summary>
        /// 添加警告日志
        /// </summary>
        private void LogWarning(string message)
        {
            AddLogEntry($"[警告] {message}");
        }

        /// <summary>
        /// 添加日志条目
        /// </summary>
        private void AddLogEntry(string entry)
        {
            // 在日志构建器中添加条目
            _logBuilder.AppendLine($"{DateTime.Now:HH:mm:ss} {entry}");
            
            // 更新UI上的日志内容
            System.Windows.Application.Current.Dispatcher.Invoke(() => 
            {
                LogContent = _logBuilder.ToString();
            });
        }
        
        /// <summary>
        /// 显示帮助
        /// </summary>
        private void ShowHelp()
        {
            string helpContent = "按字段批量裁剪要素图层工具使用说明\n\n" +
                               "功能描述：\n" +
                               "该工具用于按照指定字段对要素图层进行分组并导出为多个Shapefile文件。\n\n" +
                               "参数说明：\n" +
                               "1. 要素图层：选择要进行批量裁剪的要素图层\n" +
                               "2. 分组裁剪字段：用于分组的字段，每个不同的字段值将作为一个分组\n" +
                               "3. 输出文件夹：导出文件的存储位置\n" +
                               "4. 单独创建文件夹：是否为每个分组创建单独的文件夹\n\n" +
                               "操作步骤：\n" +
                               "1. 选择要素图层\n" +
                               "2. 选择分组裁剪字段\n" +
                               "3. 设置输出文件夹\n" +
                               "4. 选择是否为每个分组创建单独文件夹\n" +
                               "5. 点击运行按钮执行裁剪操作\n\n" +
                               "注意事项：\n" +
                               "- 导出文件名将使用分组值命名，空值时使用\"图层名_空值\"\n" +
                               "- 分组值中的特殊字符将被替换为合法的文件名字符\n" +
                               "- 操作过程和结果将显示在日志窗口中\n" +
                               "- 导出的图层不会自动添加到地图中\n" +
                               "- 处理过程中可以点击\"停止\"按钮取消操作";

            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(helpContent, "按字段批量裁剪要素图层工具使用说明");
        }
    }
} 
