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
using XIAOFUTools.Tools.Settings;

namespace XIAOFUTools.Tools.ViewArea
{
    /// <summary>
    /// 坐标系类型枚举
    /// </summary>
    public enum CoordinateSystemType
    {
        None,        // 无坐标系
        Geographic,  // 地理坐标系
        Projected    // 投影坐标系
    }

    /// <summary>
    /// 计算结果数据模型
    /// </summary>
    public class CalculationResult : PropertyChangedBase
    {
        public string CalculationType { get; set; }
        public string Unit1Value { get; set; }
        public string Unit2Value { get; set; }
        public string Unit3Value { get; set; }
        public string Unit4Value { get; set; }
    }

    /// <summary>
    /// 图层计算汇总数据模型
    /// </summary>
    public class LayerCalculationSummary : PropertyChangedBase
    {
        public string LayerName { get; set; }
        public int FeatureCount { get; set; }

        private ObservableCollection<CalculationResult> _areaResults;
        public ObservableCollection<CalculationResult> AreaResults
        {
            get => _areaResults;
            set
            {
                SetProperty(ref _areaResults, value);
                NotifyPropertyChanged(() => HasAreaResults);
            }
        }

        private ObservableCollection<CalculationResult> _lengthResults;
        public ObservableCollection<CalculationResult> LengthResults
        {
            get => _lengthResults;
            set
            {
                SetProperty(ref _lengthResults, value);
                NotifyPropertyChanged(() => HasLengthResults);
            }
        }

        public bool HasAreaResults => AreaResults != null && AreaResults.Count > 0;
        public bool HasLengthResults => LengthResults != null && LengthResults.Count > 0;

        public LayerCalculationSummary()
        {
            AreaResults = new ObservableCollection<CalculationResult>();
            LengthResults = new ObservableCollection<CalculationResult>();
        }

        /// <summary>
        /// 通知HasAreaResults和HasLengthResults属性变化
        /// </summary>
        public void RefreshHasResultsProperties()
        {
            NotifyPropertyChanged(() => HasAreaResults);
            NotifyPropertyChanged(() => HasLengthResults);
        }
    }

    /// <summary>
    /// 查看面积DockPane视图模型
    /// </summary>
    internal class ViewAreaDockPaneViewModel : PropertyChangedBase
    {
        private dynamic _mapSelectionChangedToken;

        #region 属性

        // 选择状态信息
        private string _selectionInfo = "未选择任何要素";
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

        // 合并计算结果
        private ObservableCollection<CalculationResult> _combinedAreaResults;
        public ObservableCollection<CalculationResult> CombinedAreaResults
        {
            get => _combinedAreaResults;
            set => SetProperty(ref _combinedAreaResults, value);
        }

        private ObservableCollection<CalculationResult> _combinedLengthResults;
        public ObservableCollection<CalculationResult> CombinedLengthResults
        {
            get => _combinedLengthResults;
            set => SetProperty(ref _combinedLengthResults, value);
        }

        // 分图层计算结果
        private ObservableCollection<LayerCalculationSummary> _layerResults;
        public ObservableCollection<LayerCalculationSummary> LayerResults
        {
            get => _layerResults;
            set => SetProperty(ref _layerResults, value);
        }

        // 是否显示分图层结果
        private bool _showLayerResults = false;
        public bool ShowLayerResults
        {
            get => _showLayerResults;
            set => SetProperty(ref _showLayerResults, value);
        }

        // 是否正在计算
        private bool _isCalculating = false;
        public bool IsCalculating
        {
            get => _isCalculating;
            set => SetProperty(ref _isCalculating, value);
        }

        // 是否有面积结果
        private bool _hasAreaResults = false;
        public bool HasAreaResults
        {
            get => _hasAreaResults;
            set => SetProperty(ref _hasAreaResults, value);
        }

        // 是否有长度结果
        private bool _hasLengthResults = false;
        public bool HasLengthResults
        {
            get => _hasLengthResults;
            set => SetProperty(ref _hasLengthResults, value);
        }

        // 是否有任何结果
        public bool HasResults => HasAreaResults || HasLengthResults;

        #endregion

        #region 命令

        public ICommand RefreshCommand { get; }
        public ICommand CopyResultsCommand { get; }

        #endregion

        /// <summary>
        /// 构造函数
        /// </summary>
        public ViewAreaDockPaneViewModel()
        {
            // 初始化集合
            CombinedAreaResults = new ObservableCollection<CalculationResult>();
            CombinedLengthResults = new ObservableCollection<CalculationResult>();
            LayerResults = new ObservableCollection<LayerCalculationSummary>();

            // 初始化命令
            RefreshCommand = new RelayCommand(RefreshCalculation);
            CopyResultsCommand = new RelayCommand(CopyResults);

            // 订阅地图选择变化事件
            _mapSelectionChangedToken = MapSelectionChangedEvent.Subscribe(OnMapSelectionChanged);

            // 初始计算
            RefreshCalculation();
        }

        /// <summary>
        /// 清理事件订阅
        /// </summary>
        public void Cleanup()
        {
            if (_mapSelectionChangedToken != null)
            {
                MapSelectionChangedEvent.Unsubscribe(_mapSelectionChangedToken);
                _mapSelectionChangedToken = null;
            }
        }

        /// <summary>
        /// 地图选择变化事件处理
        /// </summary>
        private async void OnMapSelectionChanged(MapSelectionChangedEventArgs args)
        {
            try
            {
                // 检查是否启用了自动关闭功能
                if (SettingsManager.Settings.ViewArea.AutoCloseOnClearSelection)
                {
                    // 在MCT线程上检查是否有选择的要素
                    bool hasSelection = await QueuedTask.Run(() => HasAnySelectedFeatures());
                    if (!hasSelection)
                    {
                        // 如果窗口可见，则关闭它
                        if (ViewAreaDockPane.IsVisible())
                        {
                            ViewAreaDockPane.Close();
                            return; // 不需要刷新计算，因为窗口已关闭
                        }
                    }
                }

                RefreshCalculation();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"处理选择变化事件时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查是否有任何选择的要素（必须在MCT线程上调用）
        /// </summary>
        private bool HasAnySelectedFeatures()
        {
            try
            {
                var mapView = MapView.Active;
                if (mapView?.Map == null) return false;

                var selection = mapView.Map.GetSelection();
                if (selection == null) return false;

                // 检查是否有选择的要素图层
                foreach (var kvp in selection.ToDictionary())
                {
                    var layer = kvp.Key as FeatureLayer;
                    if (layer != null && kvp.Value != null && kvp.Value.Count > 0)
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"检查选择要素时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 刷新计算
        /// </summary>
        private async void RefreshCalculation()
        {
            IsCalculating = true;
            try
            {
                await QueuedTask.Run(() =>
                {
                    try
                    {
                        CalculateSelectedFeatures();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"计算选中要素时出错: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"错误: {ex.Message}");
            }
            finally
            {
                IsCalculating = false;
            }
        }

        /// <summary>
        /// 复制结果到剪贴板（带重试机制）
        /// </summary>
        private async void CopyResults()
        {
            try
            {
                var result = new System.Text.StringBuilder();

                // 添加标题
                result.AppendLine("面积/长度计算结果");
                result.AppendLine("=" + new string('=', 30));
                result.AppendLine();

                // 添加选择信息
                result.AppendLine(SelectionInfo);
                result.AppendLine();

                // 合并计算结果
                if (HasResults)
                {
                    result.AppendLine("合并计算结果:");
                    result.AppendLine("-" + new string('-', 20));

                    // 面积结果
                    if (HasAreaResults)
                    {
                        result.AppendLine("面积:");
                        foreach (var areaResult in CombinedAreaResults)
                        {
                            result.AppendLine($"  {areaResult.CalculationType}:");
                            result.AppendLine($"    平方米: {areaResult.Unit1Value}");
                            result.AppendLine($"    公顷: {areaResult.Unit2Value}");
                            result.AppendLine($"    亩: {areaResult.Unit3Value}");
                            result.AppendLine($"    平方公里: {areaResult.Unit4Value}");
                        }
                        result.AppendLine();
                    }

                    // 长度结果
                    if (HasLengthResults)
                    {
                        result.AppendLine("长度:");
                        foreach (var lengthResult in CombinedLengthResults)
                        {
                            result.AppendLine($"  {lengthResult.CalculationType}:");
                            result.AppendLine($"    米: {lengthResult.Unit1Value}");
                            result.AppendLine($"    千米: {lengthResult.Unit2Value}");
                        }
                        result.AppendLine();
                    }

                    // 分图层结果
                    if (ShowLayerResults)
                    {
                        result.AppendLine("分图层计算结果:");
                        result.AppendLine("-" + new string('-', 20));

                        foreach (var layerResult in LayerResults)
                        {
                            result.AppendLine($"{layerResult.LayerName} ({layerResult.FeatureCount} 个要素):");

                            if (layerResult.HasAreaResults)
                            {
                                result.AppendLine("  面积:");
                                foreach (var areaResult in layerResult.AreaResults)
                                {
                                    result.AppendLine($"    {areaResult.CalculationType}: {areaResult.Unit1Value}㎡, {areaResult.Unit2Value}公顷, {areaResult.Unit3Value}亩, {areaResult.Unit4Value}k㎡");
                                }
                            }

                            if (layerResult.HasLengthResults)
                            {
                                result.AppendLine("  长度:");
                                foreach (var lengthResult in layerResult.LengthResults)
                                {
                                    result.AppendLine($"    {lengthResult.CalculationType}: {lengthResult.Unit1Value}米, {lengthResult.Unit2Value}千米");
                                }
                            }
                            result.AppendLine();
                        }
                    }
                }
                else
                {
                    result.AppendLine("无计算结果");
                }

                // 添加说明
                result.AppendLine();
                result.AppendLine("说明:");
                result.AppendLine("椭球面积和测地线长度使用ArcGIS Pro内置高精度算法计算");
                result.AppendLine("计算策略：无坐标系-平面计算；地理坐标系-椭球计算；投影坐标系-两种都计算");

                // 使用重试机制复制到剪贴板
                var success = await CopyToClipboardWithRetry(result.ToString());

                if (success)
                {
                    // 显示成功消息
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("计算结果已复制到剪贴板", "复制成功",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                else
                {
                    // 复制失败，提供备用方案
                    ShowCopyFailureDialog(result.ToString());
                }
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"复制失败: {ex.Message}", "错误",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 带重试机制的剪贴板复制
        /// </summary>
        private async Task<bool> CopyToClipboardWithRetry(string text, int maxRetries = 3)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    // 在UI线程中执行剪贴板操作
                    if (System.Windows.Application.Current?.Dispatcher != null)
                    {
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            System.Windows.Clipboard.Clear();
                            System.Threading.Thread.Sleep(50); // 短暂等待
                            System.Windows.Clipboard.SetText(text);
                        });
                    }
                    else
                    {
                        System.Windows.Clipboard.Clear();
                        System.Threading.Thread.Sleep(50);
                        System.Windows.Clipboard.SetText(text);
                    }

                    return true; // 成功
                }
                catch (System.Runtime.InteropServices.COMException ex) when (ex.HResult == unchecked((int)0x800401D0))
                {
                    // CLIPBRD_E_CANT_OPEN 错误，剪贴板被占用
                    System.Diagnostics.Debug.WriteLine($"剪贴板被占用，第 {i + 1} 次重试...");

                    if (i < maxRetries - 1)
                    {
                        // 等待一段时间后重试
                        await Task.Delay(200 + i * 100); // 递增等待时间
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"剪贴板操作失败: {ex.Message}");
                    break; // 其他错误不重试
                }
            }

            return false; // 失败
        }

        /// <summary>
        /// 显示复制失败对话框，提供备用方案
        /// </summary>
        private void ShowCopyFailureDialog(string text)
        {
            var message = "剪贴板当前被其他应用程序占用，无法复制。\n\n" +
                         "您可以：\n" +
                         "1. 稍后再试点击\"复制结果\"按钮\n" +
                         "2. 双击表格中的数值直接复制单个数据\n" +
                         "3. 手动记录需要的数值\n\n" +
                         "建议关闭其他可能占用剪贴板的程序（如远程桌面、截图工具等）后重试。";

            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(message, "复制失败",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }

        /// <summary>
        /// 计算选中要素
        /// </summary>
        private void CalculateSelectedFeatures()
        {
            var mapView = MapView.Active;
            if (mapView?.Map == null)
            {
                UpdateSelectionInfo("无活动地图");
                ClearResults();
                return;
            }

            // 获取所有有选择的要素图层（包括组图层中的图层）
            var selectedLayers = GetAllFeatureLayers(mapView.Map.Layers).Where(layer => layer.SelectionCount > 0).ToList();
            if (!selectedLayers.Any())
            {
                UpdateSelectionInfo("未选择任何要素");
                ClearResults();
                return;
            }

            // 统计选择信息和检测坐标系类型
            int totalFeatures = 0;
            int layerCount = 0;
            var layerData = new Dictionary<string, List<Feature>>();
            var coordinateSystemTypes = new List<CoordinateSystemType>();
            var layerSpatialReferences = new Dictionary<string, SpatialReference>();

            foreach (var layer in selectedLayers)
            {
                layerCount++;
                var features = new List<Feature>();

                // 检测图层坐标系类型
                var layerSpatialRef = layer.GetSpatialReference();
                System.Diagnostics.Debug.WriteLine($"图层 '{layer.Name}' 的坐标系检测:");
                var layerCoordType = GetCoordinateSystemType(layerSpatialRef);
                System.Diagnostics.Debug.WriteLine($"图层 '{layer.Name}' 坐标系类型: {layerCoordType}");
                coordinateSystemTypes.Add(layerCoordType);
                layerSpatialReferences[layer.Name] = layerSpatialRef;

                // 获取图层的选择
                var selection = layer.GetSelection();
                using (var cursor = selection.Search(null, false))
                {
                    while (cursor.MoveNext())
                    {
                        var feature = cursor.Current as Feature;
                        if (feature != null)
                        {
                            features.Add(feature);
                            totalFeatures++;
                        }
                    }
                }

                if (features.Any())
                {
                    layerData[layer.Name] = features;
                }
            }

            // 确定使用的坐标系类型策略
            var coordinateSystemType = DetermineCoordinateSystemStrategy(coordinateSystemTypes);

            // 构建详细的选择信息
            var selectionDetails = new List<string>();
            foreach (var kvp in layerData)
            {
                selectionDetails.Add($"{kvp.Key}: {kvp.Value.Count}个要素");
            }

            var detailInfo = string.Join(", ", selectionDetails);
            var coordTypeInfo = GetCoordinateSystemTypeDescription(coordinateSystemType);
            UpdateSelectionInfo($"选择了{totalFeatures}个图形（{layerCount}个图层）- {detailInfo} - {coordTypeInfo}");

            // 计算结果
            CalculateResults(layerData, coordinateSystemType, layerSpatialReferences);
        }

        /// <summary>
        /// 更新选择信息
        /// </summary>
        private void UpdateSelectionInfo(string info)
        {
            if (System.Windows.Application.Current?.Dispatcher != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    SelectionInfo = info;
                });
            }
        }

        /// <summary>
        /// 清空结果
        /// </summary>
        private void ClearResults()
        {
            if (System.Windows.Application.Current?.Dispatcher != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    CombinedAreaResults.Clear();
                    CombinedLengthResults.Clear();
                    LayerResults.Clear();
                    HasAreaResults = false;
                    HasLengthResults = false;
                    ShowLayerResults = false;
                    NotifyPropertyChanged(() => HasResults);
                });
            }
        }

        /// <summary>
        /// 计算结果（所有ArcGIS API调用在QueuedTask/MCT线程内执行）
        /// </summary>
        private void CalculateResults(Dictionary<string, List<Feature>> layerData, CoordinateSystemType coordinateSystemType, Dictionary<string, SpatialReference> layerSpatialReferences)
        {
            if (!layerData.Any())
            {
                ClearResults();
                return;
            }

            try
            {
                var allPolygons = new List<Polygon>();
                var allPolylines = new List<Polyline>();
                var layerSummaries = new List<LayerCalculationSummary>();

                // 在MCT线程上顺序处理每个图层
                foreach (var kvp in layerData)
                {
                    var layerName = kvp.Key;
                    var features = kvp.Value;

                    var layerSummary = new LayerCalculationSummary
                    {
                        LayerName = layerName,
                        FeatureCount = features.Count
                    };

                    var layerPolygons = new List<Polygon>();
                    var layerPolylines = new List<Polyline>();

                    foreach (var feature in features)
                    {
                        var geometry = feature.GetShape();
                        if (geometry != null)
                        {
                            // 获取图层的坐标系
                            var layerSpatialRef = layerSpatialReferences.ContainsKey(layerName) ? layerSpatialReferences[layerName] : null;

                            // 如果图层有坐标系，确保几何图形使用正确的坐标系
                            if (layerSpatialRef != null && geometry.SpatialReference == null)
                            {
                                geometry = GeometryEngine.Instance.Project(geometry, layerSpatialRef);
                            }

                            if (geometry is Polygon polygon)
                            {
                                allPolygons.Add(polygon);
                                layerPolygons.Add(polygon);
                            }
                            else if (geometry is Polyline polyline)
                            {
                                allPolylines.Add(polyline);
                                layerPolylines.Add(polyline);
                            }
                        }
                    }

                    CalculateLayerResults(layerSummary, layerPolygons, layerPolylines, coordinateSystemType);
                    layerSummaries.Add(layerSummary);
                }

                var combinedAreaResults = CalculateAreaResults(allPolygons, coordinateSystemType);
                var combinedLengthResults = CalculateLengthResults(allPolylines, coordinateSystemType);

                // 更新UI（在UI线程中）
                if (System.Windows.Application.Current?.Dispatcher != null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        CombinedAreaResults.Clear();
                        CombinedLengthResults.Clear();
                        LayerResults.Clear();

                        foreach (var result in combinedAreaResults)
                            CombinedAreaResults.Add(result);

                        foreach (var result in combinedLengthResults)
                            CombinedLengthResults.Add(result);

                        foreach (var summary in layerSummaries)
                            LayerResults.Add(summary);

                        HasAreaResults = combinedAreaResults.Any();
                        HasLengthResults = combinedLengthResults.Any();
                        ShowLayerResults = layerData.Count > 1;
                        NotifyPropertyChanged(() => HasResults);
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"计算时出错: {ex.Message}");
            }
        }





        /// <summary>
        /// 计算图层结果
        /// </summary>
        private void CalculateLayerResults(LayerCalculationSummary layerSummary, List<Polygon> polygons, List<Polyline> polylines, CoordinateSystemType coordinateSystemType)
        {
            var areaResults = CalculateAreaResults(polygons, coordinateSystemType);
            var lengthResults = CalculateLengthResults(polylines, coordinateSystemType);

            layerSummary.AreaResults.Clear();
            layerSummary.LengthResults.Clear();

            foreach (var result in areaResults)
                layerSummary.AreaResults.Add(result);

            foreach (var result in lengthResults)
                layerSummary.LengthResults.Add(result);

            // 通知属性变化
            layerSummary.RefreshHasResultsProperties();
        }

        /// <summary>
        /// 计算面积结果（并行优化，根据坐标系类型决定计算策略）
        /// </summary>
        private List<CalculationResult> CalculateAreaResults(List<Polygon> polygons, CoordinateSystemType coordinateSystemType)
        {
            var results = new List<CalculationResult>();

            if (!polygons.Any())
                return results;

            try
            {
                double planarArea = 0;
                double geodesicArea = 0;

                // 根据坐标系类型决定计算策略
                System.Diagnostics.Debug.WriteLine($"面要素面积计算 - 坐标系类型: {coordinateSystemType}, 面要素数量: {polygons.Count}");
                switch (coordinateSystemType)
                {
                    case CoordinateSystemType.None:
                        // 无坐标系：只计算平面面积
                        planarArea = polygons.Sum(p => GeometryEngine.Instance.Area(p));
                        System.Diagnostics.Debug.WriteLine($"无坐标系 - 只计算平面面积: {planarArea}");
                        break;

                    case CoordinateSystemType.Geographic:
                        // 地理坐标系：只计算椭球面积（无法计算平面）
                        geodesicArea = polygons.Sum(p => CalculateGeodesicArea(p));
                        System.Diagnostics.Debug.WriteLine($"地理坐标系 - 只计算椭球面积: {geodesicArea}");
                        break;

                    case CoordinateSystemType.Projected:
                        // 投影坐标系：计算两种面积
                        planarArea = polygons.Sum(p => GeometryEngine.Instance.Area(p));
                        geodesicArea = polygons.Sum(p => CalculateGeodesicArea(p));
                        System.Diagnostics.Debug.WriteLine($"投影坐标系 - 平面面积: {planarArea}, 椭球面积: {geodesicArea}");
                        break;
                }

                // 平面面积结果（根据坐标系类型决定是否显示）
                results.Add(new CalculationResult
                {
                    CalculationType = "平面面积",
                    Unit1Value = planarArea > 0 ? FormatValue(planarArea, "平方米") : "",
                    Unit2Value = planarArea > 0 ? FormatValue(planarArea / 10000, "公顷") : "",
                    Unit3Value = planarArea > 0 ? FormatValue(planarArea / 666.67, "亩") : "",
                    Unit4Value = planarArea > 0 ? FormatValue(planarArea / 1000000, "平方公里") : ""
                });

                // 椭球面积结果（根据坐标系类型决定是否显示）
                results.Add(new CalculationResult
                {
                    CalculationType = "椭球面积",
                    Unit1Value = geodesicArea > 0 ? FormatValue(geodesicArea, "平方米") : "",
                    Unit2Value = geodesicArea > 0 ? FormatValue(geodesicArea / 10000, "公顷") : "",
                    Unit3Value = geodesicArea > 0 ? FormatValue(geodesicArea / 666.67, "亩") : "",
                    Unit4Value = geodesicArea > 0 ? FormatValue(geodesicArea / 1000000, "平方公里") : ""
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"并行面积计算失败，使用单线程: {ex.Message}");

                // 回退到单线程计算
                double planarArea = 0;
                double geodesicArea = 0;

                // 根据坐标系类型决定计算策略
                switch (coordinateSystemType)
                {
                    case CoordinateSystemType.None:
                        // 无坐标系：只计算平面面积
                        planarArea = polygons.Sum(p => GeometryEngine.Instance.Area(p));
                        break;

                    case CoordinateSystemType.Geographic:
                        // 地理坐标系：只计算椭球面积（无法计算平面）
                        geodesicArea = polygons.Sum(p => CalculateGeodesicArea(p));
                        break;

                    case CoordinateSystemType.Projected:
                        // 投影坐标系：计算两种面积
                        planarArea = polygons.Sum(p => GeometryEngine.Instance.Area(p));
                        geodesicArea = polygons.Sum(p => CalculateGeodesicArea(p));
                        break;
                }

                results.Add(new CalculationResult
                {
                    CalculationType = "平面面积",
                    Unit1Value = planarArea > 0 ? FormatValue(planarArea, "平方米") : "",
                    Unit2Value = planarArea > 0 ? FormatValue(planarArea / 10000, "公顷") : "",
                    Unit3Value = planarArea > 0 ? FormatValue(planarArea / 666.67, "亩") : "",
                    Unit4Value = planarArea > 0 ? FormatValue(planarArea / 1000000, "平方公里") : ""
                });

                results.Add(new CalculationResult
                {
                    CalculationType = "椭球面积",
                    Unit1Value = geodesicArea > 0 ? FormatValue(geodesicArea, "平方米") : "",
                    Unit2Value = geodesicArea > 0 ? FormatValue(geodesicArea / 10000, "公顷") : "",
                    Unit3Value = geodesicArea > 0 ? FormatValue(geodesicArea / 666.67, "亩") : "",
                    Unit4Value = geodesicArea > 0 ? FormatValue(geodesicArea / 1000000, "平方公里") : ""
                });
            }

            return results;
        }

        /// <summary>
        /// 计算长度结果（并行优化，根据坐标系类型决定计算策略）
        /// </summary>
        private List<CalculationResult> CalculateLengthResults(List<Polyline> polylines, CoordinateSystemType coordinateSystemType)
        {
            var results = new List<CalculationResult>();

            if (!polylines.Any())
                return results;

            try
            {
                double planarLength = 0;
                double geodesicLength = 0;

                // 根据坐标系类型决定计算策略
                System.Diagnostics.Debug.WriteLine($"线要素长度计算 - 坐标系类型: {coordinateSystemType}, 线要素数量: {polylines.Count}");
                switch (coordinateSystemType)
                {
                    case CoordinateSystemType.None:
                        // 无坐标系：只计算平面长度
                        planarLength = polylines.Sum(p => GeometryEngine.Instance.Length(p));
                        System.Diagnostics.Debug.WriteLine($"无坐标系 - 只计算平面长度: {planarLength}");
                        break;

                    case CoordinateSystemType.Geographic:
                        // 地理坐标系：只计算测地线长度（无法计算平面）
                        geodesicLength = polylines.Sum(p => CalculateGeodesicLength(p));
                        System.Diagnostics.Debug.WriteLine($"地理坐标系 - 只计算测地线长度: {geodesicLength}");
                        break;

                    case CoordinateSystemType.Projected:
                        // 投影坐标系：计算两种长度
                        planarLength = polylines.Sum(p => GeometryEngine.Instance.Length(p));
                        geodesicLength = polylines.Sum(p => CalculateGeodesicLength(p));
                        System.Diagnostics.Debug.WriteLine($"投影坐标系 - 平面长度: {planarLength}, 测地线长度: {geodesicLength}");
                        break;
                }

                // 平面长度结果（根据坐标系类型决定是否显示）
                results.Add(new CalculationResult
                {
                    CalculationType = "平面长度",
                    Unit1Value = planarLength > 0 ? FormatValue(planarLength, "米") : "",
                    Unit2Value = planarLength > 0 ? FormatValue(planarLength / 1000, "千米") : "",
                    Unit3Value = "",
                    Unit4Value = ""
                });

                // 测地线长度结果（根据坐标系类型决定是否显示）
                results.Add(new CalculationResult
                {
                    CalculationType = "测地线",
                    Unit1Value = geodesicLength > 0 ? FormatValue(geodesicLength, "米") : "",
                    Unit2Value = geodesicLength > 0 ? FormatValue(geodesicLength / 1000, "千米") : "",
                    Unit3Value = "",
                    Unit4Value = ""
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"并行长度计算失败，使用单线程: {ex.Message}");

                // 回退到单线程计算
                double planarLength = 0;
                double geodesicLength = 0;

                // 根据坐标系类型决定计算策略
                switch (coordinateSystemType)
                {
                    case CoordinateSystemType.None:
                        // 无坐标系：只计算平面长度
                        planarLength = polylines.Sum(p => GeometryEngine.Instance.Length(p));
                        break;

                    case CoordinateSystemType.Geographic:
                        // 地理坐标系：只计算测地线长度（无法计算平面）
                        geodesicLength = polylines.Sum(p => CalculateGeodesicLength(p));
                        break;

                    case CoordinateSystemType.Projected:
                        // 投影坐标系：计算两种长度
                        planarLength = polylines.Sum(p => GeometryEngine.Instance.Length(p));
                        geodesicLength = polylines.Sum(p => CalculateGeodesicLength(p));
                        break;
                }

                results.Add(new CalculationResult
                {
                    CalculationType = "平面长度",
                    Unit1Value = planarLength > 0 ? FormatValue(planarLength, "米") : "",
                    Unit2Value = planarLength > 0 ? FormatValue(planarLength / 1000, "千米") : "",
                    Unit3Value = "",
                    Unit4Value = ""
                });

                results.Add(new CalculationResult
                {
                    CalculationType = "测地线",
                    Unit1Value = geodesicLength > 0 ? FormatValue(geodesicLength, "米") : "",
                    Unit2Value = geodesicLength > 0 ? FormatValue(geodesicLength / 1000, "千米") : "",
                    Unit3Value = "",
                    Unit4Value = ""
                });
            }

            return results;
        }

        /// <summary>
        /// 计算椭球面积（使用ArcGIS Pro内置高精度方法）
        /// </summary>
        private double CalculateGeodesicArea(Polygon polygon)
        {
            try
            {
                // 使用ArcGIS Pro内置的高精度椭球面积计算
                // 这个方法内部已经处理了大地测量的复杂计算
                var geodesicArea = GeometryEngine.Instance.GeodesicArea(polygon);

                System.Diagnostics.Debug.WriteLine($"椭球面积计算 - 原始结果: {geodesicArea}");

                // 检查结果是否合理
                if (double.IsNaN(geodesicArea) || double.IsInfinity(geodesicArea) || geodesicArea < 0)
                {
                    System.Diagnostics.Debug.WriteLine("椭球面积计算结果异常，尝试备用方法");

                    // 备用方法：使用平面面积作为参考
                    var planarArea = GeometryEngine.Instance.Area(polygon);
                    System.Diagnostics.Debug.WriteLine($"备用平面面积: {planarArea}");

                    // 如果平面面积合理，返回平面面积
                    if (!double.IsNaN(planarArea) && !double.IsInfinity(planarArea) && planarArea > 0)
                    {
                        return planarArea;
                    }

                    return 0;
                }

                return Math.Abs(geodesicArea); // 确保返回正值
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"椭球面积计算失败: {ex.Message}");

                try
                {
                    // 备用方法：使用平面面积
                    var planarArea = GeometryEngine.Instance.Area(polygon);
                    System.Diagnostics.Debug.WriteLine($"使用备用平面面积: {planarArea}");
                    return Math.Abs(planarArea);
                }
                catch (Exception ex2)
                {
                    System.Diagnostics.Debug.WriteLine($"备用面积计算也失败: {ex2.Message}");
                    return 0;
                }
            }
        }

        /// <summary>
        /// 计算测地线长度（使用ArcGIS Pro内置高精度方法）
        /// </summary>
        private double CalculateGeodesicLength(Polyline polyline)
        {
            try
            {
                // 使用ArcGIS Pro内置的高精度测地线长度计算
                var geodesicLength = GeometryEngine.Instance.GeodesicLength(polyline);

                System.Diagnostics.Debug.WriteLine($"测地线长度计算 - 原始结果: {geodesicLength}");

                // 检查结果是否合理
                if (double.IsNaN(geodesicLength) || double.IsInfinity(geodesicLength) || geodesicLength < 0)
                {
                    System.Diagnostics.Debug.WriteLine("测地线长度计算结果异常，尝试备用方法");

                    // 备用方法：使用平面长度作为参考
                    var planarLength = GeometryEngine.Instance.Length(polyline);
                    System.Diagnostics.Debug.WriteLine($"备用平面长度: {planarLength}");

                    // 如果平面长度合理，返回平面长度
                    if (!double.IsNaN(planarLength) && !double.IsInfinity(planarLength) && planarLength > 0)
                    {
                        return planarLength;
                    }

                    return 0;
                }

                return Math.Abs(geodesicLength); // 确保返回正值
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"测地线长度计算失败: {ex.Message}");

                try
                {
                    // 备用方法：使用平面长度
                    var planarLength = GeometryEngine.Instance.Length(polyline);
                    System.Diagnostics.Debug.WriteLine($"使用备用平面长度: {planarLength}");
                    return Math.Abs(planarLength);
                }
                catch (Exception ex2)
                {
                    System.Diagnostics.Debug.WriteLine($"备用长度计算也失败: {ex2.Message}");
                    return 0;
                }
            }
        }





        /// <summary>
        /// 格式化数值
        /// </summary>
        private string FormatValue(double value, string unit)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return "0";

            return Math.Round(value, DecimalPlaces).ToString($"F{DecimalPlaces}");
        }



        #region 图层获取方法

        /// <summary>
        /// 递归获取所有要素图层（包括组图层中的图层）
        /// </summary>
        private IEnumerable<FeatureLayer> GetAllFeatureLayers(IList<Layer> layers)
        {
            foreach (var layer in layers)
            {
                if (layer is GroupLayer groupLayer)
                {
                    // 递归遍历组图层
                    foreach (var childLayer in GetAllFeatureLayers(groupLayer.Layers))
                    {
                        yield return childLayer;
                    }
                }
                else if (layer is FeatureLayer featureLayer)
                {
                    yield return featureLayer;
                }
            }
        }

        /// <summary>
        /// 获取图层的坐标系
        /// </summary>
        private SpatialReference GetLayerSpatialReference(string layerName)
        {
            try
            {
                var mapView = MapView.Active;
                if (mapView?.Map == null) return null;

                var layer = GetAllFeatureLayers(mapView.Map.Layers).FirstOrDefault(l => l.Name == layerName);
                return layer?.GetSpatialReference();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取图层坐标系时出错: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region 坐标系检测方法

        /// <summary>
        /// 获取坐标系类型
        /// </summary>
        private CoordinateSystemType GetCoordinateSystemType(SpatialReference spatialRef)
        {
            try
            {
                if (spatialRef == null)
                {
                    System.Diagnostics.Debug.WriteLine("坐标系为null");
                    return CoordinateSystemType.None;
                }

                if (string.IsNullOrEmpty(spatialRef.Name))
                {
                    System.Diagnostics.Debug.WriteLine("坐标系名称为空");
                    return CoordinateSystemType.None;
                }

                System.Diagnostics.Debug.WriteLine($"坐标系名称: {spatialRef.Name}");
                System.Diagnostics.Debug.WriteLine($"IsGeographic: {spatialRef.IsGeographic}");
                System.Diagnostics.Debug.WriteLine($"IsProjected: {spatialRef.IsProjected}");

                if (spatialRef.IsGeographic)
                    return CoordinateSystemType.Geographic;

                if (spatialRef.IsProjected)
                    return CoordinateSystemType.Projected;

                return CoordinateSystemType.None;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"检测坐标系类型时出错: {ex.Message}");
                return CoordinateSystemType.None;
            }
        }

        /// <summary>
        /// 确定坐标系策略
        /// </summary>
        private CoordinateSystemType DetermineCoordinateSystemStrategy(List<CoordinateSystemType> coordinateSystemTypes)
        {
            if (!coordinateSystemTypes.Any())
                return CoordinateSystemType.None;

            // 如果所有图层坐标系类型一致，使用该类型
            var firstType = coordinateSystemTypes.First();
            if (coordinateSystemTypes.All(t => t == firstType))
                return firstType;

            // 如果坐标系类型不一致，优先级：投影坐标系 > 地理坐标系 > 无坐标系
            if (coordinateSystemTypes.Any(t => t == CoordinateSystemType.Projected))
                return CoordinateSystemType.Projected;

            if (coordinateSystemTypes.Any(t => t == CoordinateSystemType.Geographic))
                return CoordinateSystemType.Geographic;

            return CoordinateSystemType.None;
        }

        /// <summary>
        /// 获取坐标系类型描述
        /// </summary>
        private string GetCoordinateSystemTypeDescription(CoordinateSystemType coordinateSystemType)
        {
            return coordinateSystemType switch
            {
                CoordinateSystemType.None => "无坐标系",
                CoordinateSystemType.Geographic => "地理坐标系",
                CoordinateSystemType.Projected => "投影坐标系",
                _ => "未知坐标系"
            };
        }

        #endregion
    }
}
