using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using XIAOFUTools.Common;

namespace XIAOFUTools.Tools.Edit.Boundary.BoundaryPointLineGenerator
{
    /// <summary>
    /// 界址点线生成 ViewModel
    /// - 选择面图层
    /// - 选择生成类型（点/线）
    /// - 输出到默认工程GDB，命名为：<图层名>_JZD 或 <图层名>_JZX
    /// - 字段结构参考 TD/T 1066-2021（按截图常用字段）
    /// </summary>
    internal class BoundaryPointLineGeneratorDockPaneViewModel : PropertyChangedBase
    {
        #region 属性
        private bool _cancelRequested;
        public bool CancelRequested { get => _cancelRequested; set => SetProperty(ref _cancelRequested, value); }

        private bool _isProcessing;
        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                SetProperty(ref _isProcessing, value);
                NotifyPropertyChanged(() => CanProcess);
                // 更新命令可执行状态
                (_runCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (_cancelCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
        public bool CanProcess => !IsProcessing && SelectedPolygonLayer != null && HasValidOutputPath;

        private ObservableCollection<FeatureLayer> _polygonLayers = new();
        public ObservableCollection<FeatureLayer> PolygonLayers
        {
            get => _polygonLayers; set => SetProperty(ref _polygonLayers, value);
        }

        private FeatureLayer _selectedPolygonLayer;
        public FeatureLayer SelectedPolygonLayer
        {
            get => _selectedPolygonLayer;
            set
            {
                SetProperty(ref _selectedPolygonLayer, value);
                LoadAvailableFields();
                UpdateOutputName();
                UpdateOutputPaths();
                NotifyPropertyChanged(() => HasSelectedLayer);
                // 选中图层变化会影响 CanProcess
                NotifyPropertyChanged(() => CanProcess);
                (_runCommand as RelayCommand)?.RaiseCanExecuteChanged();
                // 同步更新选择信息
                UpdateSelectionInfo();
            }
        }
        public bool HasSelectedLayer => SelectedPolygonLayer != null;

        public ObservableCollection<string> OutputTypes { get; } = new() { "界址点(JZD)", "界址线(JZX)", "同时生成(JZD+JZX)" };

        private string _selectedOutputType = "界址点(JZD)";
        public string SelectedOutputType
        {
            get => _selectedOutputType;
            set
            {
                SetProperty(ref _selectedOutputType, value);
                UpdateOutputName();
                UpdateOutputPaths();
                NotifyPropertyChanged(() => ShowJZDSettings);
                NotifyPropertyChanged(() => ShowJZXSettings);
                NotifyPropertyChanged(() => CanProcess);
                (_runCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        // 宗地/宗海代码来源字段选择
        private ObservableCollection<string> _availableFields = new();
        public ObservableCollection<string> AvailableFields { get => _availableFields; set => SetProperty(ref _availableFields, value); }

        private string _selectedCodeFieldName;
        public string SelectedCodeFieldName { get => _selectedCodeFieldName; set => SetProperty(ref _selectedCodeFieldName, value); }

        private string _defaultGdbPath;
        public string DefaultGdbPath
        {
            get => _defaultGdbPath;
            set
            {
                SetProperty(ref _defaultGdbPath, value);
                // 默认GDB路径变化会影响 CanProcess
                NotifyPropertyChanged(() => CanProcess);
                (_runCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        // 自定义输出路径（根据类型显示对应组件）
        private string _outputPathJZD;
        public string OutputPathJZD
        {
            get => _outputPathJZD;
            set
            {
                var defName = SelectedPolygonLayer != null ? $"{SelectedPolygonLayer.Name}_JZD" : "JZD";
                var normalized = OutputDatasetUtils.NormalizeOutputPath(value, defName);
                SetProperty(ref _outputPathJZD, normalized);
                NotifyPropertyChanged(() => CanProcess);
                (_runCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        private string _outputPathJZX;
        public string OutputPathJZX
        {
            get => _outputPathJZX;
            set
            {
                var defName = SelectedPolygonLayer != null ? $"{SelectedPolygonLayer.Name}_JZX" : "JZX";
                var normalized = OutputDatasetUtils.NormalizeOutputPath(value, defName);
                SetProperty(ref _outputPathJZX, normalized);
                NotifyPropertyChanged(() => CanProcess);
                (_runCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        private bool HasValidOutputPath
        {
            get
            {
                if (SelectedOutputType == null) return false;
                bool needJZD = SelectedOutputType.Contains("JZD");
                bool needJZX = SelectedOutputType.Contains("JZX");
                if (needJZD && string.IsNullOrWhiteSpace(OutputPathJZD)) return false;
                if (needJZX && string.IsNullOrWhiteSpace(OutputPathJZX)) return false;
                return true;
            }
        }

        private string _outputFeatureClassName;
        public string OutputFeatureClassName { get => _outputFeatureClassName; set => SetProperty(ref _outputFeatureClassName, value); }

        private string _statusMessage = "准备就绪";
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        private string _logContent = string.Empty;
        public string LogContent { get => _logContent; set => SetProperty(ref _logContent, value); }
        private readonly StringBuilder _logBuilder = new();

        private int _progress;
        public int Progress { get => _progress; set => SetProperty(ref _progress, value); }
        private bool _isProgressIndeterminate;
        public bool IsProgressIndeterminate { get => _isProgressIndeterminate; set => SetProperty(ref _isProgressIndeterminate, value); }
        
        // JZX：长度小数位设置
        private int _jzxLengthDecimals = 2;
        public int JZXLengthDecimals
        {
            get => _jzxLengthDecimals;
            set
            {
                var v = Math.Max(0, Math.Min(6, value));
                SetProperty(ref _jzxLengthDecimals, v);
            }
        }

        // JZX：YSDM 设置
        private string _jzxYSDM = string.Empty;
        public string JZXYSDM { get => _jzxYSDM; set => SetProperty(ref _jzxYSDM, value); }

        // JZD：YSDM 设置
        private string _jzdYSDM = string.Empty;
        public string JZDYSDM { get => _jzdYSDM; set => SetProperty(ref _jzdYSDM, value); }

        // JZD：JZDH 前缀
        private string _jzdhPrefix = string.Empty;
        public string JZDHPrefix { get => _jzdhPrefix; set => SetProperty(ref _jzdhPrefix, value); }

        // 设置项可见性：根据选择的输出类型自动显示
        public bool ShowJZDSettings => SelectedOutputType != null && SelectedOutputType.Contains("JZD");
        public bool ShowJZXSettings => SelectedOutputType != null && SelectedOutputType.Contains("JZX");
        #endregion

        #region 命令
        private ICommand _runCommand;
        public ICommand RunCommand => _runCommand ??= new RelayCommand(Execute, () => CanProcess);

        private ICommand _cancelCommand;
        public ICommand CancelCommand => _cancelCommand ??= new RelayCommand(() => { if (IsProcessing) { CancelRequested = true; StatusMessage = "正在取消..."; LogWarning("用户请求取消"); } }, () => IsProcessing);

        private ICommand _refreshLayersCommand;
        public ICommand RefreshLayersCommand => _refreshLayersCommand ??= new RelayCommand(() => LoadPolygonLayers());

        private ICommand _showHelpCommand;
        public ICommand ShowHelpCommand => _showHelpCommand ??= new RelayCommand(ShowHelp);

        private ICommand _browseOutputJZDCommand;
        public ICommand BrowseOutputJZDCommand => _browseOutputJZDCommand ??= new RelayCommand(() =>
        {
            try
            {
                var initial = !string.IsNullOrWhiteSpace(OutputPathJZD) ? OutputPathJZD : PathDialogUtils.GetProjectDefaultGdb();
                var picked = PathDialogUtils.PickSaveFeatureClassPath("选择 JZD 输出位置", initial);
                if (!string.IsNullOrWhiteSpace(picked)) OutputPathJZD = picked;
            }
            catch (Exception ex) { MessageBox.Show($"选择 JZD 输出位置失败: {ex.Message}", "错误"); }
        });

        private ICommand _browseOutputJZXCommand;
        public ICommand BrowseOutputJZXCommand => _browseOutputJZXCommand ??= new RelayCommand(() =>
        {
            try
            {
                var initial = !string.IsNullOrWhiteSpace(OutputPathJZX) ? OutputPathJZX : PathDialogUtils.GetProjectDefaultGdb();
                var picked = PathDialogUtils.PickSaveFeatureClassPath("选择 JZX 输出位置", initial);
                if (!string.IsNullOrWhiteSpace(picked)) OutputPathJZX = picked;
            }
            catch (Exception ex) { MessageBox.Show($"选择 JZX 输出位置失败: {ex.Message}", "错误"); }
        });
        #endregion

        public BoundaryPointLineGeneratorDockPaneViewModel()
        {
            DefaultGdbPath = GetProjectGDBPath();
            LoadPolygonLayers();
            UpdateOutputName();
            UpdateOutputPaths();

            // 订阅地图视图相关事件，自动刷新图层列表
            MapViewInitializedEvent.Subscribe((args) =>
            {
                try
                {
                    DefaultGdbPath = GetProjectGDBPath();
                    LoadPolygonLayers();
                    UpdateOutputName();
                    UpdateOutputPaths();
                }
                catch { }
            });
            ActiveMapViewChangedEvent.Subscribe((args) =>
            {
                try
                {
                    DefaultGdbPath = GetProjectGDBPath();
                    LoadPolygonLayers();
                    UpdateOutputName();
                    UpdateOutputPaths();
                }
                catch { }
            });
            // 订阅地图选择变化事件并初始化一次选择信息
            MapSelectionChangedEvent.Subscribe(OnMapSelectionChanged);
            UpdateSelectionInfo();
        }

        private string GetProjectGDBPath()
        {
            try { return Project.Current?.DefaultGeodatabasePath; }
            catch { return null; }
        }

        private void UpdateOutputName()
        {
            if (SelectedPolygonLayer == null)
            {
                OutputFeatureClassName = "输出";
                return;
            }

            var baseName = SelectedPolygonLayer.Name;
            if (SelectedOutputType.Contains("JZD") && SelectedOutputType.Contains("JZX"))
                OutputFeatureClassName = $"{baseName}_JZD / {baseName}_JZX";
            else
                OutputFeatureClassName = SelectedOutputType.Contains("JZD") ? $"{baseName}_JZD" : $"{baseName}_JZX";
        }

        private void UpdateOutputPaths()
        {
            try
            {
                var baseName = SelectedPolygonLayer?.Name;
                var projGdb = PathDialogUtils.GetProjectDefaultGdb();
                if (string.IsNullOrWhiteSpace(OutputPathJZD) && (SelectedOutputType?.Contains("JZD") ?? false) && !string.IsNullOrWhiteSpace(baseName))
                {
                    OutputPathJZD = string.IsNullOrWhiteSpace(projGdb) ? $"{baseName}_JZD" : System.IO.Path.Combine(projGdb, $"{baseName}_JZD");
                }
                if (string.IsNullOrWhiteSpace(OutputPathJZX) && (SelectedOutputType?.Contains("JZX") ?? false) && !string.IsNullOrWhiteSpace(baseName))
                {
                    OutputPathJZX = string.IsNullOrWhiteSpace(projGdb) ? $"{baseName}_JZX" : System.IO.Path.Combine(projGdb, $"{baseName}_JZX");
                }
            }
            catch { }
        }

        private void LoadPolygonLayers()
        {
            // 使用通用接口获取面图层列表
            Task.Run(async () =>
            {
                try
                {
                    var list = await LayerUtils.GetPolygonLayersAsync();
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        PolygonLayers.Clear();
                        foreach (var fl in list) PolygonLayers.Add(fl);
                        if (PolygonLayers.Count > 0) SelectedPolygonLayer = PolygonLayers[0];
                        StatusMessage = PolygonLayers.Count > 0 ? "请选择参数后开始生成" : "未找到面图层";
                    });
                }
                catch (Exception ex)
                {
                    System.Windows.Application.Current?.Dispatcher.Invoke(() => StatusMessage = $"加载图层失败: {ex.Message}");
                }
            });
        }

        // 选择集相关属性与逻辑
        private bool _useSelection = true;
        public bool UseSelection
        {
            get => _useSelection;
            set { SetProperty(ref _useSelection, value); }
        }

        private bool _hasSelection;
        public bool HasSelection
        {
            get => _hasSelection;
            set { SetProperty(ref _hasSelection, value); }
        }

        private int _selectedCount;
        public int SelectedCount
        {
            get => _selectedCount;
            set { SetProperty(ref _selectedCount, value); }
        }

        private string _selectionInfoText;
        public string SelectionInfoText
        {
            get => _selectionInfoText;
            set { SetProperty(ref _selectionInfoText, value); }
        }

        private void OnMapSelectionChanged(MapSelectionChangedEventArgs args)
        {
            UpdateSelectionInfo();
        }

        private void UpdateSelectionInfo()
        {
            // 使用通用接口获取选择集信息
            Task.Run(async () =>
            {
                var info = await SelectionUtils.GetSelectionInfoAsync(SelectedPolygonLayer);
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    UseSelection = SelectionUtils.RecommendUseSelection(UseSelection, info.HasSelection);
                    HasSelection = info.HasSelection;
                    SelectedCount = info.Count;
                    SelectionInfoText = info.InfoText;
                });
            });
        }

        private void LoadAvailableFields()
        {
            AvailableFields.Clear();
            if (SelectedPolygonLayer == null) return;
            QueuedTask.Run(() =>
            {
                try
                {
                    using var table = SelectedPolygonLayer.GetTable();
                    var fields = table?.GetDefinition()?.GetFields();
                    if (fields == null) return;
                    var texts = fields.Where(f => f.FieldType == FieldType.String).Select(f => f.Name).ToList();
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        foreach (var n in texts) AvailableFields.Add(n);
                        // 尝试默认匹配
                        SelectedCodeFieldName = AvailableFields.FirstOrDefault(n => n.Equals("ZDZHDM", StringComparison.OrdinalIgnoreCase) || n.Contains("ZDDM", StringComparison.OrdinalIgnoreCase)) ?? AvailableFields.FirstOrDefault();
                    });
                }
                catch (Exception ex)
                {
                    LogError($"读取字段失败: {ex.Message}");
                }
            });
        }

        private async void Execute()
        {
            if (IsProcessing) return;
            CancelRequested = false; IsProcessing = true; IsProgressIndeterminate = true; Progress = 0; ClearLog();
            StatusMessage = "正在生成...";
            try
            {
                await QueuedTask.Run(async () =>
                {
                    if (SelectedPolygonLayer == null) { LogError("未选择面图层"); return; }

                    var sr = LayerUtils.GetSpatialReference(SelectedPolygonLayer);
                    var baseName = SelectedPolygonLayer.Name;

                    bool genJZD = SelectedOutputType.Contains("JZD");
                    bool genJZX = SelectedOutputType.Contains("JZX");

                    // JZD
                    if (genJZD)
                    {
                        var infoJZD = OutputDatasetUtils.ParseOutputPath(OutputPathJZD, $"{baseName}_JZD");
                        if (OutputDatasetUtils.Exists(infoJZD))
                        {
                            bool overwrite = false;
                            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                            {
                                var msg = infoJZD.IsGdb ? $"目标要素类已存在：{infoJZD.CatalogPath}。是否覆盖？" : $"目标Shapefile已存在：{infoJZD.CatalogPath}。是否覆盖？";
                                var result = MessageBox.Show(msg, "覆盖确认", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
                                overwrite = result == System.Windows.MessageBoxResult.Yes;
                            });
                            if (!overwrite)
                            {
                                LogWarning("用户取消覆盖，操作已中止。");
                                return;
                            }
                            try { await OutputDatasetUtils.DeleteIfExistsAsync(infoJZD); LogInfo($"删除已存在的数据集: {infoJZD.CatalogPath}"); } catch (Exception delEx) { LogWarning($"删除已有数据集失败: {delEx.Message}"); }
                        }

                        var pathJZD = await OutputDatasetUtils.CreateFeatureClassAsync(infoJZD, "POINT", sr);
                        await AddJZDFields(pathJZD);
                        await GeneratePointFeatures(pathJZD);
                    }

                    // JZX
                    if (genJZX)
                    {
                        var infoJZX = OutputDatasetUtils.ParseOutputPath(OutputPathJZX, $"{baseName}_JZX");
                        if (OutputDatasetUtils.Exists(infoJZX))
                        {
                            bool overwrite = false;
                            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                            {
                                var msg = infoJZX.IsGdb ? $"目标要素类已存在：{infoJZX.CatalogPath}。是否覆盖？" : $"目标Shapefile已存在：{infoJZX.CatalogPath}。是否覆盖？";
                                var result = MessageBox.Show(msg, "覆盖确认", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
                                overwrite = result == System.Windows.MessageBoxResult.Yes;
                            });
                            if (!overwrite)
                            {
                                LogWarning("用户取消覆盖，操作已中止。");
                                return;
                            }
                            try { await OutputDatasetUtils.DeleteIfExistsAsync(infoJZX); LogInfo($"删除已存在的数据集: {infoJZX.CatalogPath}"); } catch (Exception delEx) { LogWarning($"删除已有数据集失败: {delEx.Message}"); }
                        }

                        var pathJZX = await OutputDatasetUtils.CreateFeatureClassAsync(infoJZX, "POLYLINE", sr);
                        await AddJZXFields(pathJZX);
                        await GenerateLineFeatures(pathJZX);
                    }

                    if (!CancelRequested)
                    {
                        LogInfo("生成完成");
                        System.Windows.Application.Current?.Dispatcher.Invoke(() => { Progress = 100; IsProgressIndeterminate = false; StatusMessage = "完成"; });
                    }
                });
            }
            catch (Exception ex)
            {
                LogError($"执行失败: {ex.Message}");
            }
            finally { IsProcessing = false; IsProgressIndeterminate = false; }
        }

        #region 字段创建
        private async Task AddJZDFields(string featureClassPath)
        {
            try
            {
                // 添加字段并设置中文别名
                await Geoprocessing.ExecuteToolAsync("AddField_management", Geoprocessing.MakeValueArray(featureClassPath, "BSM", "LONG", null, null, null, "标识码"));
                await Geoprocessing.ExecuteToolAsync("AddField_management", Geoprocessing.MakeValueArray(featureClassPath, "ZDZHDM", "TEXT", null, null, null, "宗地/宗海代码"));
                await Geoprocessing.ExecuteToolAsync("AddField_management", Geoprocessing.MakeValueArray(featureClassPath, "YSDM", "TEXT", null, null, 10, "要素代码"));
                await Geoprocessing.ExecuteToolAsync("AddField_management", Geoprocessing.MakeValueArray(featureClassPath, "JZDH", "TEXT", null, null, 10, "界址点号"));
                await Geoprocessing.ExecuteToolAsync("AddField_management", Geoprocessing.MakeValueArray(featureClassPath, "SXH", "LONG", null, null, null, "顺序号"));
                await Geoprocessing.ExecuteToolAsync("AddField_management", Geoprocessing.MakeValueArray(featureClassPath, "JBLX", "TEXT", null, null, 2, "界标类型"));
                await Geoprocessing.ExecuteToolAsync("AddField_management", Geoprocessing.MakeValueArray(featureClassPath, "JZDLX", "TEXT", null, null, 2, "界址点类型"));
                // 坐标字段修正为 XZBZ/YZBZ 并按测绘习惯反转 XY 写入（在生成时处理）
                await Geoprocessing.ExecuteToolAsync("AddField_management", Geoprocessing.MakeValueArray(featureClassPath, "XZBZ", "DOUBLE", 15, 3, null, "X坐标值"));
                await Geoprocessing.ExecuteToolAsync("AddField_management", Geoprocessing.MakeValueArray(featureClassPath, "YZBZ", "DOUBLE", 15, 3, null, "Y坐标值"));
                await Geoprocessing.ExecuteToolAsync("AddField_management", Geoprocessing.MakeValueArray(featureClassPath, "ZZBZ", "DOUBLE", 15, 3, null, "Z坐标值"));
            }
            catch (Exception ex) { LogError($"添加JZD字段失败: {ex.Message}"); }
        }
        private async Task AddJZXFields(string featureClassPath)
        {
            try
            {
                await Geoprocessing.ExecuteToolAsync("AddField_management", Geoprocessing.MakeValueArray(featureClassPath, "BSM", "LONG", null, null, null, "标识码"));
                await Geoprocessing.ExecuteToolAsync("AddField_management", Geoprocessing.MakeValueArray(featureClassPath, "ZDZHDM", "TEXT", null, null, null, "宗地/宗海代码"));
                await Geoprocessing.ExecuteToolAsync("AddField_management", Geoprocessing.MakeValueArray(featureClassPath, "YSDM", "TEXT", null, null, 10, "要素代码"));
                await Geoprocessing.ExecuteToolAsync("AddField_management", Geoprocessing.MakeValueArray(featureClassPath, "JZXCD", "DOUBLE", 15, 2, null, "界址线长度"));
                await Geoprocessing.ExecuteToolAsync("AddField_management", Geoprocessing.MakeValueArray(featureClassPath, "JZXLB", "TEXT", null, null, 2, "界址线类别"));
                await Geoprocessing.ExecuteToolAsync("AddField_management", Geoprocessing.MakeValueArray(featureClassPath, "JZXWZ", "TEXT", null, null, 1, "界址线位置"));
                await Geoprocessing.ExecuteToolAsync("AddField_management", Geoprocessing.MakeValueArray(featureClassPath, "JZXZ", "TEXT", null, null, 6, "界址线质"));
                await Geoprocessing.ExecuteToolAsync("AddField_management", Geoprocessing.MakeValueArray(featureClassPath, "QSJXYSBH", "TEXT", null, null, 30, "权属界线协议书编号"));
                await Geoprocessing.ExecuteToolAsync("AddField_management", Geoprocessing.MakeValueArray(featureClassPath, "QSJXYS", "BLOB", null, null, null, "权属界线协议书"));
                await Geoprocessing.ExecuteToolAsync("AddField_management", Geoprocessing.MakeValueArray(featureClassPath, "QSZYYSBH", "TEXT", null, null, 30, "权属争议原由书编号"));
                await Geoprocessing.ExecuteToolAsync("AddField_management", Geoprocessing.MakeValueArray(featureClassPath, "QSZYYS", "BLOB", null, null, null, "权属争议原由书"));
            }
            catch (Exception ex) { LogError($"添加JZX字段失败: {ex.Message}"); }
        }
        #endregion

        #region 生成要素
        private async Task GeneratePointFeatures(string outputFeatureClassPath)
        {
            try
            {
                using var inputTable = SelectedPolygonLayer.GetTable();
                // 计算总量：若使用选择集则为选择数量，否则为全部数量
                bool useSelection = UseSelection && SelectionUtils.GetSelectionCount(SelectedPolygonLayer) > 0;
                int total = 0;
                if (useSelection)
                {
                    total = SelectionUtils.GetSelectionCount(SelectedPolygonLayer);
                    LogInfo($"检测到选择集: {total} 个要素，将仅处理选择的要素。");
                }
                else
                {
                    using (var c = inputTable.Search()) { while (c.MoveNext()) total++; }
                    LogInfo($"未检测到选择集，将处理全部 {total} 个要素。");
                }
                int processed = 0; int globalBSM = 1;

                FeatureClass featureClass = null;
                Geodatabase gdb = null;
                FileSystemDatastore fsds = null;
                try
                {
                    // 打开输出要素类（GDB 或 Shapefile）
                    var catalogPath = outputFeatureClassPath;
                    if (catalogPath.EndsWith(".shp", StringComparison.OrdinalIgnoreCase))
                    {
                        var folder = Path.GetDirectoryName(catalogPath);
                        var shpName = Path.GetFileName(catalogPath);
                        var conn = new FileSystemConnectionPath(new Uri(folder), FileSystemDatastoreType.Shapefile);
                        fsds = new FileSystemDatastore(conn);
                        featureClass = fsds.OpenDataset<FeatureClass>(shpName);
                    }
                    else
                    {
                        int gdbIdx = catalogPath.IndexOf(".gdb", StringComparison.OrdinalIgnoreCase);
                        if (gdbIdx >= 0)
                        {
                            var gdbRoot = catalogPath.Substring(0, gdbIdx + 4);
                            var relative = catalogPath.Length > gdbIdx + 4 ? catalogPath.Substring(gdbIdx + 4).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : string.Empty;
                            gdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(gdbRoot)));
                            featureClass = gdb.OpenDataset<FeatureClass>(relative);
                        }
                        else
                        {
                            var workspace = Path.GetDirectoryName(catalogPath);
                            var featureClassName = Path.GetFileNameWithoutExtension(catalogPath);
                            bool isGdb = workspace.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase);
                            if (isGdb)
                            {
                                gdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(workspace)));
                                featureClass = gdb.OpenDataset<FeatureClass>(featureClassName);
                            }
                            else
                            {
                                var conn = new FileSystemConnectionPath(new Uri(workspace), FileSystemDatastoreType.Shapefile);
                                fsds = new FileSystemDatastore(conn);
                                featureClass = fsds.OpenDataset<FeatureClass>(featureClassName + ".shp");
                            }
                        }
                    }

                    var shapeField = featureClass.GetDefinition().GetShapeField();

                // 按范围获取游标（通用接口）
                using var cursor = SelectionUtils.GetSelectionOrAllCursor(SelectedPolygonLayer, useSelection, new QueryFilter(), false);
                while (cursor.MoveNext())
                {
                    if (CancelRequested) break;
                    var feature = cursor.Current as Feature;
                    var polygon = feature?.GetShape() as Polygon;
                    if (polygon == null) continue;

                    var zdzhdm = GetStringSafe(feature, SelectedCodeFieldName); // 宗地/宗海代码

                    int sxh = 1;
                    foreach (var part in polygon.Parts)
                    {
                        var vertices = new List<MapPoint>();
                        foreach (var seg in part)
                        {
                            // 收集端点，避免重复最后一个点
                            var sp = seg.StartPoint; var ep = seg.EndPoint;
                            if (vertices.Count == 0) vertices.Add(sp);
                            if (vertices.Count == 0 || !IsSamePoint(vertices[^1], ep)) vertices.Add(ep);
                        }
                        // 去掉闭合重复
                        if (vertices.Count > 1 && IsSamePoint(vertices[0], vertices[^1])) vertices.RemoveAt(vertices.Count - 1);

                        foreach (var pt in vertices)
                        {
                            using var rowBuf = featureClass.CreateRowBuffer();
                            rowBuf[shapeField] = pt;
                            TrySet(rowBuf, "BSM", globalBSM++);
                            TrySet(rowBuf, "ZDZHDM", zdzhdm);
                            TrySet(rowBuf, "YSDM", JZDYSDM);
                            TrySet(rowBuf, "JZDH", string.IsNullOrEmpty(JZDHPrefix) ? sxh.ToString() : $"{JZDHPrefix}{sxh}");
                            TrySet(rowBuf, "SXH", sxh);
                            TrySet(rowBuf, "JBLX", null);
                            TrySet(rowBuf, "JZDLX", null);
                            // 坐标字段：按测绘习惯反转XY，写入 XZBZ/YZBZ
                            TrySet(rowBuf, "XZBZ", pt.Y);
                            TrySet(rowBuf, "YZBZ", pt.X);
                            TrySet(rowBuf, "ZZBZ", pt.HasZ ? pt.Z : (double?)null);
                            using var newRow = featureClass.CreateRow(rowBuf); newRow.Store();
                            sxh++;
                        }
                    }

                    processed++;
                    UpdateProgress(processed, total);
                }
                }
                finally
                {
                    featureClass?.Dispose();
                    gdb?.Dispose();
                    fsds?.Dispose();
                }
            }
            catch (Exception ex) { LogError($"生成界址点失败: {ex.Message}"); }
        }

        private async Task GenerateLineFeatures(string outputFeatureClassPath)
        {
            try
            {
                using var inputTable = SelectedPolygonLayer.GetTable();
                // 计算总量：若使用选择集则为选择数量，否则为全部数量
                bool useSelection = UseSelection && SelectionUtils.GetSelectionCount(SelectedPolygonLayer) > 0;
                int total = 0;
                if (useSelection)
                {
                    total = SelectionUtils.GetSelectionCount(SelectedPolygonLayer);
                    LogInfo($"检测到选择集: {total} 个要素，将仅处理选择的要素。");
                }
                else
                {
                    using (var c = inputTable.Search()) { while (c.MoveNext()) total++; }
                    LogInfo($"未检测到选择集，将处理全部 {total} 个要素。");
                }
                int processed = 0; int globalBSM = 1;

                FeatureClass featureClass = null;
                Geodatabase gdb = null;
                FileSystemDatastore fsds = null;
                try
                {
                    var catalogPath = outputFeatureClassPath;
                    if (catalogPath.EndsWith(".shp", StringComparison.OrdinalIgnoreCase))
                    {
                        var folder = Path.GetDirectoryName(catalogPath);
                        var shpName = Path.GetFileName(catalogPath);
                        var conn = new FileSystemConnectionPath(new Uri(folder), FileSystemDatastoreType.Shapefile);
                        fsds = new FileSystemDatastore(conn);
                        featureClass = fsds.OpenDataset<FeatureClass>(shpName);
                    }
                    else
                    {
                        int gdbIdx = catalogPath.IndexOf(".gdb", StringComparison.OrdinalIgnoreCase);
                        if (gdbIdx >= 0)
                        {
                            var gdbRoot = catalogPath.Substring(0, gdbIdx + 4);
                            var relative = catalogPath.Length > gdbIdx + 4 ? catalogPath.Substring(gdbIdx + 4).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : string.Empty;
                            gdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(gdbRoot)));
                            featureClass = gdb.OpenDataset<FeatureClass>(relative);
                        }
                        else
                        {
                            var workspace = Path.GetDirectoryName(catalogPath);
                            var featureClassName = Path.GetFileNameWithoutExtension(catalogPath);
                            bool isGdb = workspace.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase);
                            if (isGdb)
                            {
                                gdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(workspace)));
                                featureClass = gdb.OpenDataset<FeatureClass>(featureClassName);
                            }
                            else
                            {
                                var conn = new FileSystemConnectionPath(new Uri(workspace), FileSystemDatastoreType.Shapefile);
                                fsds = new FileSystemDatastore(conn);
                                featureClass = fsds.OpenDataset<FeatureClass>(featureClassName + ".shp");
                            }
                        }
                    }

                    var shapeField = featureClass.GetDefinition().GetShapeField();

                // 按范围获取游标（通用接口）
                using var cursor = SelectionUtils.GetSelectionOrAllCursor(SelectedPolygonLayer, useSelection, new QueryFilter(), false);
                while (cursor.MoveNext())
                {
                    if (CancelRequested) break;
                    var feature = cursor.Current as Feature;
                    var polygon = feature?.GetShape() as Polygon;
                    if (polygon == null) continue;
                    var zdzhdm = GetStringSafe(feature, SelectedCodeFieldName);

                    foreach (var part in polygon.Parts)
                    {
                        MapPoint last = null;
                        foreach (var seg in part)
                        {
                            var sp = seg.StartPoint; var ep = seg.EndPoint;
                            // 构造线段（两点）
                            var line = ArcGIS.Core.Geometry.PolylineBuilderEx.CreatePolyline(new List<MapPoint> { sp, ep }, polygon.SpatialReference);
                            double length = GeometryEngine.Instance.Length(line);
                            if (JZXLengthDecimals >= 0)
                                length = Math.Round(length, JZXLengthDecimals);

                            using var rowBuf = featureClass.CreateRowBuffer();
                            rowBuf[shapeField] = line;
                            TrySet(rowBuf, "BSM", globalBSM++);
                            TrySet(rowBuf, "ZDZHDM", zdzhdm);
                            TrySet(rowBuf, "YSDM", JZXYSDM);
                            TrySet(rowBuf, "JZXCD", length);
                            TrySet(rowBuf, "JZXLB", null);
                            TrySet(rowBuf, "JZXWZ", null);
                            TrySet(rowBuf, "JZXZ", null);
                            TrySet(rowBuf, "QSJXYSBH", null);
                            TrySet(rowBuf, "QSJXYS", null);
                            TrySet(rowBuf, "QSZYYSBH", null);
                            TrySet(rowBuf, "QSZYYS", null);
                            using var newRow = featureClass.CreateRow(rowBuf); newRow.Store();
                            last = ep;
                        }
                    }

                    processed++;
                    UpdateProgress(processed, total);
                }
                }
                finally
                {
                    featureClass?.Dispose();
                    gdb?.Dispose();
                    fsds?.Dispose();
                }
            }
            catch (Exception ex) { LogError($"生成界址线失败: {ex.Message}"); }
        }

        private static bool IsSamePoint(MapPoint a, MapPoint b)
        {
            if (a == null || b == null) return false;
            return Math.Abs(a.X - b.X) < 1e-7 && Math.Abs(a.Y - b.Y) < 1e-7;
        }

        private static string GetStringSafe(Feature feature, string fieldName)
        {
            try
            {
                if (feature == null || string.IsNullOrWhiteSpace(fieldName)) return null;
                var v = feature[fieldName];
                return v?.ToString();
            }
            catch { return null; }
        }

        private void TrySet(RowBuffer buf, string field, object value)
        {
            try { buf[field] = value ?? (object)DBNull.Value; } catch { }
        }

        private void UpdateProgress(int processed, int total)
        {
            var p = (int)(processed * 100.0 / Math.Max(1, total));
            System.Windows.Application.Current?.Dispatcher.Invoke(() => { Progress = p; IsProgressIndeterminate = false; });
        }

        #endregion

        #region 日志
        private void ClearLog() { _logBuilder.Clear(); LogContent = string.Empty; }
        private void LogInfo(string msg) { Append($"{msg}"); }
        private void LogWarning(string msg) { Append($"警告: {msg}"); }
        private void LogError(string msg) { Append($"错误: {msg}"); }
        private void Append(string msg)
        {
            _logBuilder.AppendLine($"[{DateTime.Now:HH:mm:ss}] {msg}");
            System.Windows.Application.Current?.Dispatcher.Invoke(() => LogContent = _logBuilder.ToString());
        }
        private void ShowHelp()
        {
            var help = "界址点线生成\n\n" +
                "- 选择面图层，选择生成类型（界址点/界址线/同时生成）。\n" +
                "- 可自定义 JZD/JZX 输出位置：支持工程GDB或文件夹（自动识别并创建 GDB 要素类或 Shapefile）。\n" +
                "- 若目标已存在，会弹出覆盖确认提示。\n" +
                "- 可选从源图层选择一个字段写入 ZDZHDM（宗地/宗海代码）。\n" +
                "- 其他标准字段预留，用户可后续按需维护。";
            MessageBox.Show(help, "帮助");
        }
        #endregion
    }
}
