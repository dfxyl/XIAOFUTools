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
    /// <summary>
    /// 字段信息类，用于存储字段名和别名，支持选择状态
    /// </summary>
    public class FieldInfo : INotifyPropertyChanged
    {
        private bool _isSelected;

        /// <summary>
        /// 字段名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 字段别名
        /// </summary>
        public string Alias { get; set; }

        /// <summary>
        /// 字段类型
        /// </summary>
        public FieldType FieldType { get; set; }

        /// <summary>
        /// 字段长度
        /// </summary>
        public int Length { get; set; }

        /// <summary>
        /// 是否被选中
        /// </summary>
        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 显示文本，格式为"字段名称(别名)"
        /// </summary>
        public string DisplayName
        {
            get
            {
                if (string.IsNullOrEmpty(Alias) || Name == Alias)
                    return Name;
                else
                    return $"{Name}({Alias})";
            }
        }

        /// <summary>
        /// 重写ToString方法，返回显示文本
        /// </summary>
        public override string ToString()
        {
            return DisplayName;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 图层信息类，支持选择状态
    /// </summary>
    public class LayerInfo : INotifyPropertyChanged
    {
        private bool _isSelected;

        /// <summary>
        /// 图层对象
        /// </summary>
        public FeatureLayer Layer { get; set; }

        /// <summary>
        /// 是否被选中
        /// </summary>
        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 图层名称
        /// </summary>
        public string Name => Layer?.Name ?? "";

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 字段复制工具视图模型
    /// </summary>
    public partial class FieldCopyToolViewModel : PropertyChangedBase
    {

        private ObservableCollection<FeatureLayer> _sourceLayerList;
        private ObservableCollection<FieldInfo> _fieldList;
        private ObservableCollection<LayerInfo> _targetLayerList;
        private FeatureLayer _selectedSourceLayer;
        private bool _isProcessing;
        private string _logText;

        /// <summary>
        /// 构造函数
        /// </summary>
        public FieldCopyToolViewModel()
        {
            Initialize();
            InitializeCommands();
            LoadLayers();
        }
    }
}
