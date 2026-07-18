using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Features.Analysis.DataPivot.Infrastructure;

namespace XIAOFUTools.Features.Analysis.DataPivot
{
    public class PivotInputDataset
    {
        public FeatureLayer FeatureLayer { get; set; }

        public StandaloneTable StandaloneTable { get; set; }

        public string DisplayName { get; set; }

        public string Name => FeatureLayer?.Name ?? StandaloneTable?.Name ?? string.Empty;

        public override string ToString() => DisplayName ?? Name;

        public object ToGeoprocessingInput()
        {
            return (object)FeatureLayer ?? StandaloneTable;
        }

        public Table OpenTable()
        {
            if (FeatureLayer != null)
                return FeatureLayer.GetTable();

            return StandaloneTable?.GetTable();
        }
    }

    public class PivotFieldOption : PropertyChangedBase
    {
        private bool _isSelected;

        public string FieldName { get; set; }

        public string Alias { get; set; }

        public FieldType FieldType { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public string DisplayText => string.IsNullOrWhiteSpace(Alias) || Alias == FieldName
            ? $"{FieldName}（{GetFieldTypeText(FieldType)}）"
            : $"{FieldName}（{Alias}）（{GetFieldTypeText(FieldType)}）";

        public override string ToString() => DisplayText;

        private static string GetFieldTypeText(FieldType fieldType)
        {
            return fieldType switch
            {
                FieldType.Double => "双精度",
                FieldType.Single => "单精度",
                FieldType.Integer => "整型",
                FieldType.SmallInteger => "短整型",
                FieldType.BigInteger => "长整型",
                FieldType.String => "文本",
                FieldType.Date => "日期",
                FieldType.DateOnly => "仅日期",
                FieldType.TimeOnly => "仅时间",
                FieldType.TimestampOffset => "时间戳偏移",
                FieldType.GUID => "GUID",
                FieldType.GlobalID => "GlobalID",
                FieldType.OID => "OID",
                _ => fieldType.ToString()
            };
        }
    }

    internal partial class DataPivotDockPaneViewModel : PropertyChangedBase
    {
        private const string TempPivotField = "XFT_PIVOT_KEY";
        private const string TempValueField = "XFT_PIVOT_VALUE";

        private CancellationTokenSource _cts;
        private readonly PivotWorkspaceResolver _workspaceResolver = new();

        private bool _isProcessing;
        private int _progress;
        private bool _isProgressIndeterminate;
        private string _statusMessage = "请选择输入表和透视参数。";
        private string _logContent = string.Empty;
        private PivotInputDataset _selectedInputDataset;
        private PivotFieldOption _selectedPivotField;
        private PivotFieldOption _selectedValueField;
        private string _selectedAggregationType;
        private string _outputGdbPath;
        private string _outputTableName = "TS_结果";

        private ICommand _runCommand;

        private ICommand _cancelCommand;

        private ICommand _refreshDatasetsCommand;

        private ICommand _browseOutputGdbCommand;

        private ICommand _showHelpCommand;

        public DataPivotDockPaneViewModel()
        {
            SelectedAggregationType = AggregationTypes[0];
            OutputGdbPath = GetProjectDefaultGdbPath();
            RefreshDatasets();
        }
    }
}
