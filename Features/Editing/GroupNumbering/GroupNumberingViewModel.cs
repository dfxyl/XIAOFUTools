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

namespace XIAOFUTools.Features.Editing.GroupNumbering
{
    /// <summary>
    /// 字段信息类，用于存储字段名和别名
    /// </summary>
    public class FieldInfo
    {
        /// <summary>
        /// 字段名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 字段别名
        /// </summary>
        public string Alias { get; set; }

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
    }

    /// <summary>
    /// 分组编号ViewModel
    /// </summary>
    internal partial class GroupNumberingViewModel : PropertyChangedBase
    {
        private dynamic _mapSelectionChangedToken;

        private ObservableCollection<FeatureLayer> _layerList;

        private FeatureLayer _selectedLayer;

        private ObservableCollection<FieldInfo> _fieldList;

        private ObservableCollection<object> _groupFieldList;

        private ObservableCollection<FieldInfo> _numberFieldList;

        private object _selectedGroupField;

        private FieldInfo _selectedNumberField;

        private ObservableCollection<int> _digitsList;

        private int _selectedDigit;

        private int _startNumber;

        private string _prefix;

        private string _suffix;

        private bool _onlyEmptyRecords;

        private bool _useSelection = true;

        private bool _hasSelection;

        private int _selectedCount;

        private string _selectionInfoText;

        private ICommand _executeCommand;

        private ICommand _showHelpCommand;

        private ICommand _cancelCommand;

        private ICommand _refreshLayersCommand;

        /// <summary>
        /// 构造函数
        /// </summary>
        public GroupNumberingViewModel()
        {
            // 初始化
            Initialize();
            
            // 异步加载图层，但不等待其完成
            LoadLayersAsync();

            // 订阅地图选择变化事件，实时更新选择信息
            _mapSelectionChangedToken = MapSelectionChangedEvent.Subscribe(OnMapSelectionChanged);
        }
    }
}

