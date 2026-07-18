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
using XIAOFUTools.Shared;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using System.Collections.Generic;

namespace XIAOFUTools.Features.DataManagement.BatchProjectionDefinition
{
    /// <summary>
    /// 批量定义投影视图模型
    /// </summary>
    internal partial class BatchProjectionDefinitionViewModel : PropertyChangedBase
    {

        // 取消操作标志
        private bool _cancelRequested = false;
        
        // 是否正在处理
        private bool _isProcessing = false;

        // 图层列表
        private ObservableCollection<LayerProjectionInfo> _layerList;

        // 选择的坐标系
        private SpatialReference _selectedSpatialReference;

        // 进度值
        private int _progress = 0;

        // 进度是否不确定
        private bool _isProgressIndeterminate = false;

        // 日志内容
        private string _logContent = "";

        // 状态消息
        private string _statusMessage = "就绪";

        // 全选命令
        private ICommand _selectAllCommand;

        // 反选命令
        private ICommand _selectNoneCommand;

        // 选择坐标系命令
        private ICommand _selectCoordinateSystemCommand;

        // 运行命令
        private ICommand _runCommand;

        // 取消命令
        private ICommand _cancelCommand;

        // 帮助命令
        private ICommand _showHelpCommand;

        // 刷新图层命令
        private ICommand _refreshLayersCommand;

        /// <summary>
        /// 构造函数
        /// </summary>
        public BatchProjectionDefinitionViewModel()
        {
            LayerList = new ObservableCollection<LayerProjectionInfo>();
        }
    }
}
