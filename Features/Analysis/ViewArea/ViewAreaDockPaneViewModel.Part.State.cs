using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Framework.Events;
using XIAOFUTools.Features.User.Settings;

namespace XIAOFUTools.Features.Analysis.ViewArea
{
    internal partial class ViewAreaDockPaneViewModel
    {
        public string SelectionInfo
        {
            get => _selectionInfo;
            set => SetProperty(ref _selectionInfo, value);
        }

        // 小数位数
        public int DecimalPlaces
        {
            get => SettingsManager.Settings.ViewArea.DefaultDecimalPlaces;
            set
            {
                if (SettingsManager.Settings.ViewArea.DefaultDecimalPlaces != value && value >= 0 && value <= 10)
                {
                    SettingsManager.Settings.ViewArea.DefaultDecimalPlaces = value;
                    SettingsManager.SaveSettings();
                    NotifyPropertyChanged();
                    RefreshCalculation();
                }
            }
        }
        public ObservableCollection<CalculationResult> CombinedAreaResults
        {
            get => _combinedAreaResults;
            set => SetProperty(ref _combinedAreaResults, value);
        }
        public ObservableCollection<CalculationResult> CombinedLengthResults
        {
            get => _combinedLengthResults;
            set => SetProperty(ref _combinedLengthResults, value);
        }
        public ObservableCollection<LayerCalculationSummary> LayerResults
        {
            get => _layerResults;
            set => SetProperty(ref _layerResults, value);
        }
        public bool ShowLayerResults
        {
            get => _showLayerResults;
            set => SetProperty(ref _showLayerResults, value);
        }
        public bool IsCalculating
        {
            get => _isCalculating;
            set => SetProperty(ref _isCalculating, value);
        }
        public bool HasAreaResults
        {
            get => _hasAreaResults;
            set => SetProperty(ref _hasAreaResults, value);
        }
        public bool HasLengthResults
        {
            get => _hasLengthResults;
            set => SetProperty(ref _hasLengthResults, value);
        }

        // 是否有任何结果
        public bool HasResults => HasAreaResults || HasLengthResults;

        public ICommand RefreshCommand { get; }
        public ICommand CopyResultsCommand { get; }
    }
}
