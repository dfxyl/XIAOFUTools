using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XIAOFUTools.Common;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Tools.RangeClipTool
{
    /// <summary>
    /// 根据范围批量裁剪要素图层视图模型
    /// </summary>
    internal class RangeClipToolViewModel : PropertyChangedBase
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

        // 范围图层列表
        private ObservableCollection<FeatureLayer> _rangeLayers;
        public ObservableCollection<FeatureLayer> RangeLayers
        {
            get => _rangeLayers;
            set
            {
                SetProperty(ref _rangeLayers, value);
            }
        }

        // 选中的范围图层
        private FeatureLayer _selectedRangeLayer;
        public FeatureLayer SelectedRangeLayer
        {
            get => _selectedRangeLayer;
            set
            {
                SetProperty(ref _selectedRangeLayer, value);
                NotifyPropertyChanged(() => HasSelectedRangeLayer);
                LoadRangeFields();
            }
        }

        // 是否有选中的范围图层
        public bool HasSelectedRangeLayer => SelectedRangeLayer != null;

        // 范围字段名称列表
        private ObservableCollection<string> _rangeFieldNames;
        public ObservableCollection<string> RangeFieldNames
        {
            get => _rangeFieldNames;
            set
            {
                SetProperty(ref _rangeFieldNames, value);
            }
        }

        // 选中的范围字段
        private string _selectedRangeField;
        public string SelectedRangeField
        {
            get => _selectedRangeField;
            set
            {
                SetProperty(ref _selectedRangeField, value);
            }
        }

        // 需裁剪图层项目列表
        private ObservableCollection<ClipLayerItem> _clipLayerItems;
        public ObservableCollection<ClipLayerItem> ClipLayerItems
        {
            get => _clipLayerItems;
            set
            {
                SetProperty(ref _clipLayerItems, value);
            }
        }

        // 是否创建子文件夹
        private bool _createSubFolder = true;
        public bool CreateSubFolder
        {
            get => _createSubFolder;
            set
            {
                SetProperty(ref _createSubFolder, value);
            }
        }

        // 输出文件夹
        private string _outputFolder = "";
        public string OutputFolder
        {
            get => _outputFolder;
            set
            {
                SetProperty(ref _outputFolder, value);
            }
        }

        // 进度值
        private int _progress = 0;
        public int Progress
        {
            get => _progress;
            set
            {
                SetProperty(ref _progress, value);
            }
        }

        // 进度是否不确定
        private bool _isProgressIndeterminate = false;
        public bool IsProgressIndeterminate
        {
            get => _isProgressIndeterminate;
            set
            {
                SetProperty(ref _isProgressIndeterminate, value);
            }
        }

        // 状态消息
        private string _statusMessage = "";
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                SetProperty(ref _statusMessage, value);
            }
        }

        // 日志内容
        private string _logContent = "";
        public string LogContent
        {
            get => _logContent;
            set
            {
                SetProperty(ref _logContent, value);
            }
        }

        #endregion

        #region 命令

        // 全选图层命令
        private ICommand _selectAllLayersCommand;
        public ICommand SelectAllLayersCommand
        {
            get
            {
                return _selectAllLayersCommand ?? (_selectAllLayersCommand = new RelayCommand(() =>
                {
                    foreach (var item in ClipLayerItems)
                    {
                        item.IsSelected = true;
                    }
                }));
            }
        }

        // 反选图层命令
        private ICommand _invertSelectionCommand;
        public ICommand InvertSelectionCommand
        {
            get
            {
                return _invertSelectionCommand ?? (_invertSelectionCommand = new RelayCommand(() =>
                {
                    foreach (var item in ClipLayerItems)
                    {
                        item.IsSelected = !item.IsSelected;
                    }
                }));
            }
        }

        // 取消命令
        private ICommand _cancelCommand;
        public ICommand CancelCommand
        {
            get
            {
                return _cancelCommand ?? (_cancelCommand = new RelayCommand(() =>
                {
                    if (IsProcessing)
                    {
                        // 如果正在处理，则设置取消标志
                        CancelRequested = true;
                        StatusMessage = "正在取消操作...";
                        LogWarning("用户请求取消操作");
                    }
                    else
                    {
                        // 如果没有正在处理的操作，则关闭窗口
                        System.Windows.Application.Current.Dispatcher.Invoke(() => 
                        {
                            // 在UI线程上查找窗口
                            foreach (var window in System.Windows.Application.Current.Windows)
                            {
                                if (window is ArcGIS.Desktop.Framework.Controls.ProWindow proWindow && 
                                    proWindow.Title == "根据范围批量裁剪要素图层")
                                {
                                    proWindow.Close();
                                    break;
                                }
                            }
                        });
                    }
                }));
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

        // 浏览文件夹命令
        private ICommand _browseFolderCommand;
        public ICommand BrowseFolderCommand
        {
            get
            {
                return _browseFolderCommand ?? (_browseFolderCommand = new RelayCommand(() => BrowseFolder()));
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
        public RangeClipToolViewModel()
        {
            // 初始化属性
            RangeLayers = new ObservableCollection<FeatureLayer>();
            RangeFieldNames = new ObservableCollection<string>();
            ClipLayerItems = new ObservableCollection<ClipLayerItem>();

            // 设置输出文件夹为当前项目文件夹
            string projectFolder = GetProjectFolderPath();
            OutputFolder = projectFolder;

            CreateSubFolder = true;
            StatusMessage = "请选择范围图层、范围字段和需裁剪的要素图层。";
            LogContent = "";
            Progress = 0;
            IsProgressIndeterminate = false;

            // 加载图层
            LoadLayers();
        }

        /// <summary>
        /// 刷新图层列表（供DockPane调用）
        /// </summary>
        public void RefreshLayers()
        {
            LoadLayers();
        }

        /// <summary>
        /// 获取项目文件夹路径
        /// </summary>
        private string GetProjectFolderPath()
        {
            try
            {
                var project = Project.Current;
                if (project != null)
                {
                    return Path.GetDirectoryName(project.Path);
                }
            }
            catch (Exception ex)
            {
                LogError($"获取项目文件夹路径失败: {ex.Message}");
            }

            return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        /// <summary>
        /// 加载图层
        /// </summary>
        private void LoadLayers()
        {
            QueuedTask.Run(() =>
            {
                try
                {
                    // 获取所有图层的临时列表
                    var tempRangeLayers = new List<FeatureLayer>();
                    var tempClipLayers = new List<FeatureLayer>();
                    var map = MapView.Active?.Map;

                    if (map != null)
                    {
                        var layers = map.GetLayersAsFlattenedList().OfType<FeatureLayer>().ToList();
                        tempRangeLayers.AddRange(layers);
                        tempClipLayers.AddRange(layers);
                    }

                    // 在UI线程更新图层列表
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        // 清空图层列表
                        RangeLayers.Clear();
                        ClipLayerItems.Clear();

                        // 添加范围图层
                        foreach (var layer in tempRangeLayers)
                        {
                            RangeLayers.Add(layer);
                        }

                        // 添加裁剪图层项目
                        foreach (var layer in tempClipLayers)
                        {
                            ClipLayerItems.Add(new ClipLayerItem
                            {
                                LayerName = layer.Name,
                                Layer = layer,
                                IsSelected = false
                            });
                        }

                        // 如果有图层，默认选择第一个范围图层
                        if (RangeLayers.Count > 0)
                        {
                            SelectedRangeLayer = RangeLayers[0];
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
        /// 加载范围字段
        /// </summary>
        private void LoadRangeFields()
        {
            if (SelectedRangeLayer == null)
            {
                RangeFieldNames.Clear();
                return;
            }

            QueuedTask.Run(() =>
            {
                try
                {
                    // 清空字段列表
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        RangeFieldNames.Clear();
                    });

                    // 获取字段的临时列表
                    var tempFields = new List<string>();

                    // 获取图层定义
                    var layerDef = SelectedRangeLayer.GetFeatureClass().GetDefinition();

                    // 遍历字段，只添加文本和数值字段
                    foreach (var field in layerDef.GetFields())
                    {
                        if (field.FieldType == FieldType.String ||
                            field.FieldType == FieldType.Integer ||
                            field.FieldType == FieldType.SmallInteger ||
                            field.FieldType == FieldType.Double ||
                            field.FieldType == FieldType.Single)
                        {
                            tempFields.Add(field.Name);
                        }
                    }

                    // 在UI线程更新字段列表
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        // 将临时列表中的字段添加到字段名称列表
                        foreach (var field in tempFields)
                        {
                            RangeFieldNames.Add(field);
                        }

                        // 如果有字段，默认选择第一个
                        if (RangeFieldNames.Count > 0)
                        {
                            SelectedRangeField = RangeFieldNames[0];
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
            return SelectedRangeLayer != null &&
                   !string.IsNullOrEmpty(SelectedRangeField) &&
                   ClipLayerItems.Any(x => x.IsSelected) &&
                   !string.IsNullOrEmpty(OutputFolder) &&
                   Directory.Exists(OutputFolder);
        }

        /// <summary>
        /// 执行裁剪操作
        /// </summary>
        private async void Execute()
        {
            if (!CanExecute())
            {
                StatusMessage = "请检查参数设置。";
                return;
            }

            IsProcessing = true;
            CancelRequested = false;
            Progress = 0;
            IsProgressIndeterminate = true;

            try
            {
                StatusMessage = "正在执行根据范围批量裁剪...";
                LogInfo("开始执行根据范围批量裁剪操作");

                await QueuedTask.Run(async () =>
                {
                    await PerformRangeClip();
                });

                if (!CancelRequested)
                {
                    StatusMessage = "根据范围批量裁剪完成。";
                    LogInfo("根据范围批量裁剪操作完成");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"执行出错: {ex.Message}";
                LogError($"执行出错: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
                IsProgressIndeterminate = false;
                Progress = 0;
            }
        }

        /// <summary>
        /// 执行范围裁剪
        /// </summary>
        private async Task PerformRangeClip()
        {
            try
            {
                // 使用用户选择的输出文件夹
                string baseOutputFolder = OutputFolder;

                if (!Directory.Exists(baseOutputFolder))
                {
                    Directory.CreateDirectory(baseOutputFolder);
                }

                LogInfo($"输出文件夹: {baseOutputFolder}");

                // 获取范围图层的唯一字段值
                var rangeValues = await GetUniqueRangeValues();
                if (rangeValues.Count == 0)
                {
                    LogWarning("范围图层中没有找到有效的字段值");
                    return;
                }

                LogInfo($"找到 {rangeValues.Count} 个范围值");

                // 获取选中的裁剪图层
                var selectedLayers = ClipLayerItems.Where(x => x.IsSelected).ToList();
                LogInfo($"选中 {selectedLayers.Count} 个图层进行裁剪");

                int totalOperations = rangeValues.Count * selectedLayers.Count;
                int currentOperation = 0;

                // 更新进度条为确定模式
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    IsProgressIndeterminate = false;
                    Progress = 0;
                });

                // 对每个范围值进行处理
                foreach (var rangeValue in rangeValues)
                {
                    if (CancelRequested) break;

                    string rangeValueStr = rangeValue?.ToString() ?? "空值";
                    LogInfo($"处理范围值: {rangeValueStr}");

                    // 创建子文件夹（如果需要）
                    string currentOutputFolder = baseOutputFolder;
                    if (CreateSubFolder)
                    {
                        string safeFolderName = GetSafeFileName(rangeValueStr);
                        currentOutputFolder = Path.Combine(baseOutputFolder, safeFolderName);
                        if (!Directory.Exists(currentOutputFolder))
                        {
                            Directory.CreateDirectory(currentOutputFolder);
                        }
                    }

                    // 创建范围几何体
                    var rangeGeometry = await CreateRangeGeometry(rangeValue);
                    if (rangeGeometry == null)
                    {
                        LogWarning($"无法为范围值 {rangeValueStr} 创建几何体");
                        continue;
                    }

                    // 对每个选中的图层进行裁剪
                    foreach (var layerItem in selectedLayers)
                    {
                        if (CancelRequested) break;

                        currentOperation++;
                        int progressPercent = (int)((double)currentOperation / totalOperations * 100);

                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            Progress = progressPercent;
                            StatusMessage = $"正在裁剪 {layerItem.LayerName} (范围: {rangeValueStr})...";
                        });

                        await ClipLayerWithRange(layerItem.Layer, rangeGeometry, rangeValueStr, currentOutputFolder);
                    }
                }

                if (!CancelRequested)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        Progress = 100;
                        StatusMessage = "根据范围批量裁剪完成。";
                    });
                    LogInfo("所有裁剪操作完成");
                }
            }
            catch (Exception ex)
            {
                LogError($"执行范围裁剪时出错: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 获取范围字段的唯一值
        /// </summary>
        private Task<List<object>> GetUniqueRangeValues()
        {
            var uniqueValues = new List<object>();

            try
            {
                var featureClass = SelectedRangeLayer.GetFeatureClass();
                var queryFilter = new QueryFilter();

                using (var cursor = featureClass.Search(queryFilter, false))
                {
                    while (cursor.MoveNext())
                    {
                        if (CancelRequested) break;

                        using (var feature = cursor.Current)
                        {
                            var value = feature[SelectedRangeField];
                            if (!uniqueValues.Contains(value))
                            {
                                uniqueValues.Add(value);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"获取唯一值时出错: {ex.Message}");
            }

            return Task.FromResult(uniqueValues);
        }

        /// <summary>
        /// 创建范围几何体
        /// </summary>
        private Task<Geometry> CreateRangeGeometry(object rangeValue)
        {
            try
            {
                var featureClass = SelectedRangeLayer.GetFeatureClass();
                var queryFilter = new QueryFilter();

                // 构建查询条件
                if (rangeValue == null)
                {
                    queryFilter.WhereClause = $"{SelectedRangeField} IS NULL";
                }
                else if (rangeValue is string)
                {
                    var strVal = rangeValue.ToString()?.Replace("'", "''");
                    queryFilter.WhereClause = $"{SelectedRangeField} = '{strVal}'";
                }
                else
                {
                    queryFilter.WhereClause = $"{SelectedRangeField} = {rangeValue}";
                }

                var geometries = new List<Geometry>();

                using (var cursor = featureClass.Search(queryFilter, false))
                {
                    while (cursor.MoveNext())
                    {
                        if (CancelRequested) break;

                        using (var feature = cursor.Current as Feature)
                        {
                            if (feature != null)
                            {
                                var geometry = feature.GetShape();
                                if (geometry != null)
                                {
                                    geometries.Add(geometry);
                                }
                            }
                        }
                    }
                }

                if (geometries.Count == 0)
                {
                    return Task.FromResult<Geometry>(null);
                }

                // 如果只有一个几何体，直接返回
                if (geometries.Count == 1)
                {
                    return Task.FromResult(geometries[0]);
                }

                // 合并多个几何体
                return Task.FromResult(GeometryEngine.Instance.Union(geometries));
            }
            catch (Exception ex)
            {
                LogError($"创建范围几何体时出错: {ex.Message}");
                return Task.FromResult<Geometry>(null);
            }
        }

        /// <summary>
        /// 使用范围裁剪图层
        /// </summary>
        private async Task ClipLayerWithRange(FeatureLayer layer, Geometry rangeGeometry, string rangeValueStr, string outputFolder)
        {
            try
            {
                // 保持原始图层名称，不添加范围值
                string safeFileName = GetSafeFileName(layer.Name);
                string outputPath = Path.Combine(outputFolder, $"{safeFileName}.shp");

                LogInfo($"开始裁剪图层: {layer.Name} -> {outputPath}");

                // 使用地理处理工具进行裁剪
                var parameters = Geoprocessing.MakeValueArray(
                    layer,               // 输入要素（直接传递图层对象，避免名称解析问题）
                    rangeGeometry,       // 裁剪要素（几何）
                    outputPath,          // 输出要素类
                    ""                   // XY 容差
                );

                var environments = Geoprocessing.MakeEnvironmentArray("addOutputsToMap", "False", "overwriteoutput", "True");

                var result = await Geoprocessing.ExecuteToolAsync("analysis.Clip", parameters, environments, null, null, GPExecuteToolFlags.AddToHistory);

                if (result == null)
                {
                    LogError($"裁剪失败: {layer.Name} - GP 结果为空");
                }
                else if (result.IsFailed)
                {
                    LogError($"裁剪失败: {layer.Name}");
                    foreach (var msg in result.Messages)
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
                }
                else
                {
                    foreach (var msg in result.Messages)
                    {
                        if (msg.Type == GPMessageType.Warning)
                            LogWarning($"GP警告: {msg.Text}");
                    }
                    LogInfo($"裁剪成功: {layer.Name} -> {outputPath}");
                }
            }
            catch (Exception ex)
            {
                LogError($"裁剪图层时出错: {layer.Name} - {ex.Message}");
            }
        }

        /// <summary>
        /// 获取安全的文件名
        /// </summary>
        private string GetSafeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return "未命名";

            // 替换非法字符
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                fileName = fileName.Replace(c, '_');
            }

            // 替换其他可能有问题的字符
            fileName = fileName.Replace(' ', '_')
                              .Replace('.', '_')
                              .Replace(',', '_')
                              .Replace(';', '_')
                              .Replace(':', '_');

            // 限制长度
            if (fileName.Length > 50)
            {
                fileName = fileName.Substring(0, 50);
            }

            return fileName;
        }

        /// <summary>
        /// 记录信息日志
        /// </summary>
        private void LogInfo(string message)
        {
            string logMessage = $"[{DateTime.Now:HH:mm:ss}] {message}";
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                LogContent += logMessage + Environment.NewLine;
            });
        }

        /// <summary>
        /// 记录错误日志
        /// </summary>
        private void LogError(string message)
        {
            string logMessage = $"[{DateTime.Now:HH:mm:ss}] 错误: {message}";
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                LogContent += logMessage + Environment.NewLine;
            });
        }

        /// <summary>
        /// 记录警告日志
        /// </summary>
        private void LogWarning(string message)
        {
            string logMessage = $"[{DateTime.Now:HH:mm:ss}] 警告: {message}";
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                LogContent += logMessage + Environment.NewLine;
            });
        }

        /// <summary>
        /// 浏览文件夹
        /// </summary>
        private void BrowseFolder()
        {
            try
            {
                // 使用 ArcGIS Pro 自带的 OpenItemDialog 选择文件夹（需在UI线程上调用）
                var initialLocation = Project.Current?.HomeFolderPath ?? OutputFolder;
                if (!Directory.Exists(initialLocation))
                {
                    initialLocation = GetProjectFolderPath();
                }

                var openItemDialog = new OpenItemDialog
                {
                    Title = "选择输出文件夹",
                    InitialLocation = initialLocation,
                    MultiSelect = false,
                    Filter = ItemFilters.Folders // 仅允许选择文件夹
                };

                bool? result = openItemDialog.ShowDialog();
                if (result == true)
                {
                    var selectedItem = openItemDialog.Items.FirstOrDefault();
                    if (selectedItem != null)
                    {
                        OutputFolder = selectedItem.Path;
                        LogInfo($"选择输出文件夹: {OutputFolder}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"选择文件夹出错: {ex.Message}", "错误");
            }
        }

        /// <summary>
        /// 显示帮助
        /// </summary>
        private void ShowHelp()
        {
            string helpContent = "根据范围批量裁剪要素图层工具使用说明\n\n" +
                               "功能描述：\n" +
                               "该工具用于根据范围图层的字段值对要素图层进行批量裁剪。\n\n" +
                               "参数说明：\n" +
                               "1. 范围图层：选择用作裁剪范围的要素图层\n" +
                               "2. 范围字段：选择范围图层中用于分组的字段\n" +
                               "3. 需裁剪要素图层：选择需要被裁剪的要素图层（可多选）\n" +
                               "4. 输出文件夹：选择裁剪结果的保存位置\n" +
                               "5. 单独创建文件夹：是否为每个范围字段值创建单独的文件夹\n\n" +
                               "操作步骤：\n" +
                               "1. 选择范围图层\n" +
                               "2. 选择范围字段\n" +
                               "3. 勾选需要裁剪的要素图层\n" +
                               "4. 选择输出文件夹\n" +
                               "5. 选择是否创建子文件夹\n" +
                               "6. 点击开始按钮执行裁剪操作\n\n" +
                               "注意事项：\n" +
                               "- 工具会根据范围字段的不同值将范围图层分组\n" +
                               "- 每个分组的几何体将用于裁剪选中的要素图层\n" +
                               "- 输出文件保存在指定的输出文件夹中\n" +
                               "- 文件名保持原始图层名称\n" +
                               "- 处理过程中可以点击停止按钮取消操作";

            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(helpContent, "根据范围批量裁剪要素图层工具使用说明");
        }
    }

    /// <summary>
    /// 裁剪图层项目类
    /// </summary>
    public class ClipLayerItem : PropertyChangedBase
    {
        private bool _isSelected = false;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public string LayerName { get; set; }
        public FeatureLayer Layer { get; set; }
    }
}
