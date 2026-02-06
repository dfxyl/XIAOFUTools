using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using XIAOFUTools.Common;

namespace XIAOFUTools.Tools.GapCheck
{
    /// <summary>
    /// 缝隙检查工具停靠窗格视图模型
    /// </summary>
    internal class GapCheckDockPaneViewModel : PropertyChangedBase
    {
        private const string _dockPaneID = "XIAOFUTools_GapCheckDockPane";

        #region 私有字段
        private ObservableCollection<FeatureLayer> _polygonLayers;
        private FeatureLayer _selectedPolygonLayer;
        private string _tolerance = "0.001";
        private string _outputPath = "";
        private string _logContent = "";
        private string _statusMessage = "准备就绪";
        private int _progress = 0;
        private bool _isProgressIndeterminate = false;
        private bool _isProcessing = false;
        private CancellationTokenSource _cancellationTokenSource;
        #endregion

        #region 构造函数
        public GapCheckDockPaneViewModel()
        {
            InitializeCommands();
            InitializeData();
        }
        #endregion

        #region 属性
        /// <summary>
        /// 面要素图层列表
        /// </summary>
        public ObservableCollection<FeatureLayer> PolygonLayers
        {
            get => _polygonLayers;
            set => SetProperty(ref _polygonLayers, value);
        }

        /// <summary>
        /// 选中的面要素图层
        /// </summary>
        public FeatureLayer SelectedPolygonLayer
        {
            get => _selectedPolygonLayer;
            set
            {
                SetProperty(ref _selectedPolygonLayer, value);
                UpdateOutputPath();
                NotifyPropertyChanged(nameof(CanProcess));
            }
        }

        /// <summary>
        /// 检查容差值（米）- 工具执行精度
        /// </summary>
        public string Tolerance
        {
            get => _tolerance;
            set => SetProperty(ref _tolerance, value);
        }

        /// <summary>
        /// 输出路径
        /// </summary>
        public string OutputPath
        {
            get => _outputPath;
            set
            {
                // 使用通用工具规范化输出路径（自动识别 GDB/SHP）
                var defName = SelectedPolygonLayer != null ? $"{SelectedPolygonLayer.Name}_缝隙" : "缝隙";
                var normalized = OutputDatasetUtils.NormalizeOutputPath(value, defName);
                SetProperty(ref _outputPath, normalized);
            }
        }

        /// <summary>
        /// 日志内容
        /// </summary>
        public string LogContent
        {
            get => _logContent;
            set => SetProperty(ref _logContent, value);
        }

        /// <summary>
        /// 状态消息
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        /// <summary>
        /// 进度值
        /// </summary>
        public int Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        /// <summary>
        /// 进度条是否为不确定状态
        /// </summary>
        public bool IsProgressIndeterminate
        {
            get => _isProgressIndeterminate;
            set => SetProperty(ref _isProgressIndeterminate, value);
        }

        /// <summary>
        /// 是否正在处理
        /// </summary>
        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                SetProperty(ref _isProcessing, value);
                NotifyPropertyChanged(nameof(CanProcess));
            }
        }

        /// <summary>
        /// 是否可以开始处理
        /// </summary>
        public bool CanProcess => !IsProcessing && SelectedPolygonLayer != null && !string.IsNullOrWhiteSpace(OutputPath);
        #endregion

        #region 命令
        public ICommand BrowseOutputCommand { get; private set; }
        public ICommand RunCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }
        public ICommand ShowHelpCommand { get; private set; }
        public ICommand RefreshLayersCommand { get; private set; }
        #endregion

        #region 初始化方法
        private void InitializeCommands()
        {
            try
            {
                BrowseOutputCommand = new RelayCommand(BrowseOutput);
                RunCommand = new RelayCommand(async () => await RunGapCheck(), () => CanProcess);
                CancelCommand = new RelayCommand(CancelProcess);
                ShowHelpCommand = new RelayCommand(ShowHelp);
                RefreshLayersCommand = new RelayCommand(RefreshLayers);
                
                AddLog("命令初始化完成");
            }
            catch (Exception ex)
            {
                AddLog($"命令初始化失败: {ex.Message}");
            }
        }

        private void InitializeData()
        {
            // 确保所有字符串属性都有初始值
            if (string.IsNullOrEmpty(_logContent))
                _logContent = "";
            if (string.IsNullOrEmpty(_outputPath))
                _outputPath = "";
            if (string.IsNullOrEmpty(_statusMessage))
                _statusMessage = "准备就绪";
                
            PolygonLayers = new ObservableCollection<FeatureLayer>();
            AddLog("缝隙检查工具已启动");
        }

        /// <summary>
        /// 初始化界面
        /// </summary>
        public void Initialize()
        {
            LoadPolygonLayers();
        }

        /// <summary>
        /// 刷新图层列表
        /// </summary>
        public void RefreshLayers()
        {
            try
            {
                AddLog("正在刷新图层列表...");
                LoadPolygonLayers();
                AddLog("图层列表刷新完成");
            }
            catch (Exception ex)
            {
                AddLog($"刷新图层列表失败: {ex.Message}");
            }
        }
        #endregion

        #region 私有方法
        /// <summary>
        /// 加载面要素图层
        /// </summary>
        private async void LoadPolygonLayers()
        {
            try
            {
                var layers = await QueuedTask.Run(() =>
                {
                    var map = MapView.Active?.Map;
                    if (map == null) return new List<FeatureLayer>();
                    return map.GetLayersAsFlattenedList()
                        ?.OfType<FeatureLayer>()
                        .Where(fl => fl != null && fl.ShapeType == esriGeometryType.esriGeometryPolygon)
                        .ToList() ?? new List<FeatureLayer>();
                });

                // 确保PolygonLayers不为null
                if (PolygonLayers == null)
                {
                    PolygonLayers = new ObservableCollection<FeatureLayer>();
                }
                else
                {
                    PolygonLayers.Clear();
                }

                if (layers.Count == 0)
                {
                    AddLog("当前没有活动地图");
                    return;
                }

                foreach (var layer in layers)
                {
                    PolygonLayers.Add(layer);
                }

                AddLog($"已加载 {PolygonLayers.Count} 个面要素图层");

                // 如果有图层，默认选择第一个
                if (PolygonLayers.Count > 0)
                {
                    SelectedPolygonLayer = PolygonLayers[0];
                }

                // 设置默认输出路径
                UpdateOutputPath();
            }
            catch (Exception ex)
            {
                AddLog($"加载图层时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 浏览输出路径
        /// </summary>
        private void BrowseOutput()
        {
            try
            {
                var saveItemDialog = new SaveItemDialog
                {
                    Title = "选择输出位置",
                    OverwritePrompt = true,
                    DefaultExt = "shp",
                    Filter = ItemFilters.FeatureClasses_All
                };

                var initialLocation = GetProjectGDBPath();
                if (!string.IsNullOrEmpty(initialLocation))
                {
                    saveItemDialog.InitialLocation = initialLocation;
                }

                bool? dialogResult = saveItemDialog.ShowDialog();
                if (dialogResult == true)
                {
                    OutputPath = saveItemDialog.FilePath;
                }
            }
            catch (Exception ex)
            {
                AddLog($"选择输出位置出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取项目默认地理数据库路径
        /// </summary>
        private string GetProjectGDBPath()
        {
            try
            {
                var project = Project.Current;
                if (project != null)
                {
                    return project.DefaultGeodatabasePath;
                }
            }
            catch (Exception ex)
            {
                AddLog($"获取项目地理数据库路径失败: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// 更新输出路径
        /// </summary>
        private void UpdateOutputPath()
        {
            string projectGDB = GetProjectGDBPath();
            string outputName;

            if (SelectedPolygonLayer != null)
            {
                outputName = $"{SelectedPolygonLayer.Name}_缝隙";
            }
            else
            {
                outputName = "缝隙";
            }

            if (!string.IsNullOrEmpty(projectGDB))
            {
                OutputPath = Path.Combine(projectGDB, outputName);
            }
            else
            {
                OutputPath = outputName;
            }
        }

        /// <summary>
        /// 添加日志
        /// </summary>
        private void AddLog(string message)
        {
            try
            {
                if (string.IsNullOrEmpty(message))
                    return;
                    
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                
                // 确保LogContent不为null
                if (LogContent == null)
                    LogContent = "";
                    
                LogContent += $"[{timestamp}] {message}\r\n";
            }
            catch (Exception ex)
            {
                // 如果日志记录失败，至少不要让整个应用崩溃
                System.Diagnostics.Debug.WriteLine($"AddLog failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 执行缝隙检查
        /// </summary>
        private async Task RunGapCheck()
        {
            if (SelectedPolygonLayer == null)
            {
                AddLog("请选择面要素图层");
                return;
            }

            if (string.IsNullOrWhiteSpace(OutputPath))
            {
                AddLog("请选择输出路径");
                return;
            }

            if (!double.TryParse(Tolerance, out double tolerance) || tolerance < 0)
            {
                AddLog("请输入有效的容差值");
                return;
            }

            try
            {
                IsProcessing = true;
                IsProgressIndeterminate = true;
                StatusMessage = "正在检查缝隙...";
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();

                AddLog("开始执行缝隙检查...");
                AddLog($"输入图层: {SelectedPolygonLayer.Name}");
                AddLog($"执行容差值: {tolerance} 米");
                AddLog($"输出路径: {OutputPath}");

                await QueuedTask.Run(async () =>
                {
                    await ProcessGapCheck(tolerance, _cancellationTokenSource.Token);
                });

                if (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    StatusMessage = "缝隙检查完成";
                    AddLog("缝隙检查完成！");
                }
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "操作已取消";
                AddLog("操作已被用户取消");
            }
            catch (Exception ex)
            {
                StatusMessage = "处理失败";
                AddLog($"处理过程中出错: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
                IsProgressIndeterminate = false;
                Progress = 0;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        /// <summary>
        /// 处理缝隙检查核心逻辑
        /// </summary>
        private async Task ProcessGapCheck(double tolerance, CancellationToken cancellationToken)
        {
            try
            {
                // 使用地理处理工具进行缝隙检查
                await RunGapCheckGeoprocessing(tolerance, cancellationToken);
            }
            catch (Exception ex)
            {
                AddLog($"缝隙检查处理失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 使用地理处理工具执行缝隙检查
        /// </summary>
        private async Task RunGapCheckGeoprocessing(double tolerance, CancellationToken cancellationToken)
        {
            string tempWorkspace = null;
            
            try
            {
                AddLog("正在准备缝隙检查...");
                
                // 验证输入图层
                if (SelectedPolygonLayer == null)
                {
                    AddLog("输入图层无效");
                    return;
                }

                // 安全获取图层名称
                string inputLayerName = null;
                try
                {
                    inputLayerName = SelectedPolygonLayer.Name;
                    if (string.IsNullOrEmpty(inputLayerName))
                    {
                        AddLog("无法获取图层名称");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"获取图层名称失败: {ex.Message}");
                    return;
                }
                
                AddLog($"输入图层: {inputLayerName}");
                
                // 创建临时工作空间路径
                tempWorkspace = Path.Combine(Path.GetTempPath(), "GapCheck_" + Guid.NewGuid().ToString("N")[..8]);
                Directory.CreateDirectory(tempWorkspace);
                
                string dissolvedPath = Path.Combine(tempWorkspace, "dissolved_features.shp");
                string gapsPath = Path.Combine(tempWorkspace, "gaps.shp");
                string finalGapsPath = Path.Combine(tempWorkspace, "final_gaps.shp");
                
                // 步骤1: 创建融合图层
                AddLog("正在融合所有面要素...");
                
                try
                {
                    var dissolveParams = Geoprocessing.MakeValueArray(
                        inputLayerName,
                        dissolvedPath,
                        "", // 空字符串表示不按字段分组
                        "", // 空字符串表示不统计字段
                        "MULTI_PART", // 允许多部件要素，保留洞
                        "DISSOLVE_LINES" // 融合线
                    );
                    
                    var dissolveResult = await Geoprocessing.ExecuteToolAsync("Dissolve_management", dissolveParams, null, cancellationToken);
                    if (dissolveResult.IsFailed)
                    {
                        AddLog($"融合面要素失败:");
                        foreach (var msg in dissolveResult.Messages)
                        {
                            AddLog($"  - {msg.Text}");
                        }
                        return;
                    }
                    AddLog("面要素融合完成");
                }
                catch (Exception ex)
                {
                    AddLog($"融合面要素时出现异常: {ex.Message}");
                    return;
                }

                // 检查取消请求
                if (cancellationToken.IsCancellationRequested)
                {
                    AddLog("操作已取消");
                    return;
                }

                // 步骤2: 使用要素转面工具提取洞，使用容差参数
                AddLog($"正在提取洞和缝隙（容差: {tolerance}米）...");
                
                try
                {
                    var featureToPolygonParams = Geoprocessing.MakeValueArray(
                        dissolvedPath,
                        gapsPath,
                        $"{tolerance} Meters", // 使用容差参数
                        "ATTRIBUTES", // 保留属性
                        "" // 空字符串表示不使用标签要素
                    );
                    
                    var featureToPolygonResult = await Geoprocessing.ExecuteToolAsync("FeatureToPolygon_management", featureToPolygonParams, null, cancellationToken);
                    if (featureToPolygonResult.IsFailed)
                    {
                        AddLog($"提取洞失败:");
                        foreach (var msg in featureToPolygonResult.Messages)
                        {
                            AddLog($"  - {msg.Text}");
                        }
                        return;
                    }
                    AddLog("洞和缝隙提取完成");
                }
                catch (Exception ex)
                {
                    AddLog($"提取洞时出现异常: {ex.Message}");
                    return;
                }

                // 检查取消请求
                if (cancellationToken.IsCancellationRequested)
                {
                    AddLog("操作已取消");
                    return;
                }

                // 步骤3: 从结果中移除原始面要素，只保留洞
                AddLog("正在过滤出纯洞要素...");
                
                try
                {
                    var eraseParams = Geoprocessing.MakeValueArray(
                        gapsPath,
                        dissolvedPath,
                        finalGapsPath,
                        $"{tolerance} Meters" // 使用容差参数
                    );
                    
                    var eraseResult = await Geoprocessing.ExecuteToolAsync("Erase_analysis", eraseParams, null, cancellationToken);
                    if (eraseResult.IsFailed)
                    {
                        AddLog($"过滤洞要素失败:");
                        foreach (var msg in eraseResult.Messages)
                        {
                            AddLog($"  - {msg.Text}");
                        }
                        return;
                    }
                    AddLog("纯洞要素过滤完成");
                }
                catch (Exception ex)
                {
                    AddLog($"过滤洞要素时出现异常: {ex.Message}");
                    return;
                }

                // 检查取消请求
                if (cancellationToken.IsCancellationRequested)
                {
                    AddLog("操作已取消");
                    return;
                }

                // 步骤4: 添加面积字段
                AddLog("正在计算缝隙面积...");
                
                try
                {
                    // 检查输出文件是否存在要素
                    var checkParams = Geoprocessing.MakeValueArray(finalGapsPath);
                    var checkResult = await Geoprocessing.ExecuteToolAsync("GetCount_management", checkParams);
                    
                    if (checkResult.ReturnValue == "0")
                    {
                        AddLog("未发现缝隙，创建空的输出要素类");
                        
                        // 创建空的输出要素类
                        var createParams = Geoprocessing.MakeValueArray(
                            Path.GetDirectoryName(OutputPath),
                            Path.GetFileNameWithoutExtension(OutputPath),
                            "POLYGON",
                            finalGapsPath // 使用模板
                        );
                        await Geoprocessing.ExecuteToolAsync("CreateFeatureclass_management", createParams);
                        
                        AddLog("缝隙检查完成 - 未发现缝隙");
                        return;
                    }
                    
                    var addFieldParams = Geoprocessing.MakeValueArray(
                        finalGapsPath,
                        "GAP_AREA",
                        "DOUBLE",
                        "", // 精度
                        "", // 小数位数
                        "", // 长度
                        "缝隙面积", // 别名
                        "NULLABLE", // 可空
                        "NON_REQUIRED" // 非必需
                    );
                    await Geoprocessing.ExecuteToolAsync("AddField_management", addFieldParams);
                    
                    var calcFieldParams = Geoprocessing.MakeValueArray(
                        finalGapsPath,
                        "GAP_AREA",
                        "!shape.area!",
                        "PYTHON3"
                    );
                    await Geoprocessing.ExecuteToolAsync("CalculateField_management", calcFieldParams);
                    
                    AddLog("缝隙面积计算完成");
                }
                catch (Exception ex)
                {
                    AddLog($"计算缝隙面积时出现异常: {ex.Message}");
                    // 继续执行，不中断流程
                }

                // 步骤5: 复制最终结果到输出位置
                AddLog("正在复制结果到输出位置...");
                
                try
                {
                    // 使用通用工具检查输出路径是否存在，并提示覆盖
                    var defName = SelectedPolygonLayer != null ? $"{SelectedPolygonLayer.Name}_缝隙" : "缝隙";
                    var info = OutputDatasetUtils.ParseOutputPath(OutputPath, defName);
                    
                    if (OutputDatasetUtils.Exists(info))
                    {
                        bool overwrite = false;
                        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                        {
                            var msg = info.IsGdb
                                ? $"目标要素类已存在：{info.CatalogPath}。是否覆盖？"
                                : $"目标Shapefile已存在：{info.CatalogPath}。是否覆盖？";
                            var result = System.Windows.MessageBox.Show(msg, "覆盖确认", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
                            overwrite = result == System.Windows.MessageBoxResult.Yes;
                        });
                        if (!overwrite)
                        {
                            AddLog("用户取消覆盖，操作已中止。");
                            return;
                        }

                        try
                        {
                            await OutputDatasetUtils.DeleteIfExistsAsync(info);
                            AddLog($"删除已存在的数据集: {info.CatalogPath}");
                        }
                        catch (Exception delEx)
                        {
                            AddLog($"删除已有数据集失败: {delEx.Message}");
                        }
                    }
                    
                    var copyParams = Geoprocessing.MakeValueArray(
                        finalGapsPath,
                        OutputPath
                    );
                    
                    var copyResult = await Geoprocessing.ExecuteToolAsync("CopyFeatures_management", copyParams, null, cancellationToken);
                    if (copyResult.IsFailed)
                    {
                        AddLog($"复制缝隙要素失败:");
                        foreach (var msg in copyResult.Messages)
                        {
                            AddLog($"  - {msg.Text}");
                        }
                        return;
                    }
                    AddLog("结果复制完成");
                }
                catch (Exception ex)
                {
                    AddLog($"复制结果时出现异常: {ex.Message}");
                    return;
                }

                // 添加额外字段
                try
                {
                    await AddGapCheckFields(OutputPath, tolerance);
                }
                catch (Exception ex)
                {
                    AddLog($"添加额外字段时出现异常: {ex.Message}");
                    // 不中断流程
                }

                AddLog("缝隙检查完成！");
            }
            catch (OperationCanceledException)
            {
                AddLog("操作已被取消");
                throw;
            }
            catch (Exception ex)
            {
                AddLog($"地理处理执行失败: {ex.Message}");
                AddLog($"异常详情: {ex.ToString()}");
                throw;
            }
            finally
            {
                // 清理资源
                if (!string.IsNullOrEmpty(tempWorkspace))
                {
                    try
                    {
                        // 先移除可能显示的临时图层
                        await RemoveTemporaryLayersFromMap();
                        
                        // 然后清理临时文件
                        await CleanupTemporaryFiles(tempWorkspace);
                    }
                    catch (Exception cleanupEx)
                    {
                        AddLog($"清理资源时出现异常: {cleanupEx.Message}");
                    }
                }
            }
        }



        /// <summary>
        /// 为输出要素类添加缝隙检查相关字段
        /// </summary>
        private async Task AddGapCheckFields(string outputPath, double tolerance)
        {
            try
            {
                AddLog("正在添加缝隙检查相关字段...");
                
                // 检查输出路径是否有效
                if (string.IsNullOrEmpty(outputPath))
                {
                    AddLog("输出路径无效，跳过字段添加");
                    return;
                }

                // 添加缝隙ID字段
                try
                {
                    var addFieldParams1 = Geoprocessing.MakeValueArray(
                        outputPath,
                        "GAP_ID",
                        "LONG",
                        "", // 精度
                        "", // 小数位数
                        "", // 长度
                        "缝隙编号", // 别名
                        "NULLABLE", // 可空
                        "NON_REQUIRED" // 非必需
                    );
                    var result1 = await Geoprocessing.ExecuteToolAsync("AddField_management", addFieldParams1);
                    if (result1.IsFailed)
                    {
                        AddLog("添加GAP_ID字段失败，可能字段已存在");
                    }
                    else
                    {
                        AddLog("GAP_ID字段添加成功");
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"添加GAP_ID字段时出现异常: {ex.Message}");
                }

                // 添加容差字段
                try
                {
                    var addFieldParams2 = Geoprocessing.MakeValueArray(
                        outputPath,
                        "TOLERANCE",
                        "DOUBLE",
                        "", // 精度
                        "", // 小数位数
                        "", // 长度
                        "检查容差", // 别名
                        "NULLABLE", // 可空
                        "NON_REQUIRED" // 非必需
                    );
                    var result2 = await Geoprocessing.ExecuteToolAsync("AddField_management", addFieldParams2);
                    if (result2.IsFailed)
                    {
                        AddLog("添加TOLERANCE字段失败，可能字段已存在");
                    }
                    else
                    {
                        AddLog("TOLERANCE字段添加成功");
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"添加TOLERANCE字段时出现异常: {ex.Message}");
                }

                // 计算缝隙ID
                try
                {
                    var calcFieldParams1 = Geoprocessing.MakeValueArray(
                        outputPath,
                        "GAP_ID",
                        "!OBJECTID!",
                        "PYTHON3"
                    );
                    var result3 = await Geoprocessing.ExecuteToolAsync("CalculateField_management", calcFieldParams1);
                    if (result3.IsFailed)
                    {
                        AddLog("计算GAP_ID字段失败");
                    }
                    else
                    {
                        AddLog("GAP_ID字段计算完成");
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"计算GAP_ID字段时出现异常: {ex.Message}");
                }

                // 设置容差值（执行精度）
                try
                {
                    var calcFieldParams2 = Geoprocessing.MakeValueArray(
                        outputPath,
                        "TOLERANCE",
                        tolerance.ToString(),
                        "PYTHON3"
                    );
                    var result4 = await Geoprocessing.ExecuteToolAsync("CalculateField_management", calcFieldParams2);
                    if (result4.IsFailed)
                    {
                        AddLog("设置TOLERANCE字段值失败");
                    }
                    else
                    {
                        AddLog("TOLERANCE字段值设置完成");
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"设置TOLERANCE字段值时出现异常: {ex.Message}");
                }

                AddLog("缝隙检查相关字段处理完成");
            }
            catch (Exception ex)
            {
                AddLog($"添加字段过程中出现异常: {ex.Message}");
            }
        }



        /// <summary>
        /// 改进的临时文件清理方法
        /// </summary>
        private async Task CleanupTemporaryFiles(string tempWorkspace)
        {
            try
            {
                AddLog("正在清理临时文件...");
                
                if (!Directory.Exists(tempWorkspace))
                {
                    return;
                }

                // 等待一段时间，让ArcGIS释放文件句柄
                await Task.Delay(1000);

                // 尝试多次删除，处理文件被占用的情况
                int maxRetries = 5;
                int retryCount = 0;
                bool deleted = false;

                while (retryCount < maxRetries && !deleted)
                {
                    try
                    {
                        // 首先尝试删除所有文件
                        var files = Directory.GetFiles(tempWorkspace, "*", SearchOption.AllDirectories);
                        foreach (var file in files)
                        {
                            try
                            {
                                File.SetAttributes(file, FileAttributes.Normal);
                                File.Delete(file);
                            }
                            catch (Exception fileEx)
                            {
                                AddLog($"删除文件失败: {Path.GetFileName(file)} - {fileEx.Message}");
                            }
                        }

                        // 然后删除目录
                        Directory.Delete(tempWorkspace, true);
                        deleted = true;
                        AddLog("临时文件清理完成");
                    }
                    catch (Exception ex)
                    {
                        retryCount++;
                        AddLog($"清理临时文件失败 (尝试 {retryCount}/{maxRetries}): {ex.Message}");
                        
                        if (retryCount < maxRetries)
                        {
                            // 等待后重试
                            await Task.Delay(2000);
                        }
                    }
                }

                if (!deleted)
                {
                    AddLog($"无法完全清理临时文件夹: {tempWorkspace}");
                    AddLog("这些文件将在系统重启后自动清理");
                }
            }
            catch (Exception ex)
            {
                AddLog($"清理临时文件时出现异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 从地图中移除临时图层
        /// </summary>
        private async Task RemoveTemporaryLayersFromMap()
        {
            try
            {
                AddLog("正在检查并移除临时图层...");
                
                // 使用更安全的方式处理图层移除
                await Task.Run(async () =>
                {
                    try
                    {
                        // 等待一段时间，让ArcGIS完成内部处理
                        await Task.Delay(2000);
                        
                        await QueuedTask.Run(() =>
                        {
                            try
                            {
                                // 检查MapView和Map是否有效
                                var mapView = MapView.Active;
                                if (mapView == null)
                                {
                                    AddLog("当前没有活动的地图视图");
                                    return;
                                }

                                var map = mapView.Map;
                                if (map == null)
                                {
                                    AddLog("当前没有活动地图");
                                    return;
                                }

                                // 使用更安全的方式获取图层列表
                                List<Layer> layersToRemove = new List<Layer>();
                                
                                try
                                {
                                    // 分别获取不同类型的图层
                                    var featureLayers = map.GetLayersAsFlattenedList().OfType<FeatureLayer>().ToList();
                                    
                                    foreach (var layer in featureLayers)
                                    {
                                        try
                                        {
                                            if (layer == null) continue;
                                            
                                            // 使用更安全的方式检查图层名称
                                            string layerName = GetSafeLayerName(layer);
                                            if (string.IsNullOrEmpty(layerName)) continue;

                                            // 检查是否为临时图层
                                            if (IsTemporaryLayer(layerName))
                                            {
                                                layersToRemove.Add(layer);
                                                AddLog($"发现临时图层: {layerName}");
                                            }
                                        }
                                        catch (Exception layerEx)
                                        {
                                            AddLog($"检查图层时出错: {layerEx.Message}");
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    AddLog($"获取图层列表失败: {ex.Message}");
                                    return;
                                }

                                // 移除找到的临时图层
                                if (layersToRemove.Count > 0)
                                {
                                    AddLog($"准备移除 {layersToRemove.Count} 个临时图层");
                                    
                                    foreach (var layer in layersToRemove)
                                    {
                                        try
                                        {
                                            if (layer != null && map != null)
                                            {
                                                string layerName = GetSafeLayerName(layer);
                                                
                                                // 使用更安全的移除方式
                                                RemoveLayerSafely(map, layer, layerName);
                                            }
                                        }
                                        catch (Exception layerEx)
                                        {
                                            AddLog($"移除单个图层时出错: {layerEx.Message}");
                                        }
                                    }
                                }
                                else
                                {
                                    AddLog("未发现需要移除的临时图层");
                                }
                            }
                            catch (Exception innerEx)
                            {
                                AddLog($"图层移除过程中出现异常: {innerEx.Message}");
                            }
                        });
                    }
                    catch (Exception taskEx)
                    {
                        AddLog($"执行图层移除任务时出错: {taskEx.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                AddLog($"移除临时图层时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 安全获取图层名称
        /// </summary>
        private string GetSafeLayerName(Layer layer)
        {
            try
            {
                if (layer == null) return null;
                return layer.Name;
            }
            catch (Exception ex)
            {
                AddLog($"获取图层名称时出错: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 检查是否为临时图层
        /// </summary>
        private bool IsTemporaryLayer(string layerName)
        {
            if (string.IsNullOrEmpty(layerName)) return false;
            
            return layerName.Contains("dissolved_features") || 
                   layerName.Contains("gaps") || 
                   layerName.Contains("final_gaps") ||
                   layerName.StartsWith("GapCheck_");
        }

        /// <summary>
        /// 安全移除图层
        /// </summary>
        private void RemoveLayerSafely(Map map, Layer layer, string layerName)
        {
            try
            {
                if (map == null || layer == null) return;
                
                // 尝试移除图层
                map.RemoveLayer(layer);
                AddLog($"已从地图中移除临时图层: {layerName ?? "未知图层"}");
            }
            catch (System.Runtime.InteropServices.COMException comEx)
            {
                AddLog($"移除图层时出现COM异常: {layerName ?? "未知图层"} - {comEx.Message}");
                // COM异常通常表示图层已经无效，可以忽略
            }
            catch (System.NullReferenceException nullEx)
            {
                AddLog($"移除图层时出现空引用异常: {layerName ?? "未知图层"} - {nullEx.Message}");
                // 空引用异常表示对象已经被释放，可以忽略
            }
            catch (Exception ex)
            {
                AddLog($"移除图层时出现其他异常: {layerName ?? "未知图层"} - {ex.Message}");
            }
        }

        /// <summary>
        /// 取消处理
        /// </summary>
        private void CancelProcess()
        {
            _cancellationTokenSource?.Cancel();
            AddLog("正在取消操作...");
        }

        /// <summary>
        /// 显示帮助
        /// </summary>
        private void ShowHelp()
        {
            try
            {
                AddLog("显示帮助信息");
                
                string helpMessage = @"缝隙检查工具使用说明：

1. 选择面要素图层：选择需要检查缝隙的面要素图层
2. 设置检查容差值：设置工具执行的距离容差（米），用于控制检测精度
3. 选择输出路径：指定缝隙要素的输出位置
4. 点击开始按钮执行检查

工具原理：
- 融合所有面要素，消除重叠和相邻边界，保留洞
- 使用要素转面工具提取所有面要素（包括洞），应用容差参数
- 从结果中擦除原始融合要素，只保留洞
- 计算洞的面积并输出缝隙要素

输出结果包含以下字段：
- GAP_ID：缝隙编号
- GAP_AREA：缝隙面积（平方米）
- TOLERANCE：使用的容差值（米）

注意：容差值是工具执行精度，较小的值检测更精细的缝隙";

                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(helpMessage, "缝隙检查工具帮助", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AddLog($"显示帮助失败: {ex.Message}");
            }
        }
        #endregion
    }
}