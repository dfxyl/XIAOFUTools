using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Core.Geometry;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using XIAOFUTools.Common;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using System.Collections.Generic;

namespace XIAOFUTools.Tools.BatchProjectionDefinition
{
    /// <summary>
    /// 批量定义投影视图模型
    /// </summary>
    internal class BatchProjectionDefinitionViewModel : PropertyChangedBase
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
        public bool CanProcess => !IsProcessing && SelectedSpatialReference != null && LayerList.Any(l => l.IsSelected);

        // 图层列表
        private ObservableCollection<LayerProjectionInfo> _layerList;
        public ObservableCollection<LayerProjectionInfo> LayerList
        {
            get => _layerList;
            set
            {
                SetProperty(ref _layerList, value);
            }
        }

        // 选择的坐标系
        private SpatialReference _selectedSpatialReference;
        public SpatialReference SelectedSpatialReference
        {
            get => _selectedSpatialReference;
            set
            {
                SetProperty(ref _selectedSpatialReference, value);
                NotifyPropertyChanged(() => CanProcess);
                NotifyPropertyChanged(() => SelectedCoordinateSystemName);
            }
        }

        // 选择的坐标系名称
        public string SelectedCoordinateSystemName
        {
            get
            {
                if (SelectedSpatialReference == null)
                    return "未选择坐标系";
                return SelectedSpatialReference.Name;
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

        // 选择坐标系命令
        private ICommand _selectCoordinateSystemCommand;
        public ICommand SelectCoordinateSystemCommand
        {
            get
            {
                return _selectCoordinateSystemCommand ?? (_selectCoordinateSystemCommand = new RelayCommand(() => SelectCoordinateSystem()));
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
        public BatchProjectionDefinitionViewModel()
        {
            LayerList = new ObservableCollection<LayerProjectionInfo>();
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
                    var tempLayers = new List<LayerProjectionInfo>();
                    var map = MapView.Active?.Map;
                    
                    if (map != null)
                    {
                        var layers = map.GetLayersAsFlattenedList().ToList();
                        
                        foreach (var layer in layers)
                        {
                            // 排除网络图层
                            if (layer is ServiceLayer) continue;
                            
                            var layerInfo = new LayerProjectionInfo
                            {
                                Layer = layer,
                                LayerName = layer.Name,
                                LayerType = GetLayerType(layer),
                                CurrentCoordinateSystem = GetLayerCoordinateSystem(layer),
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
                        foreach (var layerInfo in tempLayers)
                        {
                            LayerList.Add(layerInfo);
                        }
                        
                        LogMessage($"已加载 {LayerList.Count} 个图层");
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
        private string GetLayerType(Layer layer)
        {
            return layer switch
            {
                FeatureLayer => "要素图层",
                RasterLayer => "栅格图层",
                ImageServiceLayer => "影像服务图层",
                _ => layer.GetType().Name
            };
        }

        /// <summary>
        /// 获取图层坐标系
        /// </summary>
        private string GetLayerCoordinateSystem(Layer layer)
        {
            try
            {
                SpatialReference spatialRef = null;
                
                if (layer is FeatureLayer featureLayer)
                {
                    spatialRef = featureLayer.GetSpatialReference();
                }
                else if (layer is RasterLayer rasterLayer)
                {
                    spatialRef = rasterLayer.GetSpatialReference();
                }
                
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
        }

        /// <summary>
        /// 选择坐标系
        /// </summary>
        private void SelectCoordinateSystem()
        {
            try
            {
                var selectedSpatialRef = CoordinateSystemSelector.ShowCoordinateSystemDialog();
                if (selectedSpatialRef != null)
                {
                    SelectedSpatialReference = selectedSpatialRef;
                    LogMessage($"已选择坐标系: {SelectedSpatialReference.Name}");
                }
            }
            catch (Exception ex)
            {
                LogError($"选择坐标系时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 执行批量定义投影
        /// </summary>
        private async Task RunAsync()
        {
            if (SelectedSpatialReference == null)
            {
                LogError("请先选择坐标系");
                return;
            }

            var selectedLayers = LayerList.Where(l => l.IsSelected).ToList();
            if (selectedLayers.Count == 0)
            {
                LogError("请至少选择一个图层");
                return;
            }

            IsProcessing = true;
            CancelRequested = false;
            Progress = 0;
            IsProgressIndeterminate = false;
            
            try
            {
                StatusMessage = "正在定义投影...";
                LogMessage($"开始为 {selectedLayers.Count} 个图层定义投影");
                LogMessage($"目标坐标系: {SelectedSpatialReference.Name}");

                int processedCount = 0;
                int totalCount = selectedLayers.Count;

                foreach (var layerInfo in selectedLayers)
                {
                    if (CancelRequested)
                    {
                        LogMessage("操作已取消");
                        break;
                    }

                    try
                    {
                        await DefineProjectionForLayer(layerInfo.Layer);
                        processedCount++;
                        Progress = (int)((double)processedCount / totalCount * 100);
                        LogMessage($"已完成: {layerInfo.LayerName}");
                    }
                    catch (Exception ex)
                    {
                        LogError($"处理图层 {layerInfo.LayerName} 时出错: {ex.Message}");
                    }
                }

                if (!CancelRequested)
                {
                    StatusMessage = $"完成！已处理 {processedCount} 个图层";
                    LogMessage($"批量定义投影完成，共处理 {processedCount} 个图层");
                    
                    // 刷新图层列表以更新坐标系信息
                    RefreshLayers();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"处理出错: {ex.Message}";
                LogError($"批量定义投影出错: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
                Progress = 0;
            }
        }

        /// <summary>
        /// 为单个图层定义投影
        /// </summary>
        private async Task DefineProjectionForLayer(Layer layer)
        {
            await QueuedTask.Run(async () =>
            {
                try
                {
                    if (layer is FeatureLayer featureLayer)
                    {
                        // 对于要素图层，使用地理处理工具定义投影
                        var parameters = Geoprocessing.MakeValueArray(
                            featureLayer,
                            SelectedSpatialReference
                        );

                        var result = await Geoprocessing.ExecuteToolAsync("DefineProjection_management", parameters);
                        if (result.IsFailed)
                        {
                            throw new Exception($"定义投影失败: {string.Join(", ", result.Messages)}");
                        }
                    }
                    else if (layer is RasterLayer rasterLayer)
                    {
                        // 对于栅格图层，使用地理处理工具定义投影
                        var parameters = Geoprocessing.MakeValueArray(
                            rasterLayer,
                            SelectedSpatialReference
                        );

                        var result = await Geoprocessing.ExecuteToolAsync("DefineProjection_management", parameters);
                        if (result.IsFailed)
                        {
                            throw new Exception($"定义投影失败: {string.Join(", ", result.Messages)}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"定义投影失败: {ex.Message}");
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
        /// 记录消息
        /// </summary>
        private void LogMessage(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogContent += $"[{timestamp}] {message}\n";
        }

        /// <summary>
        /// 记录错误
        /// </summary>
        private void LogError(string error)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogContent += $"[{timestamp}] 错误: {error}\n";
        }

        /// <summary>
        /// 显示帮助
        /// </summary>
        private void ShowHelp()
        {
            string helpContent = "批量定义投影工具使用说明\n\n" +
                               "功能描述：\n" +
                               "该工具用于为多个图层批量定义投影坐标系。\n\n" +
                               "参数说明：\n" +
                               "1. 图层列表：显示当前地图中的所有图层（除网络图层）\n" +
                               "2. 选择：勾选需要定义投影的图层\n" +
                               "3. 图层名称：图层的名称\n" +
                               "4. 类型：图层的类型（要素图层、栅格图层等）\n" +
                               "5. 当前坐标系：图层当前的坐标系\n\n" +
                               "操作步骤：\n" +
                               "1. 选择需要定义投影的图层（可使用全选/反选）\n" +
                               "2. 点击\"选择坐标系\"按钮选择目标坐标系\n" +
                               "3. 点击\"开始\"按钮执行批量定义投影\n\n" +
                               "注意事项：\n" +
                               "- 定义投影不会改变数据的实际坐标，只是告诉系统数据使用的坐标系\n" +
                               "- 请确保选择的坐标系与数据的实际坐标系一致\n" +
                               "- 操作过程和结果将显示在日志窗口中\n" +
                               "- 处理过程中可以点击\"停止\"按钮取消操作";

            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(helpContent, "批量定义投影工具使用说明");
        }

        #endregion
    }
}
