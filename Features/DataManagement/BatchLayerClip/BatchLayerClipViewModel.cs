using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Text;
using System.Globalization;
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
using XIAOFUTools.Features.DataManagement.BatchLayerClip.Infrastructure;

namespace XIAOFUTools.Features.DataManagement.BatchLayerClip
{
    /// <summary>
    /// 按字段批量裁剪要素图层视图模型
    /// </summary>
    internal partial class BatchLayerClipViewModel : PropertyChangedBase
    {
        private readonly BatchLayerClipFileStore _fileStore = new();

        // 取消操作标志
        private bool _cancelRequested = false;
        
        // 是否正在处理
        private bool _isProcessing = false;

        // 要素图层列表
        private ObservableCollection<FeatureLayer> _featureLayers;

        // 选中的要素图层
        private FeatureLayer _selectedFeatureLayer;

        // 字段名称列表
        private ObservableCollection<string> _fieldNames;

        // 选中的字段
        private string _selectedField;

        // 输出文件夹
        private string _outputFolder;

        // 是否创建子文件夹
        private bool _createSubFolder;

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

        // 当前地理处理任务的取消源
        private CancellationTokenSource _geoprocessingCancellationSource;

        // 浏览文件夹命令
        private ICommand _browseFolderCommand;

        // 停止命令
        private ICommand _cancelCommand;

        // 运行命令
        private ICommand _runCommand;
        
        // 帮助命令
        private ICommand _showHelpCommand;

        // 刷新图层命令
        private ICommand _refreshLayersCommand;

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
    }
} 
