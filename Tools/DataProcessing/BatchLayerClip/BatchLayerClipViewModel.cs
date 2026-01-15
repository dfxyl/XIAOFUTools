using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Text;
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
            // 重置取消标志
            CancelRequested = false;
            // 设置处理状态
            IsProcessing = true;
            
            StatusMessage = "正在处理...";
            ClearLog();
            // 重置进度条
            Progress = 0;
            IsProgressIndeterminate = true;
            
            LogInfo($"===== 开始批量裁剪操作 - {DateTime.Now} =====");
            LogInfo($"选择的图层: {SelectedFeatureLayer.Name}");
            LogInfo($"选择的分组字段: {SelectedField}");
            LogInfo($"输出文件夹: {OutputFolder}");
            LogInfo($"是否创建子文件夹: {(CreateSubFolder ? "是" : "否")}");
            LogInfo("开始处理...");
            
            await QueuedTask.Run(async () =>
            {
                try
                {
                    // 获取选中图层和字段
                    var layer = SelectedFeatureLayer;
                    
                    // 从显示名称中提取真实字段名（去除别名部分）
                    string fieldName = SelectedField;
                    string displayFieldName = fieldName; // 保存显示用的名称
                    
                    if (fieldName.Contains("("))
                    {
                        // 如果字段名是带别名的格式，提取括号前的部分作为实际字段名
                        fieldName = fieldName.Substring(0, fieldName.IndexOf("(")).Trim();
                        LogInfo($"从显示名 \"{displayFieldName}\" 提取实际字段名: \"{fieldName}\"");
                    }
                    
                    // 检查fieldName是否为空
                    if (string.IsNullOrWhiteSpace(fieldName))
                    {
                        throw new Exception("无法获取有效字段名");
                    }
                    
                    // OID 字段名（例如 OBJECTID 或 FID），后续用于 where 子句
                    string oidFieldName = null;

                    // 获取要素并按字段分组
                    var featuresByValue = new Dictionary<string, List<long>>();
                    
                    using (var table = layer.GetTable())
                    using (var rowCursor = table.Search())
                    {
                        LogInfo("正在按字段分组要素...");
                        // 获取真实的 OID 字段名
                        try
                        {
                            oidFieldName = table.GetDefinition()?.GetObjectIDField();
                            if (string.IsNullOrWhiteSpace(oidFieldName))
                                oidFieldName = "OBJECTID"; // 兜底
                            LogInfo($"OID字段: {oidFieldName}");
                        }
                        catch { oidFieldName = "OBJECTID"; }
                        int featureCount = 0;
                        
                        while (rowCursor.MoveNext())
                        {
                            using (var row = rowCursor.Current)
                            {
                                featureCount++;
                                // 获取分组字段值
                                var value = row[fieldName]?.ToString() ?? "未知";
                                
                                // 替换特殊字符
                                var safeName = SanitizeFileName(value);
                                
                                // 将要素OID添加到对应分组
                                if (!featuresByValue.ContainsKey(safeName))
                                {
                                    featuresByValue[safeName] = new List<long>();
                                }
                                
                                featuresByValue[safeName].Add(row.GetObjectID());
                            }
                        }
                        
                        LogInfo($"分组完成，共 {featureCount} 个要素，分为 {featuresByValue.Count} 个组。");
                    }
                    
                    // 开始逐个分组导出
                    int total = featuresByValue.Count;
                    int current = 0;
                    
                    // 设置进度条为确定状态
                    System.Windows.Application.Current.Dispatcher.Invoke(() => 
                    {
                        IsProgressIndeterminate = false;
                    });
                    
                    foreach (var group in featuresByValue)
                    {
                        // 检查是否请求取消
                        if (CancelRequested)
                        {
                            LogWarning("操作已取消，停止处理");
                            break;
                        }
                        
                        current++;
                        var groupValue = group.Key;
                        var objectIds = group.Value;
                        
                        // 更新状态和进度
                        int progressValue = (int)((double)current / total * 100);
                        string statusText = $"正在导出 {groupValue} ({current}/{total})...";
                        System.Windows.Application.Current.Dispatcher.Invoke(() => 
                        {
                            StatusMessage = statusText;
                            Progress = progressValue;
                        });
                        
                        LogInfo($"开始导出分组: {groupValue} ({objectIds.Count} 个要素)");
                        
                        // 确定输出路径
                        string outputPath = OutputFolder;
                        if (CreateSubFolder)
                        {
                            outputPath = Path.Combine(OutputFolder, groupValue);
                            if (!Directory.Exists(outputPath))
                            {
                                Directory.CreateDirectory(outputPath);
                                LogInfo($"创建目录: {outputPath}");
                            }
                        }
                        
                        // 创建查询条件，使用真实 OID 字段名，格式如"<OID> IN (1,2,3)"
                        string whereClause = "";
                        if (objectIds.Count > 0)
                        {
                            var oidField = string.IsNullOrWhiteSpace(oidFieldName) ? "OBJECTID" : oidFieldName;
                            whereClause = $"{oidField} IN ({string.Join(",", objectIds)})";
                        }
                        
                        // 导出文件名
                        string shapeName;
                        if (string.IsNullOrWhiteSpace(groupValue) || groupValue == "未知")
                        {
                            // 如果分组值为空，使用图层名称加"空值"标记
                            string layerName = SanitizeFileName(layer.Name);
                            shapeName = $"{layerName}_空值";
                        }
                        else
                        {
                            // 只使用分组值作为文件名（已经在前面进行了清理）
                            shapeName = groupValue;
                        }
                        
                        // 确保shapeName是有效的文件名
                        if (string.IsNullOrWhiteSpace(shapeName) || shapeName.Trim() == "_")
                        {
                            shapeName = "未知值";
                        }
                        
                        LogInfo($"导出文件名: {shapeName}");
                        LogInfo($"查询条件: {whereClause}");
                        
                        // 执行要素导出
                        // 再次检查是否请求取消
                        if (CancelRequested)
                        {
                            LogWarning("操作已取消，跳过当前分组");
                            continue;
                        }
                        
                        try
                        {
                            // 使用按条件导出要素
                            // 设置环境变量：不添加到地图，并允许覆盖输出
                            var envSettings = Geoprocessing.MakeEnvironmentArray("addOutputsToMap", "False", "overwriteoutput", "True");
                            
                            // 准备导出参数
                            var exportParams = Geoprocessing.MakeValueArray(
                                layer, // 输入要素类
                                outputPath, // 输出位置
                                shapeName, // 输出要素类名称
                                whereClause,
                                "" // 不使用字段映射
                            );
                            
                            string toolName = "conversion.FeatureClassToFeatureClass";
                            LogInfo($"执行导出: {layer.Name} -> {outputPath}\\{shapeName} (不添加到地图)");
                            
                                            // 执行工具
                            // 创建可取消的任务
                            var cts = new System.Threading.CancellationTokenSource();
                            
                            // 设置地理处理标志，使用GPThread来避免阻塞UI线程
                            GPExecuteToolFlags flags = GPExecuteToolFlags.GPThread | GPExecuteToolFlags.None;
                            
                            // 准备取消监控任务
                            var cancelMonitorTask = Task.Run(() => {
                                while (!CancelRequested)
                                {
                                    // 短暂等待检查取消请求
                                    Thread.Sleep(200);
                                    
                                    // 如果用户请求取消
                                    if (CancelRequested)
                                    {
                                        LogWarning("取消地理处理任务...");
                                        cts.Cancel();
                                        break;
                                    }
                                }
                            });
                            
                            // 使用异步执行地理处理操作
                            LogInfo($"开始异步执行地理处理工具: {toolName}");
                            IGPResult gpResult = null;
                            
                            try 
                            {
                                // 这里使用真正的await，不阻塞UI线程
                                gpResult = await Geoprocessing.ExecuteToolAsync(toolName, exportParams, envSettings, cts.Token, null, flags);
                            }
                            catch (OperationCanceledException)
                            {
                                LogWarning("地理处理任务已被取消");
                            }
                            catch (Exception ex)
                            {
                                if (CancelRequested)
                                {
                                    LogWarning("因用户请求，操作已取消");
                                }
                                else
                                {
                                    LogError($"执行地理处理工具时出错: {ex.Message}");
                                    throw;
                                }
                            }
                            
                            // 如果任务完成且没有被取消
                            if (!CancelRequested)
                            {
                                if (gpResult == null)
                                {
                                    LogError("导出失败: GP 结果为空");
                                    LogError($"继续处理其他组...");
                                }
                                else if (gpResult.IsFailed)
                                {
                                    string errorMsg = "导出失败: " + gpResult.ErrorCode;
                                    LogError(errorMsg);
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
                                    // 不抛出异常，继续处理其他组
                                    LogError($"继续处理其他组...");
                                }
                                else
                                {
                                    foreach (var msg in gpResult.Messages)
                                    {
                                        if (msg.Type == GPMessageType.Warning)
                                            LogWarning($"GP警告: {msg.Text}");
                                    }
                                    LogInfo($"导出成功: {groupValue}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // 如果是由取消导致的异常，则不提示为错误
                            if (CancelRequested)
                            {
                                LogWarning($"由于用户取消，停止当前导出操作");
                            }
                            else
                            {
                                string errorMsg = $"导出要素出错: {ex.Message}";
                                LogError(errorMsg);
                                // 不抛出异常，继续处理其他组
                                LogError($"继续处理其他组...");
                            }
                        }
                    }
                    
                    // 完成
                    LogInfo($"===== 批量裁剪完成 - {DateTime.Now} =====");
                    LogInfo($"共导出 {total} 个分组至 {OutputFolder}");
                    
                    System.Windows.Application.Current.Dispatcher.Invoke(() => 
                    {
                        StatusMessage = $"导出完成! 共导出 {total} 个分组。";
                        Progress = 100; // 设置进度为100%
                        // 重置处理状态
                        IsProcessing = false;
                    });
                }
                catch (Exception ex)
                {
                    string errorMsg = $"处理出错: {ex.Message}";
                    LogError(errorMsg);
                    LogError($"===== 批量裁剪中断 - {DateTime.Now} =====");
                    
                    System.Windows.Application.Current.Dispatcher.Invoke(() => 
                    {
                        StatusMessage = errorMsg;
                        IsProgressIndeterminate = false; // 停止不确定进度状态
                        Progress = 0; // 重置进度
                        // 重置处理状态
                        IsProcessing = false;
                    });
                }
                finally
                {
                    // 即使取消也执行此代码
                    if (CancelRequested)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() => 
                        {
                            LogInfo($"===== 批量裁剪已取消 - {DateTime.Now} =====");
                            StatusMessage = "操作已取消";
                            IsProgressIndeterminate = false;
                            IsProcessing = false;
                        });
                    }
                }
            });
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