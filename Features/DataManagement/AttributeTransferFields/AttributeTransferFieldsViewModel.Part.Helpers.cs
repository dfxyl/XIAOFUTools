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
        private void Initialize()
        {
            _primaryList = new ObservableCollection<DatasetInfo>();
            _secondaryList = new ObservableCollection<DatasetInfo>();
            _primaryFieldList = new ObservableCollection<FieldInfo>();
            _secondaryFieldList = new ObservableCollection<FieldInfo>();
            _fieldMappings = new ObservableCollection<FieldMappingItem>();
            _fieldMappings.CollectionChanged += FieldMappings_CollectionChanged;
            _isProcessing = false;
            _logText = string.Empty;
        }
        private void MappingItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FieldMappingItem.SourceFieldName) ||
                e.PropertyName == nameof(FieldMappingItem.TargetFieldName) ||
                e.PropertyName == nameof(FieldMappingItem.IsSelected))
            {
                RaiseAllCanExecutes();
            }
        }
        private void InitializeCommands()
        {
            RefreshDatasetsCommand = new RelayCommand(() => LoadDatasets(), () => !IsProcessing);
            AutoMapCommand = new RelayCommand(() => AutoMap(), () => CanAutoMap());
            AddMappingCommand = new RelayCommand(() => AddMapping(), () => !IsProcessing);
            RemoveSelectedMappingsCommand = new RelayCommand(() => RemoveSelectedMappings(), () => FieldMappings.Any() && !IsProcessing);
            ClearMappingsCommand = new RelayCommand(() => ClearMappings(), () => FieldMappings.Any() && !IsProcessing);
            StartCommand = new RelayCommand(() => StartTransfer(), () => CanStart());
            StopCommand = new RelayCommand(() => StopTransfer(), () => IsProcessing);
            ShowHelpCommand = new RelayCommand(() => ShowHelp());
            ShowLogCommand = new RelayCommand(() => ShowLog());
        }
        private bool CanAutoMap()
        {
            return !IsProcessing && PrimaryFieldList.Any() && SecondaryFieldList.Any();
        }
        private void AutoMap()
        {
            int before = FieldMappings.Count;
            var priNames = PrimaryFieldList.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var secNames = SecondaryFieldList.Select(f => f.Name).ToList();
            foreach (var name in secNames)
            {
                if (priNames.Contains(name) && !FieldMappings.Any(m => m.SourceFieldName?.Equals(name, StringComparison.OrdinalIgnoreCase) == true || m.TargetFieldName?.Equals(name, StringComparison.OrdinalIgnoreCase) == true))
                {
                    FieldMappings.Add(new FieldMappingItem { IsSelected = true, SourceFieldName = name, TargetFieldName = name });
                }
            }
            LogInfo($"自动匹配完成，共新增 {FieldMappings.Count - before} 条映射。");
            RaiseAllCanExecutes();
        }
        private void AddMapping()
        {
            FieldMappings.Add(new FieldMappingItem { IsSelected = true });
        }
        private void ClearMappings()
        {
            FieldMappings.Clear();
        }
        private void StopTransfer()
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
                LogWarning("已请求停止操作...");
            }
        }
        private static bool IsNullOrEmptyValue(object v)
        {
            if (v == null || v is DBNull) return true;
            switch (v)
            {
                case string s:
                    return string.IsNullOrWhiteSpace(s);
                case short or int or long or float or double or decimal:
                    // 数值型：认为 null 才是空，0 不是空
                    return false;
                case bool b:
                    // 布尔：null 才是空
                    return false;
                case DateTime dt:
                    // 1900-01-01 或 MinValue 视为“空”常用占位
                    return dt == DateTime.MinValue || dt.Year <= 1900;
                case Guid g:
                    return g == Guid.Empty;
                default:
                    return false;
            }
        }
        private static string NormalizeKey(object v)
        {
            if (v == null || v is DBNull) return string.Empty;
            return v.ToString()?.Trim()?.ToUpperInvariant() ?? string.Empty;
        }
        private bool IsTypeCompatible(Table src, Table tgt, string srcField, string tgtField)
        {
            try
            {
                var sdef = src.GetDefinition();
                var tdef = tgt.GetDefinition();
                var sf = sdef.GetFields().FirstOrDefault(f => f.Name.Equals(srcField, StringComparison.OrdinalIgnoreCase));
                var tf = tdef.GetFields().FirstOrDefault(f => f.Name.Equals(tgtField, StringComparison.OrdinalIgnoreCase));
                if (sf == null || tf == null) return false;

                if (sf.FieldType == tf.FieldType) return true;
                // 允许数字之间互转
                bool sNum = IsNumericType(sf.FieldType), tNum = IsNumericType(tf.FieldType);
                if (sNum && tNum) return true;
                // 允许任意到字符串
                if (tf.FieldType == FieldType.String) return true;
                return false;
            }
            catch { return false; }
        }
        private bool IsNumericType(FieldType t)
        {
            return t == FieldType.Integer || t == FieldType.SmallInteger || t == FieldType.Double || t == FieldType.Single;
        }
    }
}
