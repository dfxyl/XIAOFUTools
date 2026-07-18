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
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.DataManagement.BatchGeometryRepair
{
    /// <summary>
    /// 批量修复几何视图模型
    /// </summary>
    internal partial class BatchGeometryRepairViewModel : PropertyChangedBase
    {

        // 取消操作标志
        private bool _cancelRequested = false;
        
        // 是否正在处理
        private bool _isProcessing = false;

        // 图层列表
        private ObservableCollection<LayerGeometryInfo> _layerList;

        // 进度值
        private int _progress = 0;

        // 进度条是否为不确定状态
        private bool _isProgressIndeterminate = false;

        // 状态消息
        private string _statusMessage = "就绪";

        // 日志内容
        private string _logContent = "";

        // 全选命令
        private ICommand _selectAllCommand;

        // 反选命令
        private ICommand _selectNoneCommand;

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
        public BatchGeometryRepairViewModel()
        {
            LayerList = new ObservableCollection<LayerGeometryInfo>();

            // 监听集合变化，为新添加的项目订阅属性变化事件
            LayerList.CollectionChanged += LayerList_CollectionChanged;
        }
    }
}
