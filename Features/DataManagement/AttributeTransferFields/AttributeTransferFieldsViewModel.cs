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
    /// <summary>
    /// 字段信息（用于下拉与显示）
    /// </summary>
    public class FieldInfo : INotifyPropertyChanged
    {
        private bool _isSelected;
        public string Name { get; set; }
        public string Alias { get; set; }
        public FieldType FieldType { get; set; }
        public int Length { get; set; }
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }
        public string DisplayName => string.IsNullOrEmpty(Alias) || Name == Alias ? Name : $"{Name}({Alias})";
        public override string ToString() => DisplayName;
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// 数据集信息（要素图层或独立表）
    /// </summary>
    public class DatasetInfo
    {
        public FeatureLayer FeatureLayer { get; set; }
        public StandaloneTable StandaloneTable { get; set; }
        public string DisplayName { get; set; }
        public string Name
        {
            get
            {
                if (FeatureLayer != null) return FeatureLayer.Name;
                if (StandaloneTable != null) return StandaloneTable.Name;
                return string.Empty;
            }
        }
        public Table GetTable()
        {
            if (FeatureLayer != null) return FeatureLayer.GetTable();
            if (StandaloneTable != null) return StandaloneTable.GetTable();
            return null;
        }
    }

    /// <summary>
    /// 字段映射项（主字段 -> 从字段）
    /// </summary>
    public class FieldMappingItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        private string _sourceFieldName;
        private string _targetFieldName;
        public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }
        public string SourceFieldName { get => _sourceFieldName; set { _sourceFieldName = value; OnPropertyChanged(); } }
        public string TargetFieldName { get => _targetFieldName; set { _targetFieldName = value; OnPropertyChanged(); } }
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// RelayCommand
    /// </summary>


    /// <summary>
    /// 属性传递（字段）视图模型
    /// </summary>
    public partial class AttributeTransferFieldsViewModel : PropertyChangedBase
    {
        private ObservableCollection<DatasetInfo> _primaryList;
        private ObservableCollection<DatasetInfo> _secondaryList;
        private ObservableCollection<FieldInfo> _primaryFieldList;
        private ObservableCollection<FieldInfo> _secondaryFieldList;
        private ObservableCollection<FieldMappingItem> _fieldMappings;
        private DatasetInfo _selectedPrimary;
        private DatasetInfo _selectedSecondary;
        private FieldInfo _selectedPrimaryKeyField;
        private FieldInfo _selectedSecondaryKeyField;
        private bool _onlyFillEmpty = true;
        private bool _isProcessing;
        private string _logText;
        private bool _isPrimaryToSecondary = true;
        private CancellationTokenSource _cts;
        public AttributeTransferFieldsViewModel()
        {
            Initialize();
            InitializeCommands();
            LoadDatasets();
        }
        private static readonly HashSet<string> _blockedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "OBJECTID","FID","OID","GLOBALID","SHAPE","SHAPE_LENGTH","SHAPE_AREA",
            "CREATED_USER","CREATED_DATE","LAST_EDITED_USER","LAST_EDITED_DATE",
            "CREATOR","EDITOR","EDITDATE","CREATIONDATE","LASTUPDATE",
        };
    }
}
