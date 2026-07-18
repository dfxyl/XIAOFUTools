using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
using ArcGIS.Desktop.Editing;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.Editing.NodeDistanceCheck
{
    /// <summary>
    /// 节点距离检查工具视图模型
    /// </summary>
    internal partial class NodeDistanceCheckDockPaneViewModel : PropertyChangedBase
    {

        // 取消操作标志
        private bool _cancelRequested = false;

        // 是否正在处理
        private bool _isProcessing = false;

        // 面要素图层列表
        private ObservableCollection<FeatureLayer> _polygonLayers;

        // 选中的面要素图层
        private FeatureLayer _selectedPolygonLayer;

        // 节点检查选项列表
        private ObservableCollection<string> _checkOptions;

        // 选中的节点检查选项
        private string _selectedCheckOption;

        // 节点检查距离
        private double _checkDistance = 1.0;

        // 输出线要素图层路径
        private string _outputPath;

        // 状态信息
        private string _statusMessage;

        // 日志内容
        private string _logContent;

        // 日志构建器
        private StringBuilder _logBuilder = new StringBuilder();

        // 进度
        private int _progress;

        // 是否为不确定进度
        private bool _isProgressIndeterminate;

        // 选中的保留字段
        private List<string> _selectedFields = new List<string>();

        // 浏览输出路径命令
        private ICommand _browseOutputCommand;

        // 取消命令
        private ICommand _cancelCommand;

        // 运行命令
        private RelayCommand _runCommand;

        // 帮助命令
        private ICommand _showHelpCommand;

        // 选择字段命令
        private ICommand _selectFieldsCommand;

        // 刷新图层命令
        private ICommand _refreshLayersCommand;

        /// <summary>
        /// 构造函数
        /// </summary>
        public NodeDistanceCheckDockPaneViewModel()
        {
            // 初始化属性
            PolygonLayers = new ObservableCollection<FeatureLayer>();
            CheckOptions = new ObservableCollection<string>
            {
                "小于等于",
                "小于",
                "大于等于", 
                "大于",
                "等于"
            };

            // 设置默认值
            SelectedCheckOption = "小于等于";
            CheckDistance = 1.0;
            
            StatusMessage = "正在加载图层...";
            LogContent = "";
            Progress = 0;
            IsProgressIndeterminate = false;

            // 先设置一个默认的输出路径
            UpdateOutputPath();

            // 加载面图层
            LoadPolygonLayers();
        }
    }
}