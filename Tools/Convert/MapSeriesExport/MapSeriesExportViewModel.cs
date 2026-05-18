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
using XIAOFUTools.Tools.Edit.Boundary.LayoutCoordinateTable;

namespace XIAOFUTools.Tools.Output.MapSeriesExport
{
    /// <summary>
    /// 驱动制图视图模型
    /// </summary>
    public class MapSeriesExportViewModel : INotifyPropertyChanged
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
        
        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XIAOFUTools", "MapSeriesSettings.json");

        public MapSeriesExportViewModel()
        {
            RebuildMapSeriesPageView();
            InitializeCommands();
            LoadLayouts();
            _outputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "地图系列导出");
            
            // 从文件加载保存的设置
            _coordinateTableSettings = LoadSettingsFromFile() ?? new CoordinateTableSettings();
        }
        
        /// <summary>
        /// 从文件加载设置
        /// </summary>
        private CoordinateTableSettings LoadSettingsFromFile()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    return System.Text.Json.JsonSerializer.Deserialize<CoordinateTableSettings>(json);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载设置失败: {ex.Message}");
            }
            return null;
        }

        #region 属性

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

        private ObservableCollection<MapSeriesPageItem> _mapSeriesPages = new ObservableCollection<MapSeriesPageItem>();
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

        #endregion

        #region 命令

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

        private void InitializeCommands()
        {
            RefreshLayoutsCommand = new RelayCommand(RefreshLayouts);
            RefreshMapSeriesCommand = new RelayCommand(RefreshMapSeries);
            SelectAllCommand = new RelayCommand(SelectAll);
            InvertSelectionCommand = new RelayCommand(InvertSelection);
            NavigateToPageCommand = new RelayCommand<MapSeriesPageItem>(NavigateToPage);
            BrowseFolderCommand = new RelayCommand(BrowseFolder);
            ShowHelpCommand = new RelayCommand(ShowHelp);
            StopCommand = new RelayCommand(Stop);
            ExportCommand = new RelayCommand(async () => await Export(), CanExport);
            OpenSettingsCommand = new RelayCommand(OpenSettings);
        }

        #endregion

        #region 方法

        private async void LoadLayouts()
        {
            try
            {
                await QueuedTask.Run(() =>
                {
                    var project = Project.Current;
                    if (project == null) return;
                    var layouts = project.GetItems<LayoutProjectItem>().ToList();
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Layouts.Clear();
                        foreach (var layout in layouts) Layouts.Add(layout);
                        if (Layouts.Count > 0) SelectedLayout = Layouts[0];
                    });
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"错误: {ex.Message}");
            }
        }

        private void RefreshLayouts() => LoadLayouts();

        private async void LoadMapSeriesPages()
        {
            try
            {
                if (SelectedLayout == null)
                {
                    MapSeriesStatus = "请选择布局";
                    MapSeriesStatusColor = new SolidColorBrush(Colors.Gray);
                    Application.Current.Dispatcher.Invoke(() => MapSeriesPages.Clear());
                    return;
                }

                await QueuedTask.Run(() =>
                {
                    try
                    {
                    var layout = SelectedLayout.GetLayout();
                    if (layout == null)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MapSeriesStatus = "无法获取布局";
                            MapSeriesStatusColor = new SolidColorBrush(Colors.Red);
                            MapSeriesPages.Clear();
                        });
                        return;
                    }

                    var mapSeries = layout.MapSeries;
                    if (mapSeries == null || !mapSeries.Enabled)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MapSeriesStatus = "该布局未启用地图系列";
                            MapSeriesStatusColor = new SolidColorBrush(Colors.Orange);
                            MapSeriesPages.Clear();
                        });
                        return;
                    }

                    var pageCount = mapSeries.PageCount;
                    var pageNames = new List<string>();
                    
                    // 优化：直接从索引图层批量查询页面名称，使用与地图系列相同的排序
                    var spatialMapSeries = mapSeries as SpatialMapSeries;
                    var smsDefinition = mapSeries.GetDefinition() as CIMSpatialMapSeries;
                    
                    if (spatialMapSeries != null && smsDefinition != null)
                    {
                        var indexLayer = spatialMapSeries.IndexLayer as FeatureLayer;
                        string nameField = smsDefinition.NameField;
                        string sortField = smsDefinition.SortField;
                        bool sortAscending = smsDefinition.SortAscending;
                        
                        System.Diagnostics.Debug.WriteLine($"[驱动制图] 索引图层: {indexLayer?.Name}, 名称字段: {nameField}, 排序字段: {sortField}, 页数: {pageCount}");
                        
                        if (indexLayer != null && !string.IsNullOrEmpty(nameField))
                        {
                            try
                            {
                                using (var table = indexLayer.GetTable())
                                {
                                    // 先尝试带排序的查询
                                    QueryFilter queryFilter;
                                    var sortFieldName = !string.IsNullOrEmpty(sortField) ? sortField : nameField;
                                    var sortOrder = sortAscending ? "ASC" : "DESC";
                                    
                                    try
                                    {
                                        // 尝试使用 ORDER BY（某些数据源可能不支持）
                                        queryFilter = new QueryFilter
                                        {
                                            SubFields = nameField,
                                            PostfixClause = $"ORDER BY {sortFieldName} {sortOrder}"
                                        };
                                        
                                        using (var cursor = table.Search(queryFilter))
                                        {
                                            while (cursor.MoveNext())
                                            {
                                                using (var row = cursor.Current)
                                                {
                                                    var name = row[nameField]?.ToString() ?? "";
                                                    pageNames.Add(string.IsNullOrEmpty(name) ? "未命名" : name);
                                                }
                                            }
                                        }
                                        System.Diagnostics.Debug.WriteLine($"[驱动制图] 批量查询成功，获取 {pageNames.Count} 条记录");
                                    }
                                    catch (Exception orderEx)
                                    {
                                        // ORDER BY 不支持，使用无排序查询
                                        System.Diagnostics.Debug.WriteLine($"[驱动制图] ORDER BY 不支持: {orderEx.Message}，使用无排序查询");
                                        queryFilter = new QueryFilter { SubFields = nameField };
                                        
                                        using (var cursor = table.Search(queryFilter))
                                        {
                                            while (cursor.MoveNext())
                                            {
                                                using (var row = cursor.Current)
                                                {
                                                    var name = row[nameField]?.ToString() ?? "";
                                                    pageNames.Add(string.IsNullOrEmpty(name) ? "未命名" : name);
                                                }
                                            }
                                        }
                                        System.Diagnostics.Debug.WriteLine($"[驱动制图] 无排序查询成功，获取 {pageNames.Count} 条记录");
                                    }
                                }
                            }
                            catch (Exception queryEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"[驱动制图] 批量查询失败: {queryEx.Message}");
                            }
                        }
                    }
                    
                    // 如果批量查询失败或结果为空，直接用页码显示（不遍历，避免卡死）
                    if (pageNames.Count == 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[驱动制图] 批量查询无结果，使用页码显示 {pageCount} 页");
                        for (int i = 1; i <= pageCount; i++)
                        {
                            pageNames.Add($"页面 {i}");
                        }
                    }

                    // 先在后台线程准备好所有数据项（使用 List 而非 ObservableCollection，性能更好）
                    System.Diagnostics.Debug.WriteLine($"[驱动制图] 开始创建 {pageNames.Count} 个页面项...");
                    var newPages = new List<MapSeriesPageItem>(pageNames.Count);
                    for (int i = 0; i < pageNames.Count; i++)
                    {
                        newPages.Add(new MapSeriesPageItem { PageIndex = i + 1, PageName = pageNames[i], IsSelected = true });
                    }
                    System.Diagnostics.Debug.WriteLine($"[驱动制图] 页面项创建完成，准备更新UI...");
                    
                    // 在UI线程上异步更新，避免阻塞
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        System.Diagnostics.Debug.WriteLine($"[驱动制图] UI更新开始...");
                        MapSeriesStatus = $"地图系列已启用，共 {pageNames.Count} 页";
                        MapSeriesStatusColor = new SolidColorBrush(Colors.Green);
                        // 直接替换为新的 ObservableCollection
                        MapSeriesPages = new ObservableCollection<MapSeriesPageItem>(newPages);
                        System.Diagnostics.Debug.WriteLine($"[驱动制图] UI更新完成");
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MapSeriesStatus = $"加载失败: {ex.Message}";
                        MapSeriesStatusColor = new SolidColorBrush(Colors.Red);
                        MapSeriesPages.Clear();
                    });
                }
            });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"错误: {ex.Message}");
            }
        }

        private void RefreshMapSeries()
        {
            LoadMapSeriesPages();
        }

        private void RebuildMapSeriesPageView()
        {
            MapSeriesPagesView = CollectionViewSource.GetDefaultView(MapSeriesPages);
            if (MapSeriesPagesView != null)
            {
                MapSeriesPagesView.Filter = MapSeriesPageFilter;
            }
            ApplyMapSeriesPageFilter();
        }

        private void ApplyMapSeriesPageFilter()
        {
            MapSeriesPagesView?.Refresh();
        }

        private bool MapSeriesPageFilter(object obj)
        {
            if (obj is not MapSeriesPageItem page)
            {
                return false;
            }

            var keyword = PageSearchText?.Trim();
            if (string.IsNullOrEmpty(keyword))
            {
                return true;
            }

            if (SelectedPageSearchMode == "名称")
            {
                return (page.PageName ?? string.Empty).IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            return page.PageIndex.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase);
        }

        private IEnumerable<MapSeriesPageItem> GetVisiblePages()
        {
            if (MapSeriesPagesView != null)
            {
                return MapSeriesPagesView.Cast<object>().OfType<MapSeriesPageItem>();
            }
            return MapSeriesPages;
        }

        private void SelectAll()
        {
            foreach (var page in GetVisiblePages())
            {
                page.IsSelected = true;
            }
        }

        private void InvertSelection()
        {
            foreach (var page in GetVisiblePages())
            {
                page.IsSelected = !page.IsSelected;
            }
        }

        private async void NavigateToPage(MapSeriesPageItem page)
        {
            try
            {
                if (page == null || SelectedLayout == null) return;
                
                // 避免重复切换同一页面
                if (_lastNavigatedPageIndex == page.PageIndex) return;
                _lastNavigatedPageIndex = page.PageIndex;
                
                await QueuedTask.Run(async () =>
                {
                    try
                    {
                        var layout = SelectedLayout.GetLayout();
                        var mapSeries = layout?.MapSeries;
                        if (mapSeries != null && mapSeries.Enabled && page.PageIndex >= 1 && page.PageIndex <= mapSeries.PageCount)
                        {
                            // 切换页面前先清除之前的坐标表
                            if (HasPageGeneratedContent())
                            {
                                ClearCoordinateTableElements(layout);
                            }
                            
                            // 切换地图系列页面
                            mapSeries.SetCurrentPageNumber(page.PageIndex.ToString());
                            
                            // 如果启用了坐标表生成，则生成新的坐标表
                            if (HasPageGeneratedContent())
                            {
                                GenerateCoordinateTableForCurrentPage(layout, mapSeries);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"导航页面失败: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"错误: {ex.Message}");
            }
        }

        private void BrowseFolder()
        {
            var dialog = new OpenFolderDialog() { Title = "选择输出文件夹", InitialDirectory = OutputFolder };
            if (dialog.ShowDialog() == true) OutputFolder = dialog.FolderName;
        }

        private void ShowHelp()
        {
            string helpContent = 
                "【地图系列批量导出工具】\n\n" +
                "━━━━━━ 基本功能 ━━━━━━\n" +
                "基于ArcGIS Pro地图系列批量导出布局页面，支持自动生成坐标表。\n\n" +
                "━━━━━━ 主界面说明 ━━━━━━\n" +
                "• 布局选择：选择包含地图系列的布局\n" +
                "• 页面列表：显示所有页面，勾选要导出的页面\n" +
                "  - 双击行可切换到该页面预览\n" +
                "  - 全选/反选按钮快速选择\n" +
                "  - 搜索支持按页码或名称筛选\n" +
                "• 输出文件夹：导出文件保存位置\n" +
                "• 导出方式：\n" +
                "  - 全部导出：导出所有页面\n" +
                "  - 选中导出：导出勾选的页面\n" +
                "  - 指定页面：输入页码如 1,3,5-8\n" +
                "• 格式：PDF、JPG、PNG、TIF\n" +
                "• 分辨率：输出DPI，默认300\n\n" +
                "━━━━━━ 坐标表设置(⚙) ━━━━━━\n" +
                "• 启用坐标表：勾选后导出时自动生成坐标表\n" +
                "• 定位设置：\n" +
                "  - 地图框定位：相对于地图框边角定位\n" +
                "  - 锚点定位：相对于指定锚点元素定位\n" +
                "• 表格尺寸：点号宽、坐标宽、边长宽、行高\n" +
                "• 每列行数：超过此行数自动分列\n" +
                "• 压缩总行数：点数过多时自动省略中间行（如1-5...25-30）\n" +
                "• 面积显示：不显示/仅面积/面积+亩数\n" +
                "• 自定义文本：支持字段占位符如 [面积]\n" +
                "• 生成模板：创建XF_TX和XF_WB模板元素\n" +
                "  用于自定义表格样式（边框颜色、文字样式等）\n\n" +
                "━━━━━━ 界址点标注 ━━━━━━\n" +
                "• 生成界址点：在地图上创建点标注\n" +
                "• 点大小：默认8点，可生成XF_JZD模板设置样式\n" +
                "• 生成点号：在界址点外侧生成编号文本\n" +
                "  - 前缀、距离、大小可设置\n" +
                "  - 可生成XF_DH模板设置字体颜色\n" +
                "• 压盖处理：\n" +
                "  - 压盖隐藏：重叠时隐藏后面的\n" +
                "  - 压盖避让：自动调整位置避开重叠\n\n" +
                "━━━━━━ 交集表格 ━━━━━━\n" +
                "• 显示交集表格：按当前驱动红线与指定面图层相交计算\n" +
                "• 分类字段：作为交集结果的表格行类别\n" +
                "• 未覆盖部分自动归为“其他”，面积按当前红线总面积调平\n" +
                "• 交集表使用地图框/锚点定位方式，可设置角点、偏移、列宽和行高\n\n" +
                "━━━━━━ 注意事项 ━━━━━━\n" +
                "• 布局必须已启用空间地图系列\n" +
                "• 坐标表定位需要正确设置地图框或锚点\n" +
                "• 模板元素可自定义后重复使用：\n" +
                "  - XF_TX/XF_WB：表格样式\n" +
                "  - XF_JZD：界址点样式\n" +
                "  - XF_DH：点号文本样式\n" +
                "• 界址点标注创建在XF_界址点标注图形图层";
            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(helpContent, "地图系列批量导出工具 - 帮助");
        }

        private void Stop()
        {
            _cancellationTokenSource?.Cancel();
            IsRunning = false;
        }

        private bool CanExport()
        {
            return !IsRunning && SelectedLayout != null && MapSeriesPages.Count > 0 &&
                   !string.IsNullOrWhiteSpace(OutputFolder) && int.TryParse(Resolution, out int res) && res > 0;
        }

        private bool HasPageGeneratedContent()
        {
            var settings = _coordinateTableSettings;
            return settings?.EnableCoordinateTable == true ||
                   settings?.EnableBoundaryPoints == true ||
                   settings?.EnablePointLabels == true ||
                   settings?.EnableEdgeLabels == true ||
                   settings?.EnableIntersectTable == true;
        }

        /// <summary>
        /// 打开设置窗口
        /// </summary>
        private void OpenSettings()
        {
            var settingsWindow = new MapSeriesSettingsWindow(_coordinateTableSettings);
            settingsWindow.Owner = Application.Current.MainWindow;
            settingsWindow.SettingsSaved += settings =>
            {
                CoordinateTableSettings = settings;
            };
            settingsWindow.Show();
        }

        #endregion

        #region 导出方法

        private async Task Export()
        {
            try
            {
                IsRunning = true;
                _cancellationTokenSource = new CancellationTokenSource();

                if (!Directory.Exists(OutputFolder)) Directory.CreateDirectory(OutputFolder);

                var pagesToExport = GetPagesToExport();
                if (pagesToExport.Count == 0)
                {
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("没有要导出的页面", "提示");
                    return;
                }

                int successCount = 0;
                int totalCount = pagesToExport.Count;

                await QueuedTask.Run(async () =>
                {
                    var layout = SelectedLayout.GetLayout();
                    var mapSeries = layout?.MapSeries;
                    if (mapSeries == null || !mapSeries.Enabled)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("地图系列未启用", "错误"));
                        return;
                    }

                    if (!int.TryParse(Resolution, out int resolution)) resolution = 300;

                    foreach (var pageIndex in pagesToExport)
                    {
                        if (_cancellationTokenSource.Token.IsCancellationRequested) break;
                        try
                        {
                            if (pageIndex >= 1 && pageIndex <= mapSeries.PageCount)
                            {
                                // 切换页面前清除坐标表
                                if (HasPageGeneratedContent())
                                {
                                    ClearCoordinateTableElements(layout);
                                }
                                
                                // 切换地图系列页面
                                mapSeries.SetCurrentPageNumber(pageIndex.ToString());
                                
                                // 如果启用了坐标表生成，则生成坐标表
                                if (HasPageGeneratedContent())
                                {
                                    GenerateCoordinateTableForCurrentPage(layout, mapSeries);
                                }
                                
                                string pageName = CleanFileName(mapSeries.CurrentPageName ?? $"Page_{pageIndex}");
                                string filePath = Path.Combine(OutputFolder, $"{pageName}.{GetFileExtension()}");
                                ExportPage(layout, filePath, resolution);
                                successCount++;
                            }
                        }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"导出页面 {pageIndex} 失败: {ex.Message}"); }
                    }
                    
                    // 导出完成后清除最后一个坐标表
                    if (HasPageGeneratedContent())
                    {
                        ClearCoordinateTableElements(layout);
                    }
                });

                if (!_cancellationTokenSource.Token.IsCancellationRequested)
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"导出完成！成功: {successCount}/{totalCount} 页", "导出完成");
            }
            catch (Exception ex) { ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"导出错误：{ex.Message}", "错误"); }
            finally
            {
                IsRunning = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private List<int> GetPagesToExport()
        {
            return SelectedExportMode switch
            {
                "全部导出" => MapSeriesPages.Select(p => p.PageIndex).ToList(),
                "选中导出" => MapSeriesPages.Where(p => p.IsSelected).Select(p => p.PageIndex).ToList(),
                "指定页面" => ParsePageRange(PageRange),
                _ => new List<int>()
            };
        }

        private List<int> ParsePageRange(string range)
        {
            var pages = new HashSet<int>();
            if (string.IsNullOrWhiteSpace(range)) return pages.ToList();
            var maxPage = MapSeriesPages.Count;
            var parts = range.Split(new[] { ',', '，', ';', '；' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (trimmed.Contains("-") || trimmed.Contains("—"))
                {
                    var rangeParts = Regex.Split(trimmed, "[-—]");
                    if (rangeParts.Length == 2 && int.TryParse(rangeParts[0].Trim(), out int start) && int.TryParse(rangeParts[1].Trim(), out int end))
                        for (int i = Math.Max(1, start); i <= Math.Min(maxPage, end); i++) pages.Add(i);
                }
                else if (int.TryParse(trimmed, out int page) && page >= 1 && page <= maxPage) pages.Add(page);
            }
            return pages.OrderBy(p => p).ToList();
        }

        private string CleanFileName(string fileName)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) fileName = fileName.Replace(c, '_');
            return fileName;
        }

        private string GetFileExtension() => SelectedFormat.ToLower() switch { "pdf" => "pdf", "jpg" => "jpg", "png" => "png", "tif" => "tif", _ => "pdf" };

        private void ExportPage(Layout layout, string filePath, int resolution)
        {
            switch (SelectedFormat.ToUpper())
            {
                case "PDF": 
                    layout.Export(new PDFFormat 
                    { 
                        Resolution = resolution, 
                        OutputFileName = filePath,
                        ImageQuality = ImageQuality.Best  // 最佳图像质量
                    }); 
                    break;
                case "JPG": 
                    layout.Export(new JPEGFormat { Resolution = resolution, OutputFileName = filePath }); 
                    break;
                case "PNG": 
                    layout.Export(new PNGFormat { Resolution = resolution, OutputFileName = filePath }); 
                    break;
                case "TIF": 
                    layout.Export(new TIFFFormat { Resolution = resolution, OutputFileName = filePath }); 
                    break;
            }
        }

        #endregion

        #region 坐标表生成方法

        /// <summary>
        /// 清除布局上的坐标表元素
        /// </summary>
        private void ClearCoordinateTableElements(Layout layout)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[驱动制图] 开始清除坐标表元素...");
                var sw = System.Diagnostics.Stopwatch.StartNew();
                
                // 只清除一轮，使用更精确的名称匹配
                var allElements = layout.GetElements().ToList();
                System.Diagnostics.Debug.WriteLine($"[驱动制图] 布局共有 {allElements.Count} 个元素");
                
                var elementsToDelete = allElements
                    .Where(e => e.Name != null && (
                        e.Name.StartsWith("MapSeries_CoordTable_") ||
                        e.Name.StartsWith("Group_") ||
                        e.Name.StartsWith("Title_") ||
                        e.Name.StartsWith("Header_") ||
                        e.Name.StartsWith("Data_") ||
                        e.Name.StartsWith("Edge_") ||
                        e.Name.StartsWith("Area_") ||
                        e.Name.StartsWith("EdgePad") ||
                        e.Name.StartsWith("MapSeries_IntersectTable_") ||
                        e.Name.StartsWith("IntersectTitle_") ||
                        e.Name.StartsWith("IntersectHeader_") ||
                        e.Name.StartsWith("IntersectData_") ||
                        e.Name.StartsWith("IntersectTotal_")))
                    .ToList();
                
                System.Diagnostics.Debug.WriteLine($"[驱动制图] 需要删除 {elementsToDelete.Count} 个元素");
                
                if (elementsToDelete.Count > 0)
                {
                    layout.DeleteElements(elementsToDelete);
                }
                
                _currentCoordinateTableGroupName = null;
                
                // 清除界址点图形图层元素（包括点号和边长）
                if (_coordinateTableSettings?.EnableBoundaryPoints == true ||
                    _coordinateTableSettings?.EnablePointLabels == true ||
                    _coordinateTableSettings?.EnableEdgeLabels == true)
                {
                    ClearBoundaryPointsFromMap(layout);
                }
                
                sw.Stop();
                System.Diagnostics.Debug.WriteLine($"[驱动制图] 清除完成，耗时 {sw.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清除坐标表失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 清除地图上的界址点标注
        /// </summary>
        private void ClearBoundaryPointsFromMap(Layout layout)
        {
            try
            {
                // 获取地图框
                MapFrame mapFrame = null;
                if (!string.IsNullOrEmpty(_coordinateTableSettings?.MapFrameName))
                {
                    mapFrame = layout.FindElement(_coordinateTableSettings.MapFrameName) as MapFrame;
                }
                if (mapFrame == null)
                {
                    mapFrame = layout.Elements.OfType<MapFrame>().FirstOrDefault();
                }
                
                if (mapFrame?.Map == null) return;
                
                var map = mapFrame.Map;
                
                // 查找界址点图形图层
                var graphicsLayer = map.GetLayersAsFlattenedList()
                    .OfType<GraphicsLayer>()
                    .FirstOrDefault(l => l.Name == "XF_界址点标注");
                
                if (graphicsLayer != null)
                {
                    ClearGraphicsLayerElements(graphicsLayer);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清除界址点标注失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 为当前页面生成坐标表（同步版本，用于导出）
        /// </summary>
        private void GenerateCoordinateTableForCurrentPage(Layout layout, MapSeries mapSeries)
        {
            try
            {
                var settings = _coordinateTableSettings;
                if (settings == null || !HasPageGeneratedContent()) return;

                // 从地图系列获取索引图层
                var spatialMapSeries = mapSeries as SpatialMapSeries;
                if (spatialMapSeries == null) return;

                var indexLayer = spatialMapSeries.IndexLayer as FeatureLayer;
                if (indexLayer == null)
                {
                    System.Diagnostics.Debug.WriteLine("地图系列索引图层不是要素图层");
                    return;
                }

                // 获取索引字段名称
                var smsDefinition = mapSeries.GetDefinition() as CIMSpatialMapSeries;
                string indexField = smsDefinition?.NameField;
                if (string.IsNullOrEmpty(indexField))
                {
                    System.Diagnostics.Debug.WriteLine("地图系列索引字段为空");
                    return;
                }

                // 获取当前地图系列页面对应的要素
                var currentPageName = mapSeries.CurrentPageName;
                if (string.IsNullOrEmpty(currentPageName)) return;

                // 查询匹配当前页面的要素
                using (var table = indexLayer.GetTable())
                {
                    var queryFilter = new QueryFilter
                    {
                        WhereClause = $"{indexField} = '{currentPageName}'"
                    };

                    using (var cursor = table.Search(queryFilter))
                    {
                        if (cursor.MoveNext())
                        {
                            using (var row = cursor.Current)
                            {
                                var geometry = row["SHAPE"] as Polygon;
                                var uniqueValue = row[indexField]?.ToString();

                                // 提取所有字段值用于自定义文本替换
                                var fieldValues = new Dictionary<string, string>();
                                var fields = row.GetFields();
                                foreach (var field in fields)
                                {
                                    try
                                    {
                                        var value = row[field.Name];
                                        fieldValues[field.Name] = value?.ToString() ?? "";
                                    }
                                    catch { }
                                }

                                if (geometry != null && !string.IsNullOrEmpty(uniqueValue))
                                {
                                    if (settings.EnableCoordinateTable)
                                    {
                                        ProcessPolygonAndCreateTable(layout, geometry, uniqueValue, settings, fieldValues);
                                    }
                                    
                                    // 如果启用了界址点、点号或边长生成，则创建相应标注
                                    if (settings.EnableBoundaryPoints || settings.EnablePointLabels || settings.EnableEdgeLabels)
                                    {
                                        CreateBoundaryPointsOnMap(layout, geometry, settings);
                                    }

                                    if (settings.EnableIntersectTable)
                                    {
                                        CreateIntersectTableForCurrentPage(layout, geometry, settings);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"生成坐标表失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理面要素并创建坐标表（同步版本）
        /// </summary>
        private void ProcessPolygonAndCreateTable(Layout layout, Polygon geometry, string uniqueValue, 
            CoordinateTableSettings settings, Dictionary<string, string> fieldValues = null)
        {
            try
            {
                // 提取坐标点
                var coordinates = ExtractCoordinates(geometry, settings.XYDecimal);
                var edgeLengths = CalculateEdgeLengths(coordinates);
                
                // 计算面积
                var (areaFormatted, muAreaFormatted) = FormatArea(geometry.Area, settings.AreaUnit, settings.AreaDecimal, settings.MuDecimal);
                
                // 替换标题中的字段占位符
                string titleText = settings.TitleText ?? "";
                if (fieldValues != null && titleText.Contains("["))
                {
                    var regex = new Regex(@"\[([^\]]+)\]");
                    titleText = regex.Replace(titleText, match =>
                    {
                        var fieldName = match.Groups[1].Value;
                        if (fieldValues.TryGetValue(fieldName, out var value))
                        {
                            return value;
                        }
                        return match.Value;
                    });
                }
                
                // 生成表格文本
                var tableData = GenerateTableData(coordinates, titleText, settings.PointPrefix, settings.GenerateEdge,
                    edgeLengths, settings.EdgeDecimal, settings.RowsPerColumn, settings.SwapXY, settings.CompressTotalRows);
                
                // 在布局上创建表格元素（同步）
                CreateTableElements(layout, tableData, uniqueValue, areaFormatted, muAreaFormatted, settings, fieldValues);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"处理面要素 {uniqueValue} 时发生错误: {ex.Message}");
            }
        }

        private List<(double X, double Y, string XStr, string YStr)> ExtractCoordinates(Polygon geometry, int decimalPlaces)
        {
            var coordinates = new List<(double X, double Y, string XStr, string YStr)>();
            
            var points = geometry.Points;
            foreach (var point in points)
            {
                coordinates.Add((
                    point.X, 
                    point.Y,
                    point.X.ToString($"F{decimalPlaces}"),
                    point.Y.ToString($"F{decimalPlaces}")
                ));
            }
            
            // 确保首尾闭合
            if (coordinates.Count > 0 && 
                (coordinates[0].X != coordinates[coordinates.Count - 1].X || 
                 coordinates[0].Y != coordinates[coordinates.Count - 1].Y))
            {
                var first = coordinates[0];
                coordinates.Add(first);
            }
            
            return coordinates;
        }

        private List<double> CalculateEdgeLengths(List<(double X, double Y, string XStr, string YStr)> coordinates)
        {
            var edgeLengths = new List<double>();
            
            for (int i = 0; i < coordinates.Count - 1; i++)
            {
                double dx = coordinates[i + 1].X - coordinates[i].X;
                double dy = coordinates[i + 1].Y - coordinates[i].Y;
                double length = Math.Sqrt(dx * dx + dy * dy);
                edgeLengths.Add(length);
            }
            
            return edgeLengths;
        }

        private (string areaFormatted, string muAreaFormatted) FormatArea(double area, string unitChoice, 
            int decimalPlaces, int muDecimalPlaces)
        {
            if (unitChoice == "平方米")
            {
                double main = Math.Round(area, decimalPlaces, MidpointRounding.AwayFromZero);
                string areaFormatted = main.ToString($"F{decimalPlaces}");
                double muArea = main * 0.0015;
                string muAreaFormatted = muArea.ToString($"F{muDecimalPlaces}");
                return (areaFormatted, muAreaFormatted);
            }
            else if (unitChoice == "公顷")
            {
                double hectares = area / 10000.0;
                double main = Math.Round(hectares, decimalPlaces, MidpointRounding.AwayFromZero);
                string areaFormatted = main.ToString($"F{decimalPlaces}");
                double muArea = main * 15.0;
                string muAreaFormatted = muArea.ToString($"F{muDecimalPlaces}");
                return (areaFormatted, muAreaFormatted);
            }

            double fallbackMain = Math.Round(area, decimalPlaces, MidpointRounding.AwayFromZero);
            return (fallbackMain.ToString($"F{decimalPlaces}"), (fallbackMain * 0.0015).ToString($"F{muDecimalPlaces}"));
        }

        private List<string> GenerateTableData(List<(double X, double Y, string XStr, string YStr)> coordinates,
            string titleText, string prefixStr, bool generateEdge, List<double> edgeLengths,
            int edgeDecimal, int rowsPerColumn, bool surveyStyle, int compressTotalRows)
        {
            var tableData = new List<string>();
            int splitRowsPerColumn = Math.Max(2, rowsPerColumn);
            
            string headerInfo = generateEdge ? 
                $"{titleText}\n点号    X坐标    Y坐标    边长" : 
                $"{titleText}\n点号    X坐标    Y坐标";

            var dataRows = new List<string[]>();
            for (int i = 0; i < coordinates.Count; i++)
            {
                int displayNo = (i == coordinates.Count - 1) ? 1 : i + 1;
                string pointName = $"{prefixStr}{displayNo}";
                string xOut = surveyStyle ? coordinates[i].YStr : coordinates[i].XStr;
                string yOut = surveyStyle ? coordinates[i].XStr : coordinates[i].YStr;

                if (generateEdge && i < edgeLengths.Count)
                {
                    string edgeStr = edgeLengths[i].ToString($"F{edgeDecimal}");
                    dataRows.Add(new[] { pointName, xOut, yOut, edgeStr });
                }
                else
                {
                    if (generateEdge)
                    {
                        dataRows.Add(new[] { pointName, xOut, yOut, string.Empty });
                    }
                    else
                    {
                        dataRows.Add(new[] { pointName, xOut, yOut });
                    }
                }
            }

            if (compressTotalRows > 0 && dataRows.Count > compressTotalRows)
            {
                int compressedDataRows = ResolveCompressedDataRowCount(dataRows.Count, splitRowsPerColumn, compressTotalRows);
                if (compressedDataRows < dataRows.Count)
                {
                    dataRows = CompressRowsToTargetCount(dataRows, compressedDataRows, generateEdge, splitRowsPerColumn);
                }
            }

            var dataLines = dataRows.Select(row => FormatTableDataLine(row, generateEdge)).ToList();
            
            if (dataLines.Count <= splitRowsPerColumn)
            {
                string subtable = headerInfo + "\n" + string.Join("\n", dataLines);
                tableData.Add(subtable);
            }
            else
            {
                var firstChunk = dataLines.Take(splitRowsPerColumn);
                string firstSubtable = headerInfo + "\n" + string.Join("\n", firstChunk);
                tableData.Add(firstSubtable);
                
                int index = splitRowsPerColumn;
                while (index < dataLines.Count)
                {
                    var lines = new List<string> { headerInfo.Split('\n')[0], headerInfo.Split('\n')[1] };
                    lines.Add(dataLines[index - 1]);
                    
                    var nextChunk = dataLines.Skip(index).Take(splitRowsPerColumn - 1);
                    lines.AddRange(nextChunk);
                    
                    string subtable = string.Join("\n", lines);
                    tableData.Add(subtable);
                    
                    index += splitRowsPerColumn - 1;
                }
            }
            
            return tableData;
        }

        private int ResolveCompressedDataRowCount(int sourceRowCount, int rowsPerColumn, int maxDisplayRows)
        {
            if (sourceRowCount <= 0 || maxDisplayRows <= 0)
            {
                return sourceRowCount;
            }

            if (rowsPerColumn <= 1)
            {
                return Math.Min(sourceRowCount, maxDisplayRows);
            }

            int upper = Math.Min(sourceRowCount, maxDisplayRows);
            int best = 1;

            for (int candidate = 1; candidate <= upper; candidate++)
            {
                int displayed = CalculateDisplayedRowCount(candidate, rowsPerColumn);
                if (displayed <= maxDisplayRows)
                {
                    best = candidate;
                }
                else
                {
                    break;
                }
            }

            return best;
        }

        private int CalculateDisplayedRowCount(int dataRowCount, int rowsPerColumn)
        {
            if (dataRowCount <= 0)
            {
                return 0;
            }

            if (dataRowCount <= rowsPerColumn)
            {
                return dataRowCount;
            }

            int displayed = rowsPerColumn;
            int index = rowsPerColumn;
            while (index < dataRowCount)
            {
                int take = Math.Min(rowsPerColumn - 1, dataRowCount - index);
                displayed += 1 + take;
                index += rowsPerColumn - 1;
            }

            return displayed;
        }

        private List<string[]> CompressRowsToTargetCount(List<string[]> sourceRows, int targetCount,
            bool generateEdge, int rowsPerColumn)
        {
            if (sourceRows == null || sourceRows.Count == 0 || targetCount >= sourceRows.Count)
            {
                return sourceRows;
            }

            if (targetCount <= 1)
            {
                return new List<string[]> { sourceRows[0] };
            }

            if (targetCount == 2)
            {
                return new List<string[]> { sourceRows[0], sourceRows[^1] };
            }

            int bestHeadCount = 1;
            int bestTailCount = targetCount - bestHeadCount - 1;
            double totalDisplayedCenter = (CalculateDisplayedRowCount(targetCount, rowsPerColumn) + 1) / 2.0;
            double bestScore = double.MaxValue;

            for (int headCount = 1; headCount <= targetCount - 2; headCount++)
            {
                int tailCount = targetCount - headCount - 1;
                if (headCount + tailCount >= sourceRows.Count)
                {
                    continue;
                }

                int ellipsisDataIndex = headCount + 1;
                int ellipsisDisplayIndex = CalculateDisplayedIndexForDataRow(ellipsisDataIndex, rowsPerColumn);
                double centerDistance = Math.Abs(ellipsisDisplayIndex - totalDisplayedCenter);
                double balancePenalty = Math.Abs(headCount - tailCount) * 0.01;
                double score = centerDistance + balancePenalty;

                if (score < bestScore)
                {
                    bestScore = score;
                    bestHeadCount = headCount;
                    bestTailCount = tailCount;
                }
            }

            var compressed = new List<string[]>();
            compressed.AddRange(sourceRows.Take(bestHeadCount));
            compressed.Add(CreateEllipsisRow(generateEdge));
            compressed.AddRange(sourceRows.Skip(sourceRows.Count - bestTailCount));
            return compressed;
        }

        private int CalculateDisplayedIndexForDataRow(int dataRowIndex, int rowsPerColumn)
        {
            if (dataRowIndex <= 0)
            {
                return 0;
            }

            if (dataRowIndex <= rowsPerColumn)
            {
                return dataRowIndex;
            }

            int displayed = rowsPerColumn;
            int index = rowsPerColumn;
            while (index < dataRowIndex)
            {
                displayed += 1;
                int take = Math.Min(rowsPerColumn - 1, dataRowIndex - index);
                displayed += take;
                index += rowsPerColumn - 1;
            }

            return displayed;
        }

        private string[] CreateEllipsisRow(bool generateEdge)
        {
            return generateEdge
                ? new[] { EllipsisMarker, EllipsisMarker, EllipsisMarker, EllipsisMarker }
                : new[] { EllipsisMarker, EllipsisMarker, EllipsisMarker };
        }

        private bool IsEllipsisRow(string[] tokens)
        {
            return tokens != null && tokens.Length > 0 && tokens[0] == EllipsisMarker;
        }

        private string FormatTableDataLine(string[] tokens, bool generateEdge)
        {
            if (generateEdge)
            {
                string edgeText = tokens.Length > 3 ? tokens[3] : string.Empty;
                return $"{tokens[0]}    {tokens[1]}    {tokens[2]}    {edgeText}";
            }

            return $"{tokens[0]}    {tokens[1]}    {tokens[2]}";
        }

        private void CreateTableElements(Layout layout, List<string> tableData, string uniqueValue,
            string areaFormatted, string muAreaFormatted, CoordinateTableSettings settings, 
            Dictionary<string, string> fieldValues = null)
        {
            // 为元素名称添加GUID确保唯一性，避免多次生成时的命名冲突
            string timestamp = Guid.NewGuid().ToString("N");
            
            try
            {
                
                double pointColWidth = settings.PointColWidth;
                double xyColWidth = settings.XYColWidth;
                double edgeColWidth = settings.EdgeColWidth;
                double rowHeight = settings.RowHeight;
                bool generateEdge = settings.GenerateEdge;
                string placementCorner = settings.PlacementCorner;
                double cornerOffset = settings.CornerOffset;
                string areaUnit = settings.AreaUnit;
                
                // 查找模板并获取符号
                CIMPolygonSymbol rectSymbol = null;
                CIMTextSymbol textSymbol = null;
                
                var txTemplate = layout.FindElement("XF_TX") as GraphicElement;
                if (txTemplate != null)
                {
                    var graphic = txTemplate.GetGraphic();
                    if (graphic is CIMPolygonGraphic polyGraphic)
                    {
                        rectSymbol = polyGraphic.Symbol?.Symbol as CIMPolygonSymbol;
                    }
                }
                
                var wbTemplate = layout.FindElement("XF_WB") as GraphicElement;
                if (wbTemplate != null)
                {
                    var graphic = wbTemplate.GetGraphic();
                    if (graphic is CIMTextGraphic textGraphic)
                    {
                        textSymbol = textGraphic.Symbol?.Symbol as CIMTextSymbol;
                    }
                }
                
                double tableWidth = generateEdge ? 
                    (pointColWidth + 2 * xyColWidth + edgeColWidth) :
                    (pointColWidth + 2 * xyColWidth);
                
                double tableRowHeight = rowHeight;

                var subtableLineCounts = new List<int>();
                for (int idx = 0; idx < tableData.Count; idx++)
                {
                    var cnt = tableData[idx]
                        .Split('\n')
                        .Select(s => s.Trim())
                        .Count(s => !string.IsNullOrEmpty(s));
                    if (idx == tableData.Count - 1)
                        cnt += 1;
                    subtableLineCounts.Add(cnt);
                }
                int maxLineCount = subtableLineCounts.Count > 0 ? subtableLineCounts.Max() : 0;

                double startX;
                double startY;
                
                // 根据定位方式确定起始位置
                if (settings.UseAnchorPosition)
                {
                    // 使用锚点元素定位
                    var anchorElement = layout.FindElement(settings.AnchorElementName) as GraphicElement;
                    if (anchorElement != null)
                    {
                        var anchorBounds = anchorElement.GetBounds();
                        double anchorX = (anchorBounds.XMin + anchorBounds.XMax) / 2;
                        double anchorY = (anchorBounds.YMin + anchorBounds.YMax) / 2;
                        
                        switch (placementCorner)
                        {
                            case "左下角":
                                startX = anchorX + cornerOffset;
                                startY = anchorY + cornerOffset + (maxLineCount * tableRowHeight);
                                break;
                            case "右下角":
                                startX = anchorX - tableWidth - cornerOffset;
                                startY = anchorY + cornerOffset + (maxLineCount * tableRowHeight);
                                break;
                            case "左上角":
                                startX = anchorX + cornerOffset;
                                startY = anchorY - cornerOffset;
                                break;
                            case "右上角":
                                startX = anchorX - tableWidth - cornerOffset;
                                startY = anchorY - cornerOffset;
                                break;
                            default:
                                startX = anchorX + cornerOffset;
                                startY = anchorY + cornerOffset + (maxLineCount * tableRowHeight);
                                break;
                        }
                    }
                    else
                    {
                        // 锚点不存在，使用默认位置
                        startX = 10;
                        startY = 300;
                    }
                }
                else
                {
                    // 使用地图框定位
                    MapFrame mapFrame = null;
                    if (!string.IsNullOrEmpty(settings.MapFrameName))
                    {
                        mapFrame = layout.FindElement(settings.MapFrameName) as MapFrame;
                    }
                    if (mapFrame == null)
                    {
                        mapFrame = layout.Elements.OfType<MapFrame>().FirstOrDefault();
                    }
                    
                    if (mapFrame != null)
                    {
                        var mapBounds = mapFrame.GetBounds();
                        switch (placementCorner)
                        {
                            case "左下角":
                                startX = mapBounds.XMin + cornerOffset;
                                startY = mapBounds.YMin + cornerOffset + (maxLineCount * tableRowHeight);
                                break;
                            case "右下角":
                                startX = mapBounds.XMax - tableWidth - cornerOffset;
                                startY = mapBounds.YMin + cornerOffset + (maxLineCount * tableRowHeight);
                                break;
                            case "左上角":
                                startX = mapBounds.XMin + cornerOffset;
                                startY = mapBounds.YMax - cornerOffset;
                                break;
                            case "右上角":
                                startX = mapBounds.XMax - tableWidth - cornerOffset;
                                startY = mapBounds.YMax - cornerOffset;
                                break;
                            default:
                                startX = mapBounds.XMin + cornerOffset;
                                startY = mapBounds.YMin + cornerOffset + (maxLineCount * tableRowHeight);
                                break;
                        }
                    }
                    else
                    {
                        startX = 10;
                        startY = 300;
                    }
                }
                
                var mainGroupElements = new List<Element>();
                double currentStartX = startX;

                for (int tableIndex = 0; tableIndex < tableData.Count; tableIndex++)
                {
                    var tableText = tableData[tableIndex];
                    var lines = tableText.Split('\n');
                    var subGroupElements = new List<Element>();
                    double edgeColumnXStart = 0;
                    double firstDataRowTopY = 0;
                    bool dataRowTopYCaptured = false;
                    var dataRowTokens = new List<string[]>();
                    int thisLineCount = lines.Select(s => s.Trim()).Count(s => !string.IsNullOrEmpty(s));
                    if (tableIndex == tableData.Count - 1)
                        thisLineCount += 1;
                    double currentY = startY - (maxLineCount - thisLineCount) * tableRowHeight;

                    for (int i = 0; i < lines.Length; i++)
                    {
                        var line = lines[i].Trim();
                        if (string.IsNullOrEmpty(line)) continue;

                        if (i == 0)
                        {
                            var titleElements = LayoutElementHelper.CreateTableCellSync(
                                layout, $"Title_{tableIndex}_{i}_{timestamp}",
                                (currentStartX, currentY), (tableWidth, tableRowHeight), line, true,
                                rectSymbol, textSymbol);
                            subGroupElements.AddRange(titleElements);
                            currentY -= tableRowHeight;
                        }
                        else if (i == 1)
                        {
                            var tokens = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            var colWidths = generateEdge ? 
                                new[] { pointColWidth, xyColWidth, xyColWidth, edgeColWidth } :
                                new[] { pointColWidth, xyColWidth, xyColWidth };

                            double xOffset = currentStartX;
                            for (int j = 0; j < Math.Min(tokens.Length, colWidths.Length); j++)
                            {
                                double cellWidth = colWidths[j];
                                
                                var headerElements = LayoutElementHelper.CreateTableCellSync(
                                    layout, $"Header_{tableIndex}_{i}_{j}_{timestamp}",
                                    (xOffset, currentY), (cellWidth, tableRowHeight), tokens[j], true,
                                    rectSymbol, textSymbol);
                                subGroupElements.AddRange(headerElements);
                                xOffset += cellWidth;
                            }
                            if (generateEdge)
                            {
                                edgeColumnXStart = currentStartX + pointColWidth + xyColWidth + xyColWidth;
                            }
                            firstDataRowTopY = currentY - tableRowHeight;
                            dataRowTopYCaptured = true;
                            currentY -= tableRowHeight;
                        }
                        else
                        {
                            var tokens = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            if (tokens.Length < 3) continue;

                            var colWidths = generateEdge ?
                                new[] { pointColWidth, xyColWidth, xyColWidth, edgeColWidth } :
                                new[] { pointColWidth, xyColWidth, xyColWidth };
                            double xOffset = currentStartX;

                            for (int j = 0; j < Math.Min(tokens.Length, colWidths.Length); j++)
                            {
                                double cellWidth = colWidths[j];
                                if (generateEdge && j == colWidths.Length - 1)
                                {
                                    xOffset += cellWidth;
                                    continue;
                                }

                                var dataElements = LayoutElementHelper.CreateTableCellSync(
                                    layout, $"Data_{tableIndex}_{i}_{j}_{timestamp}",
                                    (xOffset, currentY), (cellWidth, tableRowHeight), tokens[j], false,
                                    rectSymbol, textSymbol);
                                subGroupElements.AddRange(dataElements);
                                xOffset += cellWidth;
                            }
                            dataRowTokens.Add(tokens);
                            currentY -= tableRowHeight;
                        }
                    }

                    if (generateEdge && dataRowTopYCaptured && dataRowTokens.Count > 0)
                    {
                        double xEdge = edgeColumnXStart;

                        var padTop = LayoutElementHelper.CreateTableCellSync(
                            layout, $"EdgePadTop_{tableIndex}_{timestamp}",
                            (xEdge, firstDataRowTopY), (edgeColWidth, tableRowHeight / 2.0), string.Empty, false,
                            rectSymbol, textSymbol);
                        subGroupElements.AddRange(padTop);

                        for (int e = 0; e < dataRowTokens.Count - 1; e++)
                        {
                            bool currentIsEllipsis = IsEllipsisRow(dataRowTokens[e]);
                            bool nextIsEllipsis = IsEllipsisRow(dataRowTokens[e + 1]);
                            string edgeText = (currentIsEllipsis || nextIsEllipsis)
                                ? "•••"
                                : (dataRowTokens[e].Length > 3 ? dataRowTokens[e][3] : string.Empty);
                            double edgeTopY = firstDataRowTopY - (e + 0.5) * tableRowHeight;

                            var edgeElems = LayoutElementHelper.CreateTableCellSync(
                                layout, $"Edge_{tableIndex}_{e}_{timestamp}",
                                (xEdge, edgeTopY), (edgeColWidth, tableRowHeight), edgeText, false,
                                rectSymbol, textSymbol);
                            subGroupElements.AddRange(edgeElems);
                        }

                        double bottomPadTop = firstDataRowTopY - (dataRowTokens.Count - 0.5) * tableRowHeight;
                        var padBottom = LayoutElementHelper.CreateTableCellSync(
                            layout, $"EdgePadBottom_{tableIndex}_{timestamp}",
                            (xEdge, bottomPadTop), (edgeColWidth, tableRowHeight / 2.0), string.Empty, false,
                            rectSymbol, textSymbol);
                        subGroupElements.AddRange(padBottom);
                    }

                    // 根据面积模式生成面积行
                    if (tableIndex == tableData.Count - 1 && settings.AreaMode != "不生成")
                    {
                        string areaText;
                        if (settings.AreaMode == "自定义")
                        {
                            // 自定义模式，替换所有[字段名]占位符
                            areaText = settings.CustomAreaText ?? "";
                            
                            // 使用正则表达式查找所有 [xxx] 格式的占位符
                            var regex = new Regex(@"\[([^\]]+)\]");
                            areaText = regex.Replace(areaText, match =>
                            {
                                var fieldName = match.Groups[1].Value;
                                if (fieldValues != null && fieldValues.TryGetValue(fieldName, out var value))
                                {
                                    return value;
                                }
                                return match.Value; // 未找到字段，保持原样
                            });
                        }
                        else
                        {
                            // 自动生成模式
                            areaText = $"S={areaFormatted} {areaUnit} 合 {muAreaFormatted} 亩";
                        }
                        
                        var areaElements = LayoutElementHelper.CreateTableCellSync(
                            layout, $"Area_{tableIndex}_{timestamp}",
                            (currentStartX, currentY), (tableWidth, tableRowHeight), areaText, false,
                            rectSymbol, textSymbol);
                        subGroupElements.AddRange(areaElements);
                    }

                    // 不使用 GroupElement，直接保留元素引用用于后续清理
                    // 清空子元素列表释放引用
                    subGroupElements.Clear();
                    
                    // 每批元素创建后强制同步布局状态
                    var _ = layout.GetElements().ToList();

                    if (tableIndex < tableData.Count - 1)
                    {
                        if (placementCorner == "右下角" || placementCorner == "右上角")
                        {
                            currentStartX -= tableWidth;
                        }
                        else
                        {
                            currentStartX += tableWidth;
                        }
                    }
                }

                // 不创建主组，记录当前时间戳用于清理
                _currentCoordinateTableGroupName = $"MapSeries_CoordTable_{timestamp}";
                
                // 清空主元素列表释放引用
                mainGroupElements.Clear();
                
                // 强制同步布局状态，确保元素完全创建
                {
                    var _ = layout.GetElements().ToList();
                    
                    // 刷新布局视图确保元素正确渲染
                    var layoutView = LayoutView.Active;
                    if (layoutView != null && layoutView.Layout == layout)
                    {
                        layoutView.Refresh();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"创建表格元素时发生错误: {ex.Message}");
            }
        }

        private void CreateIntersectTableForCurrentPage(Layout layout, Polygon redlineGeometry, CoordinateTableSettings settings)
        {
            try
            {
                if (redlineGeometry == null ||
                    string.IsNullOrWhiteSpace(settings.IntersectLayerName) ||
                    string.IsNullOrWhiteSpace(settings.IntersectClassField))
                {
                    return;
                }

                var classLayer = FindIntersectFeatureLayer(layout, settings.IntersectLayerName);
                if (classLayer == null)
                {
                    System.Diagnostics.Debug.WriteLine($"未找到交集图层: {settings.IntersectLayerName}");
                    return;
                }

                var areas = CalculateCurrentRedlineIntersectAreas(redlineGeometry, classLayer, settings.IntersectClassField);
                var rows = MapSeriesIntersectTableBuilder.BuildRows(
                    areas,
                    redlineGeometry.Area,
                    settings.IntersectAreaUnit,
                    settings.IntersectDecimalPlaces);

                if (rows.Count == 0)
                {
                    return;
                }

                CreateIntersectTableElements(layout, rows, settings);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"生成交集表格失败: {ex.Message}");
            }
        }

        private FeatureLayer FindIntersectFeatureLayer(Layout layout, string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName))
            {
                return null;
            }

            var maps = new List<Map>();
            var mapFrame = ResolveMapFrame(layout, _coordinateTableSettings);
            if (mapFrame?.Map != null)
            {
                maps.Add(mapFrame.Map);
            }

            var activeMap = MapView.Active?.Map;
            if (activeMap != null && !maps.Contains(activeMap))
            {
                maps.Add(activeMap);
            }

            foreach (var map in maps)
            {
                var layer = map.GetLayersAsFlattenedList()
                    .OfType<FeatureLayer>()
                    .FirstOrDefault(item => string.Equals(item.Name, layerName, StringComparison.OrdinalIgnoreCase));
                if (layer != null)
                {
                    return layer;
                }
            }

            return null;
        }

        private List<MapSeriesIntersectArea> CalculateCurrentRedlineIntersectAreas(
            Polygon redlineGeometry,
            FeatureLayer classLayer,
            string classField)
        {
            var areas = new List<MapSeriesIntersectArea>();
            var classFeatureClass = classLayer.GetFeatureClass();
            if (classFeatureClass == null)
            {
                return areas;
            }

            var classDefinition = classFeatureClass.GetDefinition();
            var classSpatialReference = classDefinition.GetSpatialReference();
            var redlineSpatialReference = redlineGeometry.SpatialReference;

            Polygon queryGeometry = redlineGeometry;
            if (redlineSpatialReference != null &&
                classSpatialReference != null &&
                !SpatialReference.AreEqual(redlineSpatialReference, classSpatialReference, false))
            {
                queryGeometry = GeometryEngine.Instance.Project(redlineGeometry, classSpatialReference) as Polygon;
            }

            if (queryGeometry == null)
            {
                return areas;
            }

            var spatialFilter = new SpatialQueryFilter
            {
                FilterGeometry = queryGeometry,
                SpatialRelationship = SpatialRelationship.Intersects
            };

            using (var cursor = classFeatureClass.Search(spatialFilter))
            {
                while (cursor.MoveNext())
                {
                    using (var classFeature = cursor.Current as Feature)
                    {
                        if (classFeature?.GetShape() is not Polygon classPolygon)
                        {
                            continue;
                        }

                        Polygon projectedClassPolygon = classPolygon;
                        if (redlineSpatialReference != null &&
                            classSpatialReference != null &&
                            !SpatialReference.AreEqual(redlineSpatialReference, classSpatialReference, false))
                        {
                            projectedClassPolygon = GeometryEngine.Instance.Project(classPolygon, redlineSpatialReference) as Polygon;
                        }

                        if (projectedClassPolygon == null)
                        {
                            continue;
                        }

                        var intersection = GeometryEngine.Instance.Intersection(redlineGeometry, projectedClassPolygon);
                        if (intersection is not Polygon intersectPolygon || intersectPolygon.IsEmpty)
                        {
                            continue;
                        }

                        double area = intersectPolygon.Area;
                        if (area <= 0.0001)
                        {
                            continue;
                        }

                        string category = "未分类";
                        try
                        {
                            category = classFeature[classField]?.ToString();
                        }
                        catch
                        {
                            category = "未分类";
                        }

                        areas.Add(new MapSeriesIntersectArea(category, area));
                    }
                }
            }

            return areas;
        }

        private void CreateIntersectTableElements(
            Layout layout,
            List<MapSeriesIntersectTableRow> rows,
            CoordinateTableSettings settings)
        {
            string timestamp = Guid.NewGuid().ToString("N");

            try
            {
                CIMPolygonSymbol rectSymbol = null;
                CIMTextSymbol textSymbol = null;

                var txTemplate = layout.FindElement("XF_TX") as GraphicElement;
                if (txTemplate?.GetGraphic() is CIMPolygonGraphic polyGraphic)
                {
                    rectSymbol = polyGraphic.Symbol?.Symbol as CIMPolygonSymbol;
                }

                var wbTemplate = layout.FindElement("XF_WB") as GraphicElement;
                if (wbTemplate?.GetGraphic() is CIMTextGraphic textGraphic)
                {
                    textSymbol = textGraphic.Symbol?.Symbol as CIMTextSymbol;
                }

                double categoryColWidth = settings.IntersectCategoryColWidth;
                double areaColWidth = settings.IntersectAreaColWidth;
                double tableWidth = categoryColWidth + areaColWidth;
                double rowHeight = settings.IntersectRowHeight;
                int lineCount = rows.Count + 2;
                double tableHeight = lineCount * rowHeight;
                var (startX, startY) = ResolveTableStartPosition(
                    layout,
                    settings,
                    settings.IntersectPlacementCorner,
                    settings.IntersectCornerOffset,
                    tableWidth,
                    tableHeight);

                var createdElements = new List<Element>();
                double currentY = startY;

                createdElements.AddRange(LayoutElementHelper.CreateTableCellSync(
                    layout,
                    $"IntersectTitle_{timestamp}",
                    (startX, currentY),
                    (tableWidth, rowHeight),
                    settings.IntersectTableTitle,
                    true,
                    rectSymbol,
                    textSymbol));
                currentY -= rowHeight;

                createdElements.AddRange(LayoutElementHelper.CreateTableCellSync(
                    layout,
                    $"IntersectHeader_Category_{timestamp}",
                    (startX, currentY),
                    (categoryColWidth, rowHeight),
                    "类别",
                    true,
                    rectSymbol,
                    textSymbol));
                createdElements.AddRange(LayoutElementHelper.CreateTableCellSync(
                    layout,
                    $"IntersectHeader_Area_{timestamp}",
                    (startX + categoryColWidth, currentY),
                    (areaColWidth, rowHeight),
                    $"面积({settings.IntersectAreaUnit})",
                    true,
                    rectSymbol,
                    textSymbol));
                currentY -= rowHeight;

                for (int i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    string prefix = row.IsTotal ? "IntersectTotal" : "IntersectData";
                    bool isHeader = row.IsTotal;
                    string areaText = row.Area.ToString($"F{settings.IntersectDecimalPlaces}");

                    createdElements.AddRange(LayoutElementHelper.CreateTableCellSync(
                        layout,
                        $"{prefix}_Category_{i}_{timestamp}",
                        (startX, currentY),
                        (categoryColWidth, rowHeight),
                        row.Category,
                        isHeader,
                        rectSymbol,
                        textSymbol));
                    createdElements.AddRange(LayoutElementHelper.CreateTableCellSync(
                        layout,
                        $"{prefix}_Area_{i}_{timestamp}",
                        (startX + categoryColWidth, currentY),
                        (areaColWidth, rowHeight),
                        areaText,
                        isHeader,
                        rectSymbol,
                        textSymbol));
                    currentY -= rowHeight;
                }

                _currentCoordinateTableGroupName = $"MapSeries_IntersectTable_{timestamp}";
                createdElements.Clear();

                var _ = layout.GetElements().ToList();
                var layoutView = LayoutView.Active;
                if (layoutView != null && layoutView.Layout == layout)
                {
                    layoutView.Refresh();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"创建交集表格元素失败: {ex.Message}");
            }
        }

        private (double X, double Y) ResolveTableStartPosition(
            Layout layout,
            CoordinateTableSettings settings,
            string placementCorner,
            double cornerOffset,
            double tableWidth,
            double tableHeight)
        {
            if (settings.UseAnchorPosition)
            {
                var anchorElement = layout.FindElement(settings.AnchorElementName) as GraphicElement;
                if (anchorElement != null)
                {
                    var anchorBounds = anchorElement.GetBounds();
                    double anchorX = (anchorBounds.XMin + anchorBounds.XMax) / 2;
                    double anchorY = (anchorBounds.YMin + anchorBounds.YMax) / 2;
                    return ResolveCornerPosition(anchorX, anchorY, anchorX, anchorY, placementCorner, cornerOffset, tableWidth, tableHeight);
                }

                return (10, 300);
            }

            var mapFrame = ResolveMapFrame(layout, settings);
            if (mapFrame != null)
            {
                var mapBounds = mapFrame.GetBounds();
                return ResolveCornerPosition(
                    mapBounds.XMin,
                    mapBounds.YMin,
                    mapBounds.XMax,
                    mapBounds.YMax,
                    placementCorner,
                    cornerOffset,
                    tableWidth,
                    tableHeight);
            }

            return (10, 300);
        }

        private static (double X, double Y) ResolveCornerPosition(
            double xMin,
            double yMin,
            double xMax,
            double yMax,
            string placementCorner,
            double cornerOffset,
            double tableWidth,
            double tableHeight)
        {
            return placementCorner switch
            {
                "右下角" => (xMax - tableWidth - cornerOffset, yMin + cornerOffset + tableHeight),
                "左上角" => (xMin + cornerOffset, yMax - cornerOffset),
                "右上角" => (xMax - tableWidth - cornerOffset, yMax - cornerOffset),
                _ => (xMin + cornerOffset, yMin + cornerOffset + tableHeight)
            };
        }

        private MapFrame ResolveMapFrame(Layout layout, CoordinateTableSettings settings)
        {
            MapFrame mapFrame = null;
            if (!string.IsNullOrEmpty(settings?.MapFrameName))
            {
                mapFrame = layout.FindElement(settings.MapFrameName) as MapFrame;
            }

            return mapFrame ?? layout.Elements.OfType<MapFrame>().FirstOrDefault();
        }

        /// <summary>
        /// 在地图上创建界址点标注、点号和边长
        /// </summary>
        private void CreateBoundaryPointsOnMap(Layout layout, Polygon geometry, CoordinateTableSettings settings)
        {
            try
            {
                // 获取地图框
                MapFrame mapFrame = null;
                if (!string.IsNullOrEmpty(settings.MapFrameName))
                {
                    mapFrame = layout.FindElement(settings.MapFrameName) as MapFrame;
                }
                if (mapFrame == null)
                {
                    mapFrame = layout.Elements.OfType<MapFrame>().FirstOrDefault();
                }
                
                if (mapFrame == null)
                {
                    System.Diagnostics.Debug.WriteLine("未找到地图框，无法创建界址点");
                    return;
                }
                
                var map = mapFrame.Map;
                if (map == null)
                {
                    System.Diagnostics.Debug.WriteLine("地图框没有关联地图");
                    return;
                }
                
                // 获取或创建图形图层
                var graphicsLayer = GetOrCreateGraphicsLayer(map, "XF_界址点标注");
                if (graphicsLayer == null)
                {
                    System.Diagnostics.Debug.WriteLine("无法创建图形图层");
                    return;
                }
                
                // 清除该图层上的现有元素
                ClearGraphicsLayerElements(graphicsLayer);
                
                // 提取界址点坐标（不包括闭合点）
                var points = geometry.Points.ToList();
                if (points.Count > 1 && 
                    Math.Abs(points[0].X - points[points.Count - 1].X) < 0.001 &&
                    Math.Abs(points[0].Y - points[points.Count - 1].Y) < 0.001)
                {
                    points.RemoveAt(points.Count - 1); // 移除闭合点
                }
                
                // 获取点符号（优先从模板获取）
                var pointSymbol = GetPointSymbolFromTemplate(layout, settings.BoundaryPointSize);
                
                // 获取点号文本符号（优先从模板获取）
                var pointTextSymbol = settings.EnablePointLabels ? GetTextSymbolFromTemplate(layout, settings.PointLabelSize) : null;
                
                // 获取边长文本符号（优先从模板获取）
                var edgeTextSymbol = settings.EnableEdgeLabels ? GetEdgeTextSymbolFromTemplate(layout, settings.EdgeLabelSize) : null;
                
                // 计算比例尺用于距离换算
                double mapScale = mapFrame.Camera.Scale;
                double pointLabelDistanceInMapUnits = settings.PointLabelDistance * mapScale / 1000.0;
                double edgeLabelDistanceInMapUnits = settings.EdgeLabelDistance * mapScale / 1000.0;
                
                // 用于压盖检测的已放置标注位置列表（点号和边长共用）
                var placedLabels = new List<(double X, double Y, double Width, double Height, string Type)>();
                
                // 计算文本尺寸参数
                double ptToMm = 0.35;
                
                // 为每个界址点创建点元素、点号和边长（交替生成以实现互相压盖检测）
                for (int i = 0; i < points.Count; i++)
                {
                    var point = points[i];
                    var mapPoint = MapPointBuilderEx.CreateMapPoint(point.X, point.Y, geometry.SpatialReference);
                    
                    // 如果启用界址点，创建点图形元素
                    if (settings.EnableBoundaryPoints)
                    {
                        var pointGraphic = new CIMPointGraphic
                        {
                            Location = mapPoint,
                            Symbol = pointSymbol.MakeSymbolReference()
                        };
                        graphicsLayer.AddElement(pointGraphic);
                    }
                    
                    // 如果启用点号，创建点号文本
                    if (settings.EnablePointLabels && pointTextSymbol != null)
                    {
                        string labelText = settings.FormatPointLabel(i + 1);
                        
                        // 计算文本尺寸用于重叠检测
                        double charWidth = settings.PointLabelSize * ptToMm * 0.5;
                        double charHeight = settings.PointLabelSize * ptToMm * 0.8;
                        double labelWidthMm = labelText.Length * charWidth;
                        double labelWidth = labelWidthMm * mapScale / 1000.0;
                        double labelHeight = charHeight * mapScale / 1000.0;
                        
                        // 获取8方向候选位置（类CASS的选位算法）
                        var candidatePositions = GetCandidateLabelPositions8Dir(points, i, pointLabelDistanceInMapUnits, geometry.SpatialReference);
                        
                        MapPoint bestPosition = candidatePositions[0];
                        
                        // 压盖处理（检测与已有点号和边长的重叠）
                        if (settings.PointLabelOverlapMode == "压盖隐藏")
                        {
                            // 检查是否与已有标注重叠
                            bool shouldPlace = true;
                            foreach (var placed in placedLabels)
                            {
                                if (IsOverlapping(bestPosition.X, bestPosition.Y, labelWidth, labelHeight,
                                    placed.X, placed.Y, placed.Width, placed.Height))
                                {
                                    shouldPlace = false;
                                    break;
                                }
                            }
                            
                            if (shouldPlace)
                            {
                                placedLabels.Add((bestPosition.X, bestPosition.Y, labelWidth, labelHeight, "point"));
                                CreateTextGraphic(graphicsLayer, bestPosition, labelText, pointTextSymbol);
                            }
                        }
                        else if (settings.PointLabelOverlapMode == "压盖避让")
                        {
                            // 尝试找到不重叠的位置
                            foreach (var candidate in candidatePositions)
                            {
                                bool overlaps = false;
                                foreach (var placed in placedLabels)
                                {
                                    if (IsOverlapping(candidate.X, candidate.Y, labelWidth, labelHeight,
                                        placed.X, placed.Y, placed.Width, placed.Height))
                                    {
                                        overlaps = true;
                                        break;
                                    }
                                }
                                
                                if (!overlaps)
                                {
                                    bestPosition = candidate;
                                    break;
                                }
                            }
                            
                            // 始终放置（避让模式）
                            placedLabels.Add((bestPosition.X, bestPosition.Y, labelWidth, labelHeight, "point"));
                            CreateTextGraphic(graphicsLayer, bestPosition, labelText, pointTextSymbol);
                        }
                    }
                    
                    // 生成该点对应的边长标注（平行于边线，与点号交替生成实现互相检测）
                    if (settings.EnableEdgeLabels && edgeTextSymbol != null)
                    {
                        var p1 = points[i];
                        var p2 = points[(i + 1) % points.Count];
                        
                        // 计算边长
                        double dx = p2.X - p1.X;
                        double dy = p2.Y - p1.Y;
                        double edgeLength = Math.Sqrt(dx * dx + dy * dy);
                        
                        // 计算边的旋转角度（平行于边线）
                        double angleRad = Math.Atan2(dy, dx);
                        double angleDeg = angleRad * 180.0 / Math.PI;
                        
                        // 确保文字不会倒置（角度在-90到90度之间）
                        if (angleDeg > 90) angleDeg -= 180;
                        if (angleDeg < -90) angleDeg += 180;
                        
                        // 边长文本
                        string edgeLabelText = settings.FormatEdgeLabel(edgeLength);
                        
                        // 计算边长文本尺寸
                        double edgeCharWidth = settings.EdgeLabelSize * ptToMm * 0.5;
                        double edgeCharHeight = settings.EdgeLabelSize * ptToMm * 0.8;
                        double edgeLabelWidthMm = edgeLabelText.Length * edgeCharWidth;
                        double edgeLabelWidth = edgeLabelWidthMm * mapScale / 1000.0;
                        double edgeLabelHeight = edgeCharHeight * mapScale / 1000.0;
                        
                        // 获取边长标注候选位置（边的外侧）
                        var edgeCandidates = GetEdgeLabelCandidatePositions(p1, p2, edgeLabelDistanceInMapUnits, points, geometry.SpatialReference);
                        
                        MapPoint bestEdgePos = edgeCandidates[0];
                        
                        // 压盖处理（检测与已有点号和边长的重叠）
                        if (settings.EdgeLabelOverlapMode == "压盖隐藏")
                        {
                            bool shouldPlace = true;
                            foreach (var placed in placedLabels)
                            {
                                if (IsOverlapping(bestEdgePos.X, bestEdgePos.Y, edgeLabelWidth, edgeLabelHeight,
                                    placed.X, placed.Y, placed.Width, placed.Height))
                                {
                                    shouldPlace = false;
                                    break;
                                }
                            }
                            
                            if (shouldPlace)
                            {
                                placedLabels.Add((bestEdgePos.X, bestEdgePos.Y, edgeLabelWidth, edgeLabelHeight, "edge"));
                                CreateRotatedTextGraphic(graphicsLayer, bestEdgePos, edgeLabelText, edgeTextSymbol, angleDeg);
                            }
                        }
                        else if (settings.EdgeLabelOverlapMode == "压盖避让")
                        {
                            foreach (var candidate in edgeCandidates)
                            {
                                bool overlaps = false;
                                foreach (var placed in placedLabels)
                                {
                                    if (IsOverlapping(candidate.X, candidate.Y, edgeLabelWidth, edgeLabelHeight,
                                        placed.X, placed.Y, placed.Width, placed.Height))
                                    {
                                        overlaps = true;
                                        break;
                                    }
                                }
                                
                                if (!overlaps)
                                {
                                    bestEdgePos = candidate;
                                    break;
                                }
                            }
                            
                            placedLabels.Add((bestEdgePos.X, bestEdgePos.Y, edgeLabelWidth, edgeLabelHeight, "edge"));
                            CreateRotatedTextGraphic(graphicsLayer, bestEdgePos, edgeLabelText, edgeTextSymbol, angleDeg);
                        }
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"已创建 {points.Count} 个界址点标注");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"创建界址点失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 创建文本图形元素
        /// </summary>
        private void CreateTextGraphic(GraphicsLayer layer, MapPoint position, string text, CIMTextSymbol symbol)
        {
            var textGraphic = new CIMTextGraphic
            {
                Shape = position,
                Text = text,
                Symbol = symbol.MakeSymbolReference()
            };
            layer.AddElement(textGraphic);
        }
        
        /// <summary>
        /// 创建带旋转角度的文本图形元素（用于边长标注平行于边线）
        /// </summary>
        private void CreateRotatedTextGraphic(GraphicsLayer layer, MapPoint position, string text, CIMTextSymbol symbol, double angleDegrees)
        {
            // 复制符号并设置旋转角度
            var rotatedSymbol = symbol.Clone() as CIMTextSymbol;
            if (rotatedSymbol != null)
            {
                rotatedSymbol.Angle = angleDegrees;
            }
            
            var textGraphic = new CIMTextGraphic
            {
                Shape = position,
                Text = text,
                Symbol = (rotatedSymbol ?? symbol).MakeSymbolReference()
            };
            layer.AddElement(textGraphic);
        }
        
        /// <summary>
        /// 检测两个矩形是否重叠
        /// </summary>
        private bool IsOverlapping(double x1, double y1, double w1, double h1,
            double x2, double y2, double w2, double h2)
        {
            return Math.Abs(x1 - x2) < (w1 + w2) / 2 * 0.9 &&  // 0.9系数允许少量重叠
                   Math.Abs(y1 - y2) < (h1 + h2) / 2 * 0.9;
        }
        
        /// <summary>
        /// 从布局模板获取点符号（应用用户设置的大小）
        /// </summary>
        private CIMPointSymbol GetPointSymbolFromTemplate(Layout layout, double userSize)
        {
            CIMPointSymbol symbol = null;
            
            try
            {
                var template = layout.FindElement("XF_JZD") as GraphicElement;
                if (template != null)
                {
                    var graphic = template.GetGraphic();
                    if (graphic is CIMPointGraphic pointGraphic)
                    {
                        symbol = pointGraphic.Symbol?.Symbol as CIMPointSymbol;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取点符号模板失败: {ex.Message}");
            }
            
            // 如果没有模板，创建默认红色点符号
            if (symbol == null)
            {
                symbol = CreateRedPointSymbol(userSize);
            }
            else
            {
                // 有模板时，克隆并应用用户设置的大小
                symbol = symbol.Clone() as CIMPointSymbol;
                if (symbol != null)
                {
                    symbol.SetSize(userSize);
                }
            }
            
            return symbol;
        }
        
        /// <summary>
        /// 从布局模板获取文本符号（应用用户设置的大小，设置为中心对齐）
        /// </summary>
        private CIMTextSymbol GetTextSymbolFromTemplate(Layout layout, double userSize)
        {
            CIMTextSymbol symbol = null;
            
            try
            {
                var template = layout.FindElement("XF_DH") as GraphicElement;
                if (template != null)
                {
                    var graphic = template.GetGraphic();
                    if (graphic is CIMTextGraphic textGraphic)
                    {
                        symbol = textGraphic.Symbol?.Symbol as CIMTextSymbol;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取文本符号模板失败: {ex.Message}");
            }
            
            // 如果没有模板，创建默认红色文本符号
            if (symbol == null)
            {
                var redColor = CIMColor.CreateRGBColor(255, 0, 0);
                symbol = SymbolFactory.Instance.ConstructTextSymbol(redColor, userSize, "Arial", "Regular");
            }
            else
            {
                // 有模板时，克隆并应用用户设置的大小
                symbol = symbol.Clone() as CIMTextSymbol;
                if (symbol != null)
                {
                    symbol.SetSize(userSize);
                }
            }
            
            // 设置文本为中心对齐（水平和垂直居中）
            symbol.HorizontalAlignment = ArcGIS.Core.CIM.HorizontalAlignment.Center;
            symbol.VerticalAlignment = ArcGIS.Core.CIM.VerticalAlignment.Center;
            
            return symbol;
        }
        
        /// <summary>
        /// 从布局模板获取边长文本符号（应用用户设置的大小）
        /// </summary>
        private CIMTextSymbol GetEdgeTextSymbolFromTemplate(Layout layout, double userSize)
        {
            CIMTextSymbol symbol = null;
            
            try
            {
                var template = layout.FindElement("XF_BC") as GraphicElement;
                if (template != null)
                {
                    var graphic = template.GetGraphic();
                    if (graphic is CIMTextGraphic textGraphic)
                    {
                        symbol = textGraphic.Symbol?.Symbol as CIMTextSymbol;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取边长文本符号模板失败: {ex.Message}");
            }
            
            // 如果没有模板，创建默认红色文本符号
            if (symbol == null)
            {
                var redColor = CIMColor.CreateRGBColor(255, 0, 0);
                symbol = SymbolFactory.Instance.ConstructTextSymbol(redColor, userSize, "Arial", "Regular");
            }
            else
            {
                // 有模板时，克隆并应用用户设置的大小
                symbol = symbol.Clone() as CIMTextSymbol;
                if (symbol != null)
                {
                    symbol.SetSize(userSize);
                }
            }
            
            // 设置文本为中心对齐
            symbol.HorizontalAlignment = ArcGIS.Core.CIM.HorizontalAlignment.Center;
            symbol.VerticalAlignment = ArcGIS.Core.CIM.VerticalAlignment.Center;
            
            return symbol;
        }
        
        /// <summary>
        /// 计算点号标注位置（始终在多边形外部，沿外角角平分线方向）
        /// </summary>
        private MapPoint CalculateLabelPosition(List<MapPoint> points, int index, double distance, SpatialReference sr)
        {
            int count = points.Count;
            var current = points[index];
            var prev = points[(index - 1 + count) % count];
            var next = points[(index + 1) % count];
            
            // 计算从当前点指向前后点的向量
            double v1x = prev.X - current.X;
            double v1y = prev.Y - current.Y;
            double v2x = next.X - current.X;
            double v2y = next.Y - current.Y;
            
            // 归一化
            double len1 = Math.Sqrt(v1x * v1x + v1y * v1y);
            double len2 = Math.Sqrt(v2x * v2x + v2y * v2y);
            if (len1 > 0.0001) { v1x /= len1; v1y /= len1; }
            if (len2 > 0.0001) { v2x /= len2; v2y /= len2; }
            
            // 角平分线方向 = 两个单位向量之和
            double bisectX = v1x + v2x;
            double bisectY = v1y + v2y;
            double bisectLen = Math.Sqrt(bisectX * bisectX + bisectY * bisectY);
            
            if (bisectLen < 0.0001)
            {
                // 180度角，使用v1的垂直方向
                bisectX = -v1y;
                bisectY = v1x;
                bisectLen = 1.0;
            }
            else
            {
                bisectX /= bisectLen;
                bisectY /= bisectLen;
            }
            
            // 测试bisect方向的点是否在多边形内部
            double testDist = distance * 0.1; // 用小距离测试
            double testX = current.X + bisectX * testDist;
            double testY = current.Y + bisectY * testDist;
            
            bool isInside = IsPointInPolygon(testX, testY, points);
            
            // 如果测试点在内部，说明bisect指向内部，需要取反方向
            // 如果测试点在外部，说明bisect指向外部，保持方向
            double outX, outY;
            if (isInside)
            {
                // bisect指向内部，取反得到外部方向
                outX = -bisectX;
                outY = -bisectY;
            }
            else
            {
                // bisect已经指向外部
                outX = bisectX;
                outY = bisectY;
            }
            
            // 计算标注位置（沿外部方向偏移）
            double labelX = current.X + outX * distance;
            double labelY = current.Y + outY * distance;
            
            return MapPointBuilderEx.CreateMapPoint(labelX, labelY, sr);
        }
        
        /// <summary>
        /// 判断点是否在多边形内部（射线法）
        /// </summary>
        private bool IsPointInPolygon(double x, double y, List<MapPoint> polygon)
        {
            int count = polygon.Count;
            bool inside = false;
            
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                double xi = polygon[i].X, yi = polygon[i].Y;
                double xj = polygon[j].X, yj = polygon[j].Y;
                
                if (((yi > y) != (yj > y)) &&
                    (x < (xj - xi) * (y - yi) / (yj - yi) + xi))
                {
                    inside = !inside;
                }
            }
            
            return inside;
        }
        
        /// <summary>
        /// 获变8方向候选标注位置列表（类CASS的选位算法）
        /// 优先级：角平分线外侧 > 右上 > 左上 > 右下 > 左下 > 右 > 上 > 左 > 下
        /// </summary>
        private List<MapPoint> GetCandidateLabelPositions8Dir(List<MapPoint> points, int index, double distance, SpatialReference sr)
        {
            var candidates = new List<MapPoint>();
            int count = points.Count;
            var current = points[index];
            
            // 首选位置：角平分线外侧
            var primaryPos = CalculateLabelPosition(points, index, distance, sr);
            candidates.Add(primaryPos);
            
            // 8方向候选位置（类CASS）
            double sqrt2 = Math.Sqrt(2) / 2;
            var directions = new (double dx, double dy, string name)[]
            {
                (sqrt2, sqrt2, "右上"),      // 右上
                (-sqrt2, sqrt2, "左上"),     // 左上
                (sqrt2, -sqrt2, "右下"),     // 右下
                (-sqrt2, -sqrt2, "左下"),    // 左下
                (1, 0, "右"),                // 右
                (0, 1, "上"),                // 上
                (-1, 0, "左"),               // 左
                (0, -1, "下")                // 下
            };
            
            // 不同距离的候选位置
            double[] distanceFactors = { 1.0, 1.3, 1.6, 2.0 };
            
            foreach (var factor in distanceFactors)
            {
                double d = distance * factor;
                
                foreach (var (dx, dy, name) in directions)
                {
                    double posX = current.X + dx * d;
                    double posY = current.Y + dy * d;
                    
                    // 只添加在多边形外部的位置
                    if (!IsPointInPolygon(posX, posY, points))
                    {
                        candidates.Add(MapPointBuilderEx.CreateMapPoint(posX, posY, sr));
                    }
                }
            }
            
            // 远距离候选位置（角平分线方向）
            candidates.Add(CalculateLabelPosition(points, index, distance * 2.5, sr));
            candidates.Add(CalculateLabelPosition(points, index, distance * 3.0, sr));
            
            return candidates;
        }
        
        /// <summary>
        /// 获取边长标注候选位置列表（边的外侧）
        /// </summary>
        private List<MapPoint> GetEdgeLabelCandidatePositions(MapPoint p1, MapPoint p2, double distance, List<MapPoint> polygon, SpatialReference sr)
        {
            var candidates = new List<MapPoint>();
            
            // 边的中点
            double midX = (p1.X + p2.X) / 2;
            double midY = (p1.Y + p2.Y) / 2;
            
            // 边的方向向量
            double edgeX = p2.X - p1.X;
            double edgeY = p2.Y - p1.Y;
            double edgeLen = Math.Sqrt(edgeX * edgeX + edgeY * edgeY);
            if (edgeLen < 0.0001) edgeLen = 0.0001;
            edgeX /= edgeLen;
            edgeY /= edgeLen;
            
            // 边的法向量（左侧和右侧）
            double n1x = -edgeY, n1y = edgeX;  // 左侧法向
            double n2x = edgeY, n2y = -edgeX;   // 右侧法向
            
            // 测试哪个方向是外侧
            double test1X = midX + n1x * distance * 0.1;
            double test1Y = midY + n1y * distance * 0.1;
            bool n1IsOutside = !IsPointInPolygon(test1X, test1Y, polygon);
            
            // 优先使用外侧方向
            double primaryNx = n1IsOutside ? n1x : n2x;
            double primaryNy = n1IsOutside ? n1y : n2y;
            double secondaryNx = n1IsOutside ? n2x : n1x;
            double secondaryNy = n1IsOutside ? n2y : n1y;
            
            // 不同距离的候选位置
            double[] distanceFactors = { 1.0, 1.5, 2.0, 2.5 };
            
            // 外侧候选位置
            foreach (var factor in distanceFactors)
            {
                double d = distance * factor;
                double posX = midX + primaryNx * d;
                double posY = midY + primaryNy * d;
                candidates.Add(MapPointBuilderEx.CreateMapPoint(posX, posY, sr));
            }
            
            // 内侧候选位置（作为备选）
            foreach (var factor in distanceFactors)
            {
                double d = distance * factor;
                double posX = midX + secondaryNx * d;
                double posY = midY + secondaryNy * d;
                if (!IsPointInPolygon(posX, posY, polygon))
                {
                    candidates.Add(MapPointBuilderEx.CreateMapPoint(posX, posY, sr));
                }
            }
            
            // 沿边方向偏移的候选位置
            double[] offsetFactors = { 0.2, -0.2, 0.3, -0.3 };
            foreach (var offsetFactor in offsetFactors)
            {
                double offsetX = midX + edgeX * edgeLen * offsetFactor;
                double offsetY = midY + edgeY * edgeLen * offsetFactor;
                double posX = offsetX + primaryNx * distance;
                double posY = offsetY + primaryNy * distance;
                if (!IsPointInPolygon(posX, posY, polygon))
                {
                    candidates.Add(MapPointBuilderEx.CreateMapPoint(posX, posY, sr));
                }
            }
            
            return candidates;
        }
        
        /// <summary>
        /// 获取候选标注位置列表（兼容旧版本）
        /// </summary>
        private List<MapPoint> GetCandidateLabelPositions(List<MapPoint> points, int index, double distance, SpatialReference sr)
        {
            return GetCandidateLabelPositions8Dir(points, index, distance, sr);
        }
        
        /// <summary>
        /// 获取或创建图形图层
        /// </summary>
        private GraphicsLayer GetOrCreateGraphicsLayer(Map map, string layerName)
        {
            try
            {
                // 查找现有图形图层
                var existingLayer = map.GetLayersAsFlattenedList()
                    .OfType<GraphicsLayer>()
                    .FirstOrDefault(l => l.Name == layerName);
                
                if (existingLayer != null)
                {
                    return existingLayer;
                }
                
                // 创建新的图形图层
                var graphicsLayerParams = new GraphicsLayerCreationParams
                {
                    Name = layerName
                };
                
                var newLayer = LayerFactory.Instance.CreateLayer<GraphicsLayer>(graphicsLayerParams, map);
                return newLayer;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取或创建图形图层失败: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 清除图形图层上的所有元素
        /// </summary>
        private void ClearGraphicsLayerElements(GraphicsLayer graphicsLayer)
        {
            try
            {
                var elements = graphicsLayer.GetElementsAsFlattenedList();
                if (elements != null && elements.Any())
                {
                    graphicsLayer.RemoveElements(elements);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清除图形图层元素失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 创建红色点符号
        /// </summary>
        private CIMPointSymbol CreateRedPointSymbol(double sizeInPoints)
        {
            // 使用SymbolFactory创建简单圆形标记符号
            // 红色填充，深红色边框
            var redColor = CIMColor.CreateRGBColor(255, 0, 0);
            var darkRedColor = CIMColor.CreateRGBColor(139, 0, 0);
            
            // 创建简单标记符号（圆形）
            var pointSymbol = SymbolFactory.Instance.ConstructPointSymbol(
                redColor,
                sizeInPoints,
                SimpleMarkerStyle.Circle);
            
            // 设置边框
            if (pointSymbol.SymbolLayers != null && pointSymbol.SymbolLayers.Length > 0)
            {
                var markerLayer = pointSymbol.SymbolLayers[0] as CIMVectorMarker;
                if (markerLayer?.MarkerGraphics != null && markerLayer.MarkerGraphics.Length > 0)
                {
                    var markerGraphic = markerLayer.MarkerGraphics[0];
                    if (markerGraphic.Symbol is CIMPolygonSymbol polySymbol)
                    {
                        // 添加深红色边框
                        var strokeSymbol = SymbolFactory.Instance.ConstructStroke(darkRedColor, 0.5, SimpleLineStyle.Solid);
                        var solidFill = SymbolFactory.Instance.ConstructSolidFill(redColor);
                        polySymbol.SymbolLayers = new CIMSymbolLayer[] { strokeSymbol, solidFill };
                    }
                }
            }
            
            return pointSymbol;
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        #endregion
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
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;
        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
        public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;
        public void Execute(object parameter) => _execute();
    }

    /// <summary>
    /// 带参数的命令实现
    /// </summary>
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Func<T, bool> _canExecute;
        public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
        public bool CanExecute(object parameter) => _canExecute?.Invoke((T)parameter) ?? true;
        public void Execute(object parameter) => _execute((T)parameter);
    }
}
