using ArcGIS.Core.CIM;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using XIAOFUTools.Tools.HistoricalImageryDownload;
using XIAOFUTools.Tools.HistoricalImageryDownload.Services;

namespace XIAOFUTools.Tools.HistoricalImagery
{
    /// <summary>
    /// 历史影像版本数据模型
    /// </summary>
    public class WaybackVersion : PropertyChangedBase
    {
        private bool _isSelected;

        public int ReleaseNum { get; set; }
        public string ReleaseDate { get; set; }
        public int Year { get; set; }
        public string ItemTitle { get; set; }
        public string Url { get; set; }
        public string MetadataLayerUrl { get; set; }
        public string AcquisitionDate { get; set; }
        public bool IsChanged { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public string DisplayName => $"{ReleaseDate}";
    }

    /// <summary>
    /// 当前位置影像元数据
    /// </summary>
    public class ImageryMetadata : PropertyChangedBase
    {
        public string AcquisitionDate { get; set; }
        public string Provider { get; set; }
        public string Satellite { get; set; }
        public string Resolution { get; set; }
        public string Accuracy { get; set; }
        public int ZoomLevel { get; set; }
        public string ReleaseTitle { get; set; }
    }

    /// <summary>
    /// 树形节点数据模型
    /// </summary>
    public class TreeNode : PropertyChangedBase
    {
        private bool _isExpanded;
        private bool _isSelected;

        public string Header { get; set; }
        public ObservableCollection<TreeNode> Children { get; set; }
        public WaybackVersion Version { get; set; }
        public bool IsLeaf => Version != null;

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public TreeNode()
        {
            Children = new ObservableCollection<TreeNode>();
        }
    }

    /// <summary>
    /// 历史影像停靠窗格视图模型
    /// </summary>
    internal class HistoricalImageryDockPaneViewModel : PropertyChangedBase
    {
        private static readonly HttpClient _httpClient = new HttpClient()
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        private ObservableCollection<TreeNode> _treeNodes;
        private bool _isLoading;
        private string _statusMessage;
        private TreeNode _selectedNode;
        private string _searchText;
        private ImageryMetadata _currentMetadata;
        private string _metadataDisplay;
        private bool _showChangedOnly;

        public ObservableCollection<TreeNode> TreeNodes
        {
            get => _treeNodes;
            set => SetProperty(ref _treeNodes, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public TreeNode SelectedNode
        {
            get => _selectedNode;
            set => SetProperty(ref _selectedNode, value);
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    FilterVersions();
                }
            }
        }

        public ImageryMetadata CurrentMetadata
        {
            get => _currentMetadata;
            set => SetProperty(ref _currentMetadata, value);
        }

        public string MetadataDisplay
        {
            get => _metadataDisplay;
            set => SetProperty(ref _metadataDisplay, value);
        }

        public bool ShowChangedOnly
        {
            get => _showChangedOnly;
            set
            {
                if (SetProperty(ref _showChangedOnly, value))
                {
                    if (value)
                    {
                        _ = DetectChangedVersionsAsync();
                    }
                    else
                    {
                        _allVersions = _catalogVersions.Select(CloneVersion).ToList();
                        FilterVersions();
                    }
                }
            }
        }

        public ICommand RefreshCommand { get; }
        public ICommand AddLayerCommand { get; }
        public ICommand ClearSearchCommand { get; }
        public ICommand QueryMetadataCommand { get; }

        private List<WaybackVersion> _allVersions;
        private List<WaybackVersion> _catalogVersions;
        private readonly WaybackHistoricalImageryProvider _waybackProvider = new();

        public HistoricalImageryDockPaneViewModel()
        {
            TreeNodes = new ObservableCollection<TreeNode>();
            _allVersions = new List<WaybackVersion>();
            _catalogVersions = new List<WaybackVersion>();
            MetadataDisplay = "点击查询按钮获取当前位置影像信息";

            RefreshCommand = new RelayCommand(async () => await RefreshAsync());
            AddLayerCommand = new RelayCommand(AddSelectedLayer, () => SelectedNode?.IsLeaf == true);
            ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);
            QueryMetadataCommand = new RelayCommand(async () => await QueryCurrentMetadataAsync());

            // 自动加载版本列表
            _ = LoadVersionsAsync();
        }

        /// <summary>
        /// 将地图缩放级别转换为查询级别
        /// </summary>
        private int MapZoomToQueryLevel(double mapZoom)
        {
            int zoom = (int)Math.Round(mapZoom);
            
            if (zoom >= 19) return 4;
            if (zoom == 18) return 5;
            if (zoom == 17) return 6;
            if (zoom == 16) return 7;
            if (zoom == 15) return 8;
            if (zoom == 14) return 9;
            if (zoom == 13) return 10;
            if (zoom == 12) return 11;
            if (zoom == 11) return 12;
            
            // 无12以上
            return 12;
        }

        /// <summary>
        /// 查询当前位置的影像元数据
        /// </summary>
        private async Task QueryCurrentMetadataAsync()
        {
            try
            {
                if (MapView.Active?.Map == null)
                {
                    MetadataDisplay = "当前没有活动地图";
                    return;
                }

                // 获取最新版本
                if (_catalogVersions == null || _catalogVersions.Count == 0)
                {
                    MetadataDisplay = "请先加载历史影像列表";
                    return;
                }

                MetadataDisplay = "正在查询...";

                var queryContext = await CaptureMetadataQueryContextAsync();
                var catalog = _catalogVersions.Select(ToHistoricalVersionItem).ToList();
                var resolution = WaybackMetadataSourceResolver.Resolve(catalog, queryContext.MapLayers);
                var candidate = await ResolveMetadataSourceCandidateAsync(resolution);
                if (candidate == null)
                {
                    MetadataDisplay = "已取消查询";
                    return;
                }

                if (string.IsNullOrEmpty(candidate.Version.MetadataLayerUrl))
                {
                    MetadataDisplay = "所选版本没有元数据服务";
                    return;
                }

                var metadata = await QueryWaybackMetadata(
                    queryContext.Longitude,
                    queryContext.Latitude,
                    queryContext.QueryLevel,
                    candidate.Version.MetadataLayerUrl,
                    candidate.Version.Summary ?? candidate.Version.DisplayDate ?? candidate.Version.VersionId);

                if (metadata != null)
                {
                    CurrentMetadata = metadata;
                    MetadataDisplay = BuildMetadataDisplay(candidate, metadata);
                }
                else
                {
                    MetadataDisplay = BuildNoDataDisplay(candidate);
                }
            }
            catch (Exception ex)
            {
                MetadataDisplay = $"查询失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 刷新历史影像列表
        /// </summary>
        private async Task RefreshAsync()
        {
            await LoadVersionsAsync();
        }

        /// <summary>
        /// 根据搜索文本过滤版本
        /// </summary>
        private void FilterVersions()
        {
            if (_allVersions == null || _allVersions.Count == 0)
                return;

            var searchText = SearchText?.Trim().ToLower() ?? string.Empty;
            
            // 先根据是否只显示改变的版本进行过滤
            var filteredByChange = ShowChangedOnly
                ? _allVersions.Where(v => v.IsChanged).ToList()
                : _allVersions;

            if (string.IsNullOrWhiteSpace(searchText))
            {
                // 显示过滤后的版本
                BuildTreeStructure(filteredByChange);
                if (ShowChangedOnly && filteredByChange.Count < _allVersions.Count)
                {
                    StatusMessage = $"显示 {filteredByChange.Count} 个影像改变的版本（共 {_allVersions.Count} 个）";
                }
            }
            else
            {
                // 再根据搜索文本过滤
                var filtered = filteredByChange.Where(v =>
                    v.ReleaseDate.Contains(searchText) ||
                    v.Year.ToString().Contains(searchText) ||
                    v.ItemTitle.ToLower().Contains(searchText)
                ).ToList();

                BuildTreeStructure(filtered);
                StatusMessage = $"找到 {filtered.Count} 个匹配的历史影像版本";
            }
        }

        /// <summary>
        /// 检测影像改变的版本（多线程并发查询）
        /// </summary>
        private async Task DetectChangedVersionsAsync()
        {
            if (_catalogVersions == null || _catalogVersions.Count == 0)
                return;

            IsLoading = true;
            StatusMessage = "正在检测当前位置的真实变化版本...";

            try
            {
                var versions = await GetWaybackVersionsAsync(includeAllVersions: false);
                _allVersions = versions;
                var changedCount = versions.Count(v => v.IsChanged);
                StatusMessage = $"检测完成：{changedCount} 个版本在当前位置发生真实变化";
                FilterVersions();
            }
            catch (Exception ex)
            {
                StatusMessage = $"检测失败: {ex.Message}";
                ShowChangedOnly = false;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 查询单个版本的元数据
        /// </summary>
        private async Task QuerySingleVersionMetadataAsync(WaybackVersion version, double lon, double lat, int zoomLevel)
        {
            if (string.IsNullOrEmpty(version.MetadataLayerUrl))
                return;

            try
            {
                var metadata = await QueryWaybackMetadata(lon, lat, zoomLevel, version.MetadataLayerUrl, version.ItemTitle);
                if (metadata != null)
                {
                    version.AcquisitionDate = metadata.AcquisitionDate;
                }
            }
            catch
            {
                // 忽略单个查询失败
            }
        }

        /// <summary>
        /// 构建树形结构
        /// </summary>
        private void BuildTreeStructure(List<WaybackVersion> versions)
        {
            var yearGroups = versions
                .Where(v => v.Year > 0)
                .GroupBy(v => v.Year)
                .OrderByDescending(g => g.Key);

            TreeNodes.Clear();

            foreach (var yearGroup in yearGroups)
            {
                var yearNode = new TreeNode
                {
                    Header = $"{yearGroup.Key}年 ({yearGroup.Count()}个版本)",
                    IsExpanded = yearGroup.Key == DateTime.Now.Year || versions.Count <= 50 // 搜索结果少时全部展开
                };

                foreach (var version in yearGroup.OrderByDescending(v => v.ReleaseDate))
                {
                    yearNode.Children.Add(new TreeNode
                    {
                        Header = version.DisplayName,
                        Version = version
                    });
                }

                TreeNodes.Add(yearNode);
            }
        }

        /// <summary>
        /// 加载 Wayback 版本列表
        /// </summary>
        private async Task LoadVersionsAsync()
        {
            IsLoading = true;
            StatusMessage = "正在获取历史影像列表...";

            try
            {
                var catalogVersions = await GetWaybackVersionsAsync(includeAllVersions: true);
                _catalogVersions = catalogVersions;

                if (ShowChangedOnly)
                {
                    _allVersions = await GetWaybackVersionsAsync(includeAllVersions: false);
                }
                else
                {
                    _allVersions = catalogVersions.Select(CloneVersion).ToList();
                }

                if (_allVersions != null && _allVersions.Count > 0)
                {
                    BuildTreeStructure(_allVersions);

                    var yearCount = _allVersions.Select(v => v.Year).Distinct().Count();
                    StatusMessage = ShowChangedOnly
                        ? $"共找到 {_allVersions.Count} 个当前位置发生真实变化的版本，分布在 {yearCount} 年"
                        : $"共找到 {_allVersions.Count} 个历史影像版本，分布在 {yearCount} 年";
                }
                else
                {
                    StatusMessage = "未获取到历史影像版本";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"获取失败: {ex.Message}";
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    $"获取历史影像列表失败：{ex.Message}",
                    "错误",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }


        /// <summary>
        /// 添加选中的图层到地图
        /// </summary>
        private async void AddSelectedLayer()
        {
            if (SelectedNode?.Version == null)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    "请选择一个历史影像版本",
                    "提示",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }

            var version = SelectedNode.Version;

            try
            {
                await QueuedTask.Run(() =>
                {
                    var mapView = MapView.Active;
                    if (mapView?.Map == null)
                    {
                        ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                            "当前没有活动地图",
                            "错误",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Error);
                        return;
                    }

                    // 通过 URI 创建图层
                    var uri = new Uri(version.Url);
                    var layer = LayerFactory.Instance.CreateLayer(uri, mapView.Map, layerName: $"Wayback {version.ReleaseDate}");

                    if (layer != null)
                    {
                        // 将图层移到底部
                        var allLayers = mapView.Map.Layers.ToList();
                        if (allLayers.Count > 1)
                        {
                            mapView.Map.MoveLayer(layer, allLayers.Count - 1);
                        }
                    }
                });

                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    $"已添加历史影像图层：{version.ReleaseDate}",
                    "成功",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    $"添加图层失败：{ex.Message}",
                    "错误",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 获取 ArcGIS Wayback 历史影像版本列表
        /// </summary>
        private async Task<List<WaybackVersion>> GetWaybackVersionsAsync(bool includeAllVersions)
        {
            var query = includeAllVersions
                ? new HistoricalVersionQuery
                {
                    IncludeAllVersions = true,
                    ZoomLevel = 12
                }
                : await BuildCurrentMapQueryAsync();

            var versions = await _waybackProvider.QueryVersionsAsync(query);
            return versions.Select(MapVersion).ToList();
        }

        private async Task<HistoricalVersionQuery> BuildCurrentMapQueryAsync()
        {
            return await QueuedTask.Run(() =>
            {
                var mapView = MapView.Active;
                if (mapView?.Map == null)
                {
                    throw new InvalidOperationException("当前没有活动地图，无法检测当前位置的变化版本");
                }

                var center = mapView.Extent.Center;
                var centerWgs84 = GeometryEngine.Instance.Project(center, SpatialReferences.WGS84) as MapPoint
                    ?? throw new InvalidOperationException("无法获取当前地图中心点");

                var zoomLevel = (int)Math.Round(Math.Log(591657550.5 / mapView.Camera.Scale, 2));
                zoomLevel = Math.Clamp(zoomLevel, 1, 23);

                return new HistoricalVersionQuery
                {
                    Longitude = centerWgs84.X,
                    Latitude = centerWgs84.Y,
                    ZoomLevel = zoomLevel,
                    IncludeAllVersions = false
                };
            });
        }

        private static WaybackVersion MapVersion(HistoricalVersionItem version)
        {
            var releaseDate = version.DisplayDate ?? string.Empty;
            var year = 0;
            if (!string.IsNullOrWhiteSpace(releaseDate))
            {
                var segments = releaseDate.Split('-');
                if (segments.Length > 0)
                {
                    int.TryParse(segments[0], out year);
                }
            }

            var releaseNum = 0;
            int.TryParse(version.VersionId, out releaseNum);

            return new WaybackVersion
            {
                ReleaseNum = releaseNum,
                ReleaseDate = releaseDate,
                Year = year,
                ItemTitle = version.Summary ?? version.DisplayDate ?? version.VersionId,
                Url = version.TileUrlTemplate ?? string.Empty,
                MetadataLayerUrl = version.MetadataLayerUrl ?? string.Empty,
                AcquisitionDate = version.AcquisitionDate,
                IsChanged = string.Equals(version.ChangeKey, version.VersionId, StringComparison.Ordinal)
            };
        }

        private static WaybackVersion CloneVersion(WaybackVersion version)
        {
            return new WaybackVersion
            {
                ReleaseNum = version.ReleaseNum,
                ReleaseDate = version.ReleaseDate,
                Year = version.Year,
                ItemTitle = version.ItemTitle,
                Url = version.Url,
                MetadataLayerUrl = version.MetadataLayerUrl,
                AcquisitionDate = version.AcquisitionDate,
                IsChanged = version.IsChanged
            };
        }

        private async Task<MetadataQueryContext> CaptureMetadataQueryContextAsync()
        {
            return await QueuedTask.Run(() =>
            {
                var mapView = MapView.Active;
                if (mapView?.Map == null)
                {
                    throw new InvalidOperationException("当前没有活动地图");
                }

                var center = mapView.Extent.Center;
                var centerWgs84 = GeometryEngine.Instance.Project(center, SpatialReferences.WGS84) as MapPoint
                    ?? throw new InvalidOperationException("无法获取当前地图中心点");

                var mapScale = mapView.Camera.Scale;
                var zoomLevel = (int)Math.Round(Math.Log(591657550.5 / mapScale, 2));
                var queryLevel = MapZoomToQueryLevel(zoomLevel);
                var mapLayers = mapView.Map.GetLayersAsFlattenedList()
                    .Select(layer => new WaybackMapLayerReference(layer.Name, layer.URI))
                    .ToList();

                return new MetadataQueryContext(centerWgs84.X, centerWgs84.Y, queryLevel, mapLayers);
            });
        }

        private async Task<WaybackMetadataSourceCandidate?> ResolveMetadataSourceCandidateAsync(WaybackMetadataSourceResolution resolution)
        {
            if (!resolution.RequiresSelection)
            {
                return resolution.AutoSelectedCandidate;
            }

            return await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var dialog = new HistoricalImageryMetadataSourceDialog(resolution.Candidates);
                var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive);
                if (owner != null)
                {
                    dialog.Owner = owner;
                }

                return dialog.ShowDialog() == true ? dialog.SelectedCandidate : null;
            });
        }

        private static HistoricalVersionItem ToHistoricalVersionItem(WaybackVersion version)
        {
            return new HistoricalVersionItem
            {
                Provider = HistoricalImageryProviderType.Wayback,
                VersionId = version.ReleaseNum.ToString(),
                DisplayDate = version.ReleaseDate,
                Summary = version.ItemTitle,
                MetadataLayerUrl = version.MetadataLayerUrl,
                TileUrlTemplate = version.Url
            };
        }

        private static string BuildMetadataDisplay(WaybackMetadataSourceCandidate candidate, ImageryMetadata metadata)
        {
            return $"{BuildSourceHeader(candidate)}\n" +
                   $"采集日期: {metadata.AcquisitionDate ?? "N/A"} | " +
                   $"卫星: {metadata.Satellite ?? "N/A"} | " +
                   $"分辨率: {metadata.Resolution}m | " +
                   $"级别: {metadata.ZoomLevel}";
        }

        private static string BuildNoDataDisplay(WaybackMetadataSourceCandidate candidate)
            => $"{BuildSourceHeader(candidate)}\n该位置无影像数据";

        private static string BuildSourceHeader(WaybackMetadataSourceCandidate candidate)
        {
            var versionText = string.IsNullOrWhiteSpace(candidate.Version.DisplayDate)
                ? candidate.Version.VersionId
                : candidate.Version.DisplayDate;

            return candidate.SourceType == WaybackMetadataSourceType.GlobalLatest
                ? $"查询来源: 全局最新版本\n版本: {versionText}"
                : $"查询来源: 当前地图图层\n图层: {candidate.LayerName ?? "未命名图层"}\n版本: {versionText}";
        }

        private sealed record MetadataQueryContext(
            double Longitude,
            double Latitude,
            int QueryLevel,
            IReadOnlyList<WaybackMapLayerReference> MapLayers);

        /// <summary>
        /// 查询Wayback影像元数据
        /// </summary>
        private async Task<ImageryMetadata> QueryWaybackMetadata(double lon, double lat, int zoomLevel, string metadataUrl, string releaseTitle)
        {
            try
            {
                // 构建查询URL
                var queryUrl = $"{metadataUrl}/{zoomLevel}/query";
                
                var geometry = new
                {
                    spatialReference = new { wkid = 4326 },
                    x = lon,
                    y = lat
                };

                var queryParams = new Dictionary<string, string>
                {
                    { "f", "json" },
                    { "where", "1=1" },
                    { "outFields", "SRC_DATE2,NICE_DESC,SRC_DESC,SAMP_RES,SRC_ACC" },
                    { "geometry", JsonSerializer.Serialize(geometry) },
                    { "returnGeometry", "false" },
                    { "geometryType", "esriGeometryPoint" },
                    { "spatialRel", "esriSpatialRelIntersects" }
                };

                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
                var content = new FormUrlEncodedContent(queryParams);
                var response = await _httpClient.PostAsync(queryUrl, content, cts.Token);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<JsonElement>(json);

                // 检查错误
                if (result.TryGetProperty("error", out var error))
                {
                    return null;
                }

                // 检查是否有数据
                if (!result.TryGetProperty("features", out var features) || features.GetArrayLength() == 0)
                {
                    return null;
                }

                // 解析第一个要素的属性
                var attrs = features[0].GetProperty("attributes");
                
                string acquisitionDate = null;
                if (attrs.TryGetProperty("SRC_DATE2", out var srcDate) && srcDate.ValueKind != JsonValueKind.Null)
                {
                    long timestamp = srcDate.GetInt64();
                    var date = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).DateTime;
                    acquisitionDate = date.ToString("yyyy-MM-dd");
                }

                var metadata = new ImageryMetadata
                {
                    AcquisitionDate = acquisitionDate,
                    Provider = attrs.TryGetProperty("NICE_DESC", out var provider) ? provider.GetString() : "N/A",
                    Satellite = attrs.TryGetProperty("SRC_DESC", out var satellite) ? satellite.GetString() : "N/A",
                    Resolution = attrs.TryGetProperty("SAMP_RES", out var resolution) ? resolution.ToString() : "N/A",
                    Accuracy = attrs.TryGetProperty("SRC_ACC", out var accuracy) ? accuracy.ToString() : "N/A",
                    ZoomLevel = zoomLevel,
                    ReleaseTitle = releaseTitle
                };

                return metadata;
            }
            catch
            {
                return null;
            }
        }
    }
}

