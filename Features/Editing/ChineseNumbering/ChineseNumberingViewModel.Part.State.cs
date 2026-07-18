using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Framework.Events;

namespace XIAOFUTools.Features.Editing.ChineseNumbering
{
    internal partial class ChineseNumberingViewModel
    {
        public ObservableCollection<FeatureLayer> LayerList
        {
            get { return _layerList; }
            set
            {
                SetProperty(ref _layerList, value);
            }
        }
        public FeatureLayer SelectedLayer
        {
            get { return _selectedLayer; }
            set
            {
                var oldLayer = _selectedLayer;
                SetProperty(ref _selectedLayer, value);
                if (_selectedLayer != null && _selectedLayer != oldLayer)
                {
                    // 加载新选图层的字段
                    LoadFields();
                    // 更新选择信息
                    UpdateSelectionInfo();
                }
            }
        }
        public ObservableCollection<FieldInfo> FieldList
        {
            get { return _fieldList; }
            set
            {
                SetProperty(ref _fieldList, value);
            }
        }
        public ObservableCollection<object> GroupFieldList
        {
            get { return _groupFieldList; }
            set
            {
                SetProperty(ref _groupFieldList, value);
            }
        }
        public object SelectedGroupField
        {
            get { return _selectedGroupField; }
            set
            {
                SetProperty(ref _selectedGroupField, value);
                // 不影响编号字段的选择
            }
        }
        public FieldInfo SelectedNumberField
        {
            get { return _selectedNumberField; }
            set
            {
                SetProperty(ref _selectedNumberField, value);
            }
        }
        public int StartNumber
        {
            get { return _startNumber; }
            set
            {
                SetProperty(ref _startNumber, value);
                // 更新中文表示
                ChineseStartNumber = ConvertToChinese(value);
            }
        }
        public string ChineseStartNumber
        {
            get { return _chineseStartNumber; }
            set
            {
                SetProperty(ref _chineseStartNumber, value);
            }
        }
        public string Prefix
        {
            get { return _prefix; }
            set
            {
                SetProperty(ref _prefix, value);
            }
        }
        public string Suffix
        {
            get { return _suffix; }
            set
            {
                SetProperty(ref _suffix, value);
            }
        }
        public bool OnlyEmptyRecords
        {
            get { return _onlyEmptyRecords; }
            set
            {
                SetProperty(ref _onlyEmptyRecords, value);
            }
        }
        public bool UseSelection
        {
            get { return _useSelection; }
            set { SetProperty(ref _useSelection, value); }
        }
        public bool HasSelection
        {
            get { return _hasSelection; }
            set { SetProperty(ref _hasSelection, value); }
        }
        public int SelectedCount
        {
            get { return _selectedCount; }
            set { SetProperty(ref _selectedCount, value); }
        }
        public string SelectionInfoText
        {
            get { return _selectionInfoText; }
            set
            {
                SetProperty(ref _selectionInfoText, value);
            }
        }
        public ICommand ExecuteCommand
        {
            get
            {
                if (_executeCommand == null)
                {
                    _executeCommand = new RelayCommand(() => ExecuteNumbering(), () => CanExecuteNumbering());
                }
                return _executeCommand;
            }
        }
        public ICommand ShowHelpCommand
        {
            get
            {
                if (_showHelpCommand == null)
                {
                    _showHelpCommand = new RelayCommand(() => ShowHelp());
                }
                return _showHelpCommand;
            }
        }
        public ICommand CancelCommand
        {
            get
            {
                if (_cancelCommand == null)
                {
                    _cancelCommand = new RelayCommand(() => CloseWindow());
                }
                return _cancelCommand;
            }
        }
        public ICommand RefreshLayersCommand
        {
            get
            {
                if (_refreshLayersCommand == null)
                {
                    _refreshLayersCommand = new RelayCommand(() => RefreshLayers());
                }
                return _refreshLayersCommand;
            }
        }
    }
}
