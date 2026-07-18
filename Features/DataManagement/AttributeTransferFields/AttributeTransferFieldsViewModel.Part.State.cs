using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Features.DataManagement.AttributeTransferFields
{
    public partial class AttributeTransferFieldsViewModel
    {
        public ObservableCollection<DatasetInfo> PrimaryList { get => _primaryList; set => SetProperty(ref _primaryList, value); }
        public ObservableCollection<DatasetInfo> SecondaryList { get => _secondaryList; set => SetProperty(ref _secondaryList, value); }
        public ObservableCollection<FieldInfo> PrimaryFieldList { get => _primaryFieldList; set => SetProperty(ref _primaryFieldList, value); }
        public ObservableCollection<FieldInfo> SecondaryFieldList { get => _secondaryFieldList; set => SetProperty(ref _secondaryFieldList, value); }
        public ObservableCollection<FieldMappingItem> FieldMappings { get => _fieldMappings; set => SetProperty(ref _fieldMappings, value); }

        public DatasetInfo SelectedPrimary
        {
            get => _selectedPrimary;
            set
            {
                if (SetProperty(ref _selectedPrimary, value))
                {
                    LoadPrimaryFields();
                    RaiseAllCanExecutes();
                }
            }
        }
        public DatasetInfo SelectedSecondary
        {
            get => _selectedSecondary;
            set
            {
                if (SetProperty(ref _selectedSecondary, value))
                {
                    LoadSecondaryFields();
                    RaiseAllCanExecutes();
                }
            }
        }
        public FieldInfo SelectedPrimaryKeyField { get => _selectedPrimaryKeyField; set { if (SetProperty(ref _selectedPrimaryKeyField, value)) RaiseAllCanExecutes(); } }
        public FieldInfo SelectedSecondaryKeyField { get => _selectedSecondaryKeyField; set { if (SetProperty(ref _selectedSecondaryKeyField, value)) RaiseAllCanExecutes(); } }
        public bool OnlyFillEmpty { get => _onlyFillEmpty; set => SetProperty(ref _onlyFillEmpty, value); }

        public bool IsProcessing { get => _isProcessing; set { SetProperty(ref _isProcessing, value); RaiseAllCanExecutes(); } }
        public string LogText { get => _logText; set => SetProperty(ref _logText, value); }

        // 方向：主->从 与 从->主
        public bool IsPrimaryToSecondary
        {
            get => _isPrimaryToSecondary;
            set
            {
                if (SetProperty(ref _isPrimaryToSecondary, value))
                {
                    // 确保互斥
                    NotifyPropertyChanged(() => IsSecondaryToPrimary);
                }
            }
        }
        public bool IsSecondaryToPrimary
        {
            get => !_isPrimaryToSecondary;
            set { IsPrimaryToSecondary = !value; }
        }
        public RelayCommand RefreshDatasetsCommand { get; private set; }
        public RelayCommand AutoMapCommand { get; private set; }
        public RelayCommand AddMappingCommand { get; private set; }
        public RelayCommand RemoveSelectedMappingsCommand { get; private set; }
        public RelayCommand ClearMappingsCommand { get; private set; }
        public RelayCommand StartCommand { get; private set; }
        public RelayCommand StopCommand { get; private set; }
        public RelayCommand ShowHelpCommand { get; private set; }
        public RelayCommand ShowLogCommand { get; private set; }
    }
}
