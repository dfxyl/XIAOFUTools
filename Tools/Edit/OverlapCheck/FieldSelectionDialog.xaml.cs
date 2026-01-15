using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using ArcGIS.Core.Data;

namespace XIAOFUTools.Tools.OverlapCheck
{
    /// <summary>
    /// 字段选择对话框
    /// </summary>
    public partial class FieldSelectionDialog : Window, INotifyPropertyChanged
    {
        private ObservableCollection<FieldItem> _fieldItems;
        private string _selectionSummary;

        public ObservableCollection<FieldItem> FieldItems
        {
            get => _fieldItems;
            set
            {
                _fieldItems = value;
                OnPropertyChanged();
                UpdateSelectionSummary();
            }
        }

        public string SelectionSummary
        {
            get => _selectionSummary;
            set
            {
                _selectionSummary = value;
                OnPropertyChanged();
            }
        }

        public List<string> SelectedFieldNames { get; private set; }

        public FieldSelectionDialog(List<Field> fields, List<string> preSelectedFields = null)
        {
            InitializeComponent();
            DataContext = this;
            
            // 初始化字段列表
            FieldItems = new ObservableCollection<FieldItem>();
            
            foreach (var field in fields)
            {
                // 排除几何字段和ObjectID字段
                if (field.FieldType != FieldType.Geometry && 
                    field.FieldType != FieldType.OID)
                {
                    var fieldItem = new FieldItem
                    {
                        FieldName = field.Name,
                        DisplayName = GetFieldDisplayName(field),
                        IsSelected = preSelectedFields?.Contains(field.Name) ?? false
                    };
                    
                    fieldItem.PropertyChanged += FieldItem_PropertyChanged;
                    FieldItems.Add(fieldItem);
                }
            }
            
            UpdateSelectionSummary();
        }

        private void FieldItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FieldItem.IsSelected))
            {
                UpdateSelectionSummary();
            }
        }

        private void UpdateSelectionSummary()
        {
            if (FieldItems != null)
            {
                int selectedCount = FieldItems.Count(f => f.IsSelected);
                int totalCount = FieldItems.Count;
                SelectionSummary = $"已选择 {selectedCount} 个字段，共 {totalCount} 个字段";
            }
        }

        /// <summary>
        /// 获取字段显示名称：字段名称（别名）（类型）
        /// </summary>
        private string GetFieldDisplayName(Field field)
        {
            string fieldName = field.Name;
            string alias = field.AliasName;
            string fieldType = GetFieldTypeDisplayName(field.FieldType);

            // 如果别名存在且与字段名不同，则显示别名
            if (!string.IsNullOrEmpty(alias) && alias != fieldName)
            {
                return $"{fieldName}（{alias}）（{fieldType}）";
            }
            else
            {
                return $"{fieldName}（{fieldType}）";
            }
        }

        private string GetFieldTypeDisplayName(FieldType fieldType)
        {
            return fieldType switch
            {
                FieldType.String => "文本",
                FieldType.Integer => "整数",
                FieldType.SmallInteger => "短整数",
                FieldType.Double => "双精度",
                FieldType.Single => "单精度",
                FieldType.Date => "日期",
                FieldType.GUID => "GUID",
                FieldType.GlobalID => "全局ID",
                FieldType.XML => "XML",
                FieldType.Raster => "栅格",
                FieldType.Blob => "二进制",
                _ => fieldType.ToString()
            };
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in FieldItems)
            {
                item.IsSelected = true;
            }
        }

        private void SelectNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in FieldItems)
            {
                item.IsSelected = false;
            }
        }

        private void InvertSelection_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in FieldItems)
            {
                item.IsSelected = !item.IsSelected;
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            SelectedFieldNames = FieldItems.Where(f => f.IsSelected).Select(f => f.FieldName).ToList();
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 字段项数据模型
    /// </summary>
    public class FieldItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public string FieldName { get; set; }
        public string DisplayName { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
