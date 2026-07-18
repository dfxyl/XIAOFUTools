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
        /// 初始化基本属性
        /// </summary>
        private void Initialize()
        {
            _sourceLayerList = new ObservableCollection<FeatureLayer>();
            _fieldList = new ObservableCollection<FieldInfo>();
            _targetLayerList = new ObservableCollection<LayerInfo>();
            _isProcessing = false;
            _logText = "";
        }

        /// <summary>
        /// 初始化命令
        /// </summary>
        private void InitializeCommands()
        {
            SelectAllFieldsCommand = new RelayCommand(() => SelectAllFields(), () => !IsProcessing);
            InvertFieldSelectionCommand = new RelayCommand(() => InvertFieldSelection(), () => !IsProcessing);
            SelectAllTargetLayersCommand = new RelayCommand(() => SelectAllTargetLayers(), () => !IsProcessing);
            InvertTargetLayerSelectionCommand = new RelayCommand(() => InvertTargetLayerSelection(), () => !IsProcessing);
            StartCommand = new RelayCommand(() => StartCopyFields(), () => CanStartCopy());
            StopCommand = new RelayCommand(() => StopCopyFields(), () => IsProcessing);
            ShowHelpCommand = new RelayCommand(() => ShowHelp());
            RefreshLayersCommand = new RelayCommand(() => RefreshLayers());
        }

        /// <summary>
        /// 停止复制字段
        /// </summary>
        private void StopCopyFields()
        {
            // 这里可以实现取消逻辑
            IsProcessing = false;
            LogWarning("操作已停止");
        }
    }
}
