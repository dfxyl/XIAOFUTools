using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Core.CIM;
using System.Threading;
using ArcGIS.Desktop.Core.Geoprocessing;

namespace XIAOFUTools.Features.DataManagement.FieldCopyTool
{
    public partial class FieldCopyToolViewModel
    {

        /// <summary>
        /// 源图层列表
        /// </summary>
        public ObservableCollection<FeatureLayer> SourceLayerList
        {
            get { return _sourceLayerList; }
            set
            {
                SetProperty(ref _sourceLayerList, value);
            }
        }

        /// <summary>
        /// 字段列表
        /// </summary>
        public ObservableCollection<FieldInfo> FieldList
        {
            get { return _fieldList; }
            set
            {
                SetProperty(ref _fieldList, value);
            }
        }

        /// <summary>
        /// 目标图层列表
        /// </summary>
        public ObservableCollection<LayerInfo> TargetLayerList
        {
            get { return _targetLayerList; }
            set
            {
                SetProperty(ref _targetLayerList, value);
            }
        }

        /// <summary>
        /// 选中的源图层
        /// </summary>
        public FeatureLayer SelectedSourceLayer
        {
            get { return _selectedSourceLayer; }
            set
            {
                SetProperty(ref _selectedSourceLayer, value);
                LoadFields();
            }
        }

        /// <summary>
        /// 是否正在处理
        /// </summary>
        public bool IsProcessing
        {
            get { return _isProcessing; }
            set
            {
                SetProperty(ref _isProcessing, value);
            }
        }

        /// <summary>
        /// 日志文本
        /// </summary>
        public string LogText
        {
            get { return _logText; }
            set
            {
                SetProperty(ref _logText, value);
            }
        }

        /// <summary>
        /// 字段全选命令
        /// </summary>
        public ICommand SelectAllFieldsCommand { get; private set; }

        /// <summary>
        /// 字段反选命令
        /// </summary>
        public ICommand InvertFieldSelectionCommand { get; private set; }

        /// <summary>
        /// 目标图层全选命令
        /// </summary>
        public ICommand SelectAllTargetLayersCommand { get; private set; }

        /// <summary>
        /// 目标图层反选命令
        /// </summary>
        public ICommand InvertTargetLayerSelectionCommand { get; private set; }

        /// <summary>
        /// 开始执行命令
        /// </summary>
        public ICommand StartCommand { get; private set; }

        /// <summary>
        /// 停止执行命令
        /// </summary>
        public ICommand StopCommand { get; private set; }



        /// <summary>
        /// 帮助命令
        /// </summary>
        public ICommand ShowHelpCommand { get; private set; }

        /// <summary>
        /// 刷新图层命令
        /// </summary>
        public ICommand RefreshLayersCommand { get; private set; }
    }
}
