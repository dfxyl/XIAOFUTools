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

namespace XIAOFUTools.Features.Analysis.DataPivot
{
    internal partial class DataPivotDockPaneViewModel
    {

        public ObservableCollection<PivotInputDataset> InputDatasets { get; } = new ObservableCollection<PivotInputDataset>();

        public ObservableCollection<PivotFieldOption> RegionFields { get; } = new ObservableCollection<PivotFieldOption>();

        public ObservableCollection<PivotFieldOption> PivotFields { get; } = new ObservableCollection<PivotFieldOption>();

        public ObservableCollection<PivotFieldOption> ValueFields { get; } = new ObservableCollection<PivotFieldOption>();

        public ObservableCollection<string> AggregationTypes { get; } = new ObservableCollection<string>
        {
            "求和",
            "计数",
            "平均值",
            "最大值",
            "最小值",
            "中位数",
            "极差",
            "标准差",
            "方差"
        };

        public PivotInputDataset SelectedInputDataset
        {
            get => _selectedInputDataset;
            set
            {
                if (SetProperty(ref _selectedInputDataset, value))
                {
                    OutputTableName = BuildDefaultOutputTableName(value?.Name);
                    NotifyPropertyChanged(() => CanProcess);
                    LoadFieldOptions();
                }
            }
        }

        public PivotFieldOption SelectedPivotField
        {
            get => _selectedPivotField;
            set
            {
                if (SetProperty(ref _selectedPivotField, value))
                    NotifyPropertyChanged(() => CanProcess);
            }
        }

        public PivotFieldOption SelectedValueField
        {
            get => _selectedValueField;
            set
            {
                if (SetProperty(ref _selectedValueField, value))
                    NotifyPropertyChanged(() => CanProcess);
            }
        }

        public string SelectedAggregationType
        {
            get => _selectedAggregationType;
            set
            {
                if (SetProperty(ref _selectedAggregationType, value))
                    NotifyPropertyChanged(() => CanProcess);
            }
        }

        public string OutputGdbPath
        {
            get => _outputGdbPath;
            set
            {
                if (SetProperty(ref _outputGdbPath, value))
                {
                    NotifyPropertyChanged(() => OutputTablePath);
                    NotifyPropertyChanged(() => CanProcess);
                }
            }
        }

        public string OutputTableName
        {
            get => _outputTableName;
            set
            {
                if (SetProperty(ref _outputTableName, value))
                {
                    NotifyPropertyChanged(() => OutputTablePath);
                    NotifyPropertyChanged(() => CanProcess);
                }
            }
        }

        public string OutputTablePath
        {
            get
            {
                if (string.IsNullOrWhiteSpace(OutputGdbPath) || string.IsNullOrWhiteSpace(OutputTableName))
                    return string.Empty;

                return Path.Combine(OutputGdbPath.Trim(), OutputTableName.Trim());
            }
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                if (SetProperty(ref _isProcessing, value))
                    NotifyPropertyChanged(() => CanProcess);
            }
        }

        public int Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        public bool IsProgressIndeterminate
        {
            get => _isProgressIndeterminate;
            set => SetProperty(ref _isProgressIndeterminate, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public string LogContent
        {
            get => _logContent;
            set => SetProperty(ref _logContent, value);
        }

        public bool CanProcess =>
            !IsProcessing &&
            SelectedInputDataset != null &&
            SelectedPivotField != null &&
            SelectedValueField != null &&
            !string.IsNullOrWhiteSpace(SelectedAggregationType) &&
            RegionFields.Any(f => f.IsSelected) &&
            !string.IsNullOrWhiteSpace(OutputGdbPath) &&
            !string.IsNullOrWhiteSpace(OutputTableName);
        public ICommand RunCommand => _runCommand ??= new RelayCommand(async () => await ExecuteAsync(), () => CanProcess);
        public ICommand CancelCommand => _cancelCommand ??= new RelayCommand(Cancel, () => IsProcessing);
        public ICommand RefreshDatasetsCommand => _refreshDatasetsCommand ??= new RelayCommand(RefreshDatasets, () => !IsProcessing);
        public ICommand BrowseOutputGdbCommand => _browseOutputGdbCommand ??= new RelayCommand(BrowseOutputGdb, () => !IsProcessing);
        public ICommand ShowHelpCommand => _showHelpCommand ??= new RelayCommand(ShowHelp);
    }
}
