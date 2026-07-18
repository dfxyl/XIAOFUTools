using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using Microsoft.Win32;
using XIAOFUTools.Features.Editing.Boundary.LayoutCoordinateTable;

namespace XIAOFUTools.Features.Cartography.MapSeriesExport
{
    public partial class MapSeriesExportViewModel
    {

        public ObservableCollection<LayoutProjectItem> Layouts { get; } = new ObservableCollection<LayoutProjectItem>();

        public LayoutProjectItem SelectedLayout
        {
            get => _selectedLayout;
            set
            {
                _selectedLayout = value;
                OnPropertyChanged();
                LoadMapSeriesPages();
            }
        }

        public string MapSeriesStatus
        {
            get => _mapSeriesStatus;
            set { _mapSeriesStatus = value; OnPropertyChanged(); }
        }

        public SolidColorBrush MapSeriesStatusColor
        {
            get => _mapSeriesStatusColor;
            set { _mapSeriesStatusColor = value; OnPropertyChanged(); }
        }
        public ObservableCollection<MapSeriesPageItem> MapSeriesPages
        {
            get => _mapSeriesPages;
            set
            {
                _mapSeriesPages = value ?? new ObservableCollection<MapSeriesPageItem>();
                OnPropertyChanged();
                RebuildMapSeriesPageView();
            }
        }

        public ICollectionView MapSeriesPagesView
        {
            get => _mapSeriesPagesView;
            private set { _mapSeriesPagesView = value; OnPropertyChanged(); }
        }

        public ObservableCollection<string> PageSearchModes { get; } = new ObservableCollection<string> { "页码", "名称" };

        public string SelectedPageSearchMode
        {
            get => _selectedPageSearchMode;
            set
            {
                _selectedPageSearchMode = string.IsNullOrWhiteSpace(value) ? "页码" : value;
                OnPropertyChanged();
                ApplyMapSeriesPageFilter();
            }
        }

        public string PageSearchText
        {
            get => _pageSearchText;
            set
            {
                _pageSearchText = value ?? string.Empty;
                OnPropertyChanged();
                ApplyMapSeriesPageFilter();
            }
        }

        public string OutputFolder
        {
            get => _outputFolder;
            set { _outputFolder = value; OnPropertyChanged(); }
        }

        public ObservableCollection<string> ExportModes { get; } = new ObservableCollection<string> { "全部导出", "选中导出", "指定页面" };

        public string SelectedExportMode
        {
            get => _selectedExportMode;
            set
            {
                _selectedExportMode = value;
                OnPropertyChanged();
                PageRangeVisibility = value == "指定页面" ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public string PageRange
        {
            get => _pageRange;
            set { _pageRange = value; OnPropertyChanged(); }
        }

        public Visibility PageRangeVisibility
        {
            get => _pageRangeVisibility;
            set { _pageRangeVisibility = value; OnPropertyChanged(); }
        }

        public ObservableCollection<string> ExportFormats { get; } = new ObservableCollection<string> { "PDF", "JPG", "PNG", "TIF" };

        public string SelectedFormat
        {
            get => _selectedFormat;
            set { _selectedFormat = value; OnPropertyChanged(); }
        }

        public string Resolution
        {
            get => _resolution;
            set { _resolution = value; OnPropertyChanged(); }
        }

        public bool IsRunning
        {
            get => _isRunning;
            set { _isRunning = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 坐标表设置
        /// </summary>
        public CoordinateTableSettings CoordinateTableSettings
        {
            get => _coordinateTableSettings;
            set { _coordinateTableSettings = value; OnPropertyChanged(); OnPropertyChanged(nameof(CoordinateTableStatusText)); }
        }

        /// <summary>
        /// 坐标表状态文本
        /// </summary>
        public string CoordinateTableStatusText
        {
            get
            {
                if (_coordinateTableSettings?.EnableCoordinateTable == true &&
                    _coordinateTableSettings?.EnableIntersectTable == true)
                {
                    return "坐标表+交集表";
                }

                if (_coordinateTableSettings?.EnableCoordinateTable == true)
                {
                    return "坐标表已启用";
                }

                return _coordinateTableSettings?.EnableIntersectTable == true ? "交集表已启用" : "未启用";
            }
        }


        public ICommand RefreshLayoutsCommand { get; private set; }
        public ICommand RefreshMapSeriesCommand { get; private set; }
        public ICommand SelectAllCommand { get; private set; }
        public ICommand InvertSelectionCommand { get; private set; }
        public ICommand NavigateToPageCommand { get; private set; }
        public ICommand BrowseFolderCommand { get; private set; }
        public ICommand ShowHelpCommand { get; private set; }
        public ICommand StopCommand { get; private set; }
        public ICommand ExportCommand { get; private set; }
        public ICommand OpenSettingsCommand { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
