using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XIAOFUTools.Shared;
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

namespace XIAOFUTools.Features.DataManagement.RangeClipTool
{
    /// <summary>
    /// 根据范围批量裁剪要素图层视图模型
    /// </summary>
    internal partial class RangeClipToolViewModel : PropertyChangedBase
    {
        private readonly Infrastructure.RangeClipOutputFolderStore _outputFolderStore = new Infrastructure.RangeClipOutputFolderStore();

        // 取消操作标志
        private bool _cancelRequested = false;
        
        // 是否正在处理
        private bool _isProcessing = false;

        // 范围图层列表
        private ObservableCollection<FeatureLayer> _rangeLayers;

        // 选中的范围图层
        private FeatureLayer _selectedRangeLayer;

        // 范围字段名称列表
        private ObservableCollection<string> _rangeFieldNames;

        // 选中的范围字段
        private string _selectedRangeField;

        // 需裁剪图层项目列表
        private ObservableCollection<ClipLayerItem> _clipLayerItems;

        // 是否创建子文件夹
        private bool _createSubFolder = true;

        // 输出文件夹
        private string _outputFolder = "";

        // 进度值
        private int _progress = 0;

        // 进度是否不确定
        private bool _isProgressIndeterminate = false;

        // 状态消息
        private string _statusMessage = "";

        // 日志内容
        private string _logContent = "";

        // 全选图层命令
        private ICommand _selectAllLayersCommand;

        // 反选图层命令
        private ICommand _invertSelectionCommand;

        // 取消命令
        private ICommand _cancelCommand;

        // 运行命令
        private ICommand _runCommand;
        
        // 帮助命令
        private ICommand _showHelpCommand;

        // 浏览文件夹命令
        private ICommand _browseFolderCommand;

        // 刷新图层命令
        private ICommand _refreshLayersCommand;

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
