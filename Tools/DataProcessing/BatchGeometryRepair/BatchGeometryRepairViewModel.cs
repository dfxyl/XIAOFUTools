using ArcGIS.Core.CIM;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using XIAOFUTools.Common;

namespace XIAOFUTools.Tools.BatchGeometryRepair
{
    /// <summary>
    /// 批量修复几何视图模型
    /// </summary>
    internal class BatchGeometryRepairViewModel : PropertyChangedBase
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
        public bool CanProcess => !IsProcessing && LayerList.Any(l => l.IsSelected);

        // 图层列表
        private ObservableCollection<LayerGeometryInfo> _layerList;
        public ObservableCollection<LayerGeometryInfo> LayerList
        {
            get => _layerList;
            set
            {
                SetProperty(ref _layerList, value);
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

        // 进度条是否为不确定状态
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
        private string _statusMessage = "就绪";
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

        // 全选命令
        private ICommand _selectAllCommand;
        public ICommand SelectAllCommand
        {
            get
            {
                return _selectAllCommand ?? (_selectAllCommand = new RelayCommand(() => SelectAll(), () => LayerList.Count > 0));
            }
        }

        // 反选命令
        private ICommand _selectNoneCommand;
        public ICommand SelectNoneCommand
        {
            get
            {
                return _selectNoneCommand ?? (_selectNoneCommand = new RelayCommand(() => SelectNone(), () => LayerList.Count > 0));
            }
        }

        // 运行命令
        private ICommand _runCommand;
        public ICommand RunCommand
        {
            get
            {
                return _runCommand ?? (_runCommand = new RelayCommand(async () => await RunAsync(), () => CanProcess));
            }
        }

        // 取消命令
        private ICommand _cancelCommand;
        public ICommand CancelCommand
        {
            get
            {
                return _cancelCommand ?? (_cancelCommand = new RelayCommand(() => Cancel(), () => IsProcessing));
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

        #region 构造函数

        /// <summary>
        /// 构造函数
        /// </summary>
        public BatchGeometryRepairViewModel()
        {
            LayerList = new ObservableCollection<LayerGeometryInfo>();

            // 监听集合变化，为新添加的项目订阅属性变化事件
            LayerList.CollectionChanged += LayerList_CollectionChanged;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 刷新图层列表
        /// </summary>
        public void RefreshLayers()
        {
            LoadLayers();
        }

        #endregion

        #region 私有方法

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
                    var tempLayers = new List<LayerGeometryInfo>();
                    var map = MapView.Active?.Map;
                    
                    if (map != null)
                    {
                        // 只获取要素图层，因为只有要素图层需要修复几何
                        var featureLayers = map.GetLayersAsFlattenedList().OfType<FeatureLayer>().ToList();
                        
                        foreach (var layer in featureLayers)
                        {
                            var layerInfo = new LayerGeometryInfo
                            {
                                Layer = layer,
                                LayerName = layer.Name,
                                LayerType = GetLayerType(layer),
                                CoordinateSystem = GetLayerCoordinateSystem(layer),
                                IsSelected = false
                            };
                            
                            tempLayers.Add(layerInfo);
                        }
                    }
                    
                    // 在UI线程更新图层列表
                    System.Windows.Application.Current.Dispatcher.Invoke(() => 
                    {
                        // 清空图层列表
                        LayerList.Clear();
                        
                        // 添加图层
                        foreach (var layer in tempLayers)
                        {
                            LayerList.Add(layer);
                        }
                        
                        StatusMessage = $"加载了 {LayerList.Count} 个要素图层";
                        LogInfo($"加载了 {LayerList.Count} 个要素图层");
                    });
                }
                catch (Exception ex)
                {
                    // 确保在UI线程显示错误信息
                    System.Windows.Application.Current.Dispatcher.Invoke(() => 
                    {
                        StatusMessage = $"加载图层出错: {ex.Message}";
                        LogError($"加载图层出错: {ex.Message}");
                    });
                }
            });
        }

        /// <summary>
        /// 获取图层类型
        /// </summary>
        private string GetLayerType(FeatureLayer layer)
        {
            try
            {
                var geometryType = layer.GetFeatureClass()?.GetDefinition()?.GetShapeType();
                return geometryType switch
                {
                    ArcGIS.Core.Geometry.GeometryType.Point => "点图层",
                    ArcGIS.Core.Geometry.GeometryType.Polyline => "线图层",
                    ArcGIS.Core.Geometry.GeometryType.Polygon => "面图层",
                    ArcGIS.Core.Geometry.GeometryType.Multipoint => "多点图层",
                    _ => "要素图层"
                };
            }
            catch
            {
                return "要素图层";
            }
        }

        /// <summary>
        /// 获取图层坐标系
        /// </summary>
        private string GetLayerCoordinateSystem(FeatureLayer layer)
        {
            try
            {
                var spatialRef = layer.GetSpatialReference();
                return spatialRef?.Name ?? "未知";
            }
            catch
            {
                return "未知";
            }
        }

        /// <summary>
        /// 全选
        /// </summary>
        private void SelectAll()
        {
            foreach (var layer in LayerList)
            {
                layer.IsSelected = true;
            }
            NotifyPropertyChanged(() => CanProcess);
            StatusMessage = $"已选择 {LayerList.Count} 个图层";
        }

        /// <summary>
        /// 反选
        /// </summary>
        private void SelectNone()
        {
            foreach (var layer in LayerList)
            {
                layer.IsSelected = false;
            }
            NotifyPropertyChanged(() => CanProcess);
            StatusMessage = "已取消选择所有图层";
        }

        /// <summary>
        /// 异步运行修复几何
        /// </summary>
        private async Task RunAsync()
        {
            try
            {
                IsProcessing = true;
                CancelRequested = false;
                Progress = 0;
                IsProgressIndeterminate = false;

                var selectedLayers = LayerList.Where(l => l.IsSelected).ToList();
                if (selectedLayers.Count == 0)
                {
                    StatusMessage = "请选择要修复的图层";
                    return;
                }

                StatusMessage = $"开始修复 {selectedLayers.Count} 个图层的几何...";
                LogInfo($"开始批量修复几何，共 {selectedLayers.Count} 个图层");

                int processedCount = 0;
                int totalCount = selectedLayers.Count;

                foreach (var layerInfo in selectedLayers)
                {
                    if (CancelRequested)
                    {
                        StatusMessage = "操作已取消";
                        LogWarning("用户取消了操作");
                        break;
                    }

                    try
                    {
                        StatusMessage = $"正在修复: {layerInfo.LayerName}";
                        LogInfo($"开始修复图层: {layerInfo.LayerName}");

                        await RepairGeometryForLayer(layerInfo.Layer as FeatureLayer);

                        processedCount++;
                        Progress = (int)((double)processedCount / totalCount * 100);

                        LogInfo($"完成修复图层: {layerInfo.LayerName}");
                    }
                    catch (Exception ex)
                    {
                        LogError($"修复图层 {layerInfo.LayerName} 时出错: {ex.Message}");
                    }
                }

                if (!CancelRequested)
                {
                    StatusMessage = $"修复完成，共处理 {processedCount} 个图层";
                    LogInfo($"批量修复几何完成，共处理 {processedCount} 个图层");
                    Progress = 100;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"处理出错: {ex.Message}";
                LogError($"批量修复几何出错: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
                Progress = 0;
            }
        }

        /// <summary>
        /// 为单个图层修复几何
        /// </summary>
        private async Task RepairGeometryForLayer(FeatureLayer layer)
        {
            await QueuedTask.Run(async () =>
            {
                try
                {
                    // 使用ArcGIS Pro的修复几何地理处理工具
                    var parameters = Geoprocessing.MakeValueArray(
                        layer,
                        "DELETE_NULL"  // 删除空几何
                    );

                    var result = await Geoprocessing.ExecuteToolAsync("RepairGeometry_management", parameters);
                    if (result.IsFailed)
                    {
                        throw new Exception($"修复几何失败: {string.Join(", ", result.Messages.Select(m => m.Text))}");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"修复几何失败: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 取消操作
        /// </summary>
        private void Cancel()
        {
            CancelRequested = true;
            StatusMessage = "正在取消...";
        }

        /// <summary>
        /// 记录信息消息
        /// </summary>
        private void LogInfo(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogContent += $"[{timestamp}] {message}\n";
        }

        /// <summary>
        /// 记录警告消息
        /// </summary>
        private void LogWarning(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogContent += $"[{timestamp}] 警告: {message}\n";
        }

        /// <summary>
        /// 记录错误消息
        /// </summary>
        private void LogError(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogContent += $"[{timestamp}] 错误: {message}\n";
        }

        /// <summary>
        /// 显示帮助
        /// </summary>
        private void ShowHelp()
        {
            string helpContent = "批量修复几何工具使用说明\n\n" +
                               "功能描述：\n" +
                               "该工具用于批量修复要素图层中的几何错误。\n\n" +
                               "参数说明：\n" +
                               "1. 图层列表：显示当前地图中的所有要素图层\n" +
                               "2. 选择：勾选需要修复几何的图层\n" +
                               "3. 图层名称：图层的名称\n" +
                               "4. 类型：图层的几何类型（点、线、面等）\n" +
                               "5. 坐标系：图层的坐标系\n\n" +
                               "操作步骤：\n" +
                               "1. 选择需要修复几何的图层（可使用全选/反选）\n" +
                               "2. 点击\"开始\"按钮执行批量修复几何\n" +
                               "3. 查看日志窗口了解处理进度和结果\n\n" +
                               "注意事项：\n" +
                               "- 修复几何会删除空几何和修复无效几何\n" +
                               "- 建议在修复前备份重要数据\n" +
                               "- 操作过程和结果将显示在日志窗口中\n" +
                               "- 处理过程中可以点击\"停止\"按钮取消操作";

            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(helpContent, "批量修复几何工具使用说明");
        }

        /// <summary>
        /// 处理图层列表集合变化事件
        /// </summary>
        private void LayerList_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // 为新添加的项目订阅属性变化事件
            if (e.NewItems != null)
            {
                foreach (LayerGeometryInfo item in e.NewItems)
                {
                    item.PropertyChanged += LayerInfo_PropertyChanged;
                }
            }

            // 为移除的项目取消订阅属性变化事件
            if (e.OldItems != null)
            {
                foreach (LayerGeometryInfo item in e.OldItems)
                {
                    item.PropertyChanged -= LayerInfo_PropertyChanged;
                }
            }
        }

        /// <summary>
        /// 处理图层信息属性变化事件
        /// </summary>
        private void LayerInfo_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // 当IsSelected属性改变时，通知CanProcess属性也发生了变化
            if (e.PropertyName == nameof(LayerGeometryInfo.IsSelected))
            {
                NotifyPropertyChanged(() => CanProcess);
            }
        }

        #endregion
    }
}
