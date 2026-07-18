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
using XIAOFUTools.Features.Cartography.MapSeriesExport.Infrastructure;
using XIAOFUTools.Features.Cartography.MapSeriesExport.Presentation;

namespace XIAOFUTools.Features.Cartography.MapSeriesExport
{
    /// <summary>
    /// 驱动制图视图模型
    /// </summary>
    public partial class MapSeriesExportViewModel : INotifyPropertyChanged
    {
        private LayoutProjectItem _selectedLayout;
        private string _mapSeriesStatus = "请选择布局";
        private SolidColorBrush _mapSeriesStatusColor = new SolidColorBrush(Colors.Gray);
        private string _outputFolder = "";
        private string _selectedExportMode = "全部导出";
        private string _pageRange = "";
        private Visibility _pageRangeVisibility = Visibility.Collapsed;
        private string _selectedFormat = "PDF";
        private string _resolution = "300";
        private bool _isRunning = false;
        private CancellationTokenSource _cancellationTokenSource;

        // 坐标表设置
        private CoordinateTableSettings _coordinateTableSettings;
        private string _currentCoordinateTableGroupName = null;
        private int _lastNavigatedPageIndex = -1;
        private const string EllipsisMarker = "•••";
        private ICollectionView _mapSeriesPagesView;
        private string _pageSearchText = string.Empty;
        private string _selectedPageSearchMode = "页码";

        private readonly MapSeriesSettingsStore _settingsStore = new();
        private readonly MapSeriesOutputFolderStore _outputFolderStore = new();
        private readonly IMapSeriesSettingsWindowService _settingsWindowService = new MapSeriesSettingsWindowService();
        private readonly ArcGisMapSeriesPageReader _pageReader = new();
        private readonly ArcGisLayoutExporter _layoutExporter = new();
        private readonly ArcGisMapSeriesIntersectAreaReader _intersectAreaReader = new();

        public MapSeriesExportViewModel()
        {
            RebuildMapSeriesPageView();
            InitializeCommands();
            LoadLayouts();
            _outputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "地图系列导出");

            // 从文件加载保存的设置
            _coordinateTableSettings = _settingsStore.Load() ?? new CoordinateTableSettings();
        }

        private ObservableCollection<MapSeriesPageItem> _mapSeriesPages = new ObservableCollection<MapSeriesPageItem>();
    }

    /// <summary>
    /// 地图系列页面项
    /// </summary>
    public class MapSeriesPageItem : INotifyPropertyChanged
    {
        private bool _isSelected = true;
        public int PageIndex { get; set; }
        public string PageName { get; set; }
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// 简单的命令实现
    /// </summary>


    /// <summary>
    /// 带参数的命令实现
    /// </summary>

}
