using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using XIAOFUTools.Shared;
using XIAOFUTools.Features.User.Settings;
using XIAOFUTools.Features.Analysis.BrowseFeatures.Core;

namespace XIAOFUTools.Features.Analysis.BrowseFeatures
{
    internal sealed partial class BrowseFeaturesDockPaneViewModel
    {
        public async Task RefreshLayersAsync()
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            try
            {
                var layers = await QueuedTask.Run(() =>
                {
                    var map = MapView.Active?.Map;
                    if (map == null)
                    {
                        return new List<FeatureLayer>();
                    }

                    return map.GetLayersAsFlattenedList()
                        .OfType<FeatureLayer>()
                        .Where(IsSupportedLayer)
                        .OrderBy(layer => layer.Name, StringComparer.CurrentCultureIgnoreCase)
                        .ToList();
                });

                var previousLayerUri = SelectedLayer?.URI;
                _featureLayers.Clear();
                foreach (var layer in layers)
                {
                    _featureLayers.Add(layer);
                }

                SelectedLayer = _featureLayers.FirstOrDefault(layer => string.Equals(layer.URI, previousLayerUri, StringComparison.Ordinal)) ??
                                _featureLayers.FirstOrDefault();

                await AutoResolveReviewStorageAsync();

                if (SelectedLayer == null)
                {
                    ClearSnapshotState("未找到可用要素图层。");
                }
            }
            catch (Exception ex)
            {
                LogError($"刷新图层失败: {ex.Message}");
                ClearSnapshotState("刷新图层失败。");
            }
            finally
            {
                IsBusy = false;
            }
        }


        private void OpenSettings()
        {
            if (_settingsWindowService.Show(SelectedLayer))
            {
                LoadSettings();
                ResetReviewTableState();
                NotifyPropertyChanged(() => ReviewerName);
                StatusMessage = "浏览要素设置已更新。";
            }
        }


        public async Task RefreshSnapshotAsync()
        {
            if (IsBusy)
            {
                return;
            }

            if (SelectedLayer == null)
            {
                PresentationServices.Dialogs.Show("请先选择目标要素图层。", "提示");
                return;
            }

            if (IsFieldSortMode && SelectedSortField == null)
            {
                PresentationServices.Dialogs.Show("请选择排序字段。", "提示");
                return;
            }

            IsBusy = true;
            try
            {
                if (!await EnsureReviewTableReadyAsync())
                {
                    return;
                }

                var snapshotResult = await BuildSnapshotAsync();
                var snapshot = snapshotResult.Items;
                _snapshotItems.Clear();
                _snapshotItems.AddRange(snapshot);
                RebuildSnapshotList();
                _currentIndex = -1;
                _currentFeature = null;
                _currentPartIndex = 0;
                _currentFeatureVisitedAt = DateTime.MinValue;

                if (_snapshotItems.Count == 0)
                {
                    ClearSnapshotState("快照为空。");
                    LogInfo("未生成可浏览要素（可能为选择集为空或图层无要素）。");
                    return;
                }

                if (snapshotResult.UsedFallbackSort)
                {
                    LogInfo("排序字段不可用，已回退到 OID 升序。");
                }

                IsSnapshotStale = false;
                StaleHint = string.Empty;
                StatusMessage = $"已加载 {_snapshotItems.Count} 条要素。";
                var resumeObjectId = await TryGetResumeObjectIdAsync();
                var resumeIndex = resumeObjectId.HasValue
                    ? _snapshotItems.FindIndex(item => item.ObjectId == resumeObjectId.Value)
                    : -1;
                await NavigateToIndexAsync(resumeIndex >= 0 ? resumeIndex : 0, persistCurrentBeforeNavigate: false);
            }
            catch (Exception ex)
            {
                LogError($"刷新快照失败: {ex.Message}");
                ClearSnapshotState("刷新快照失败。");
            }
            finally
            {
                IsBusy = false;
                RaiseCommandStates();
            }
        }


        public bool HandleDirectionKey(System.Windows.Input.Key key)
        {
            if (IsBusy || _snapshotItems.Count == 0)
            {
                return false;
            }

            if (key == System.Windows.Input.Key.Left || key == System.Windows.Input.Key.Up)
            {
                if (CanGoPrevious())
                {
                    _ = NavigateToIndexAsync(_currentIndex - 1);
                    return true;
                }
            }
            else if (key == System.Windows.Input.Key.Right || key == System.Windows.Input.Key.Down)
            {
                if (CanGoNext())
                {
                    _ = NavigateToIndexAsync(_currentIndex + 1);
                    return true;
                }
            }

            return false;
        }


        private async Task RefreshSortFieldsAsync()
        {
            _sortFields.Clear();
            _selectedSortField = null;
            NotifyPropertyChanged(() => SelectedSortField);

            if (SelectedLayer == null)
            {
                return;
            }

            try
            {
                var fields = await QueuedTask.Run(() =>
                {
                    using var table = SelectedLayer.GetTable();
                    var definition = table?.GetDefinition();
                    if (definition == null)
                    {
                        return new List<SortFieldOption>();
                    }

                    return definition.GetFields()
                        .Where(field => IsSortableField(field.FieldType))
                        .Where(field => !IsSystemField(field.Name))
                        .OrderBy(field => field.Name, StringComparer.CurrentCultureIgnoreCase)
                        .Select(field => new SortFieldOption
                        {
                            Name = field.Name,
                            Alias = field.AliasName,
                            FieldType = field.FieldType
                        })
                        .ToList();
                });

                foreach (var field in fields)
                {
                    _sortFields.Add(field);
                }

                SelectedSortField = _sortFields.FirstOrDefault();
            }
            catch (Exception ex)
            {
                LogError($"加载排序字段失败: {ex.Message}");
            }
        }


        private async Task TryNavigateToNextValidAsync(int failedIndex)
        {
            var nextIndex = failedIndex + 1;
            if (nextIndex < _snapshotItems.Count)
            {
                await NavigateToIndexAsync(nextIndex, persistCurrentBeforeNavigate: false);
                return;
            }

            var previousIndex = failedIndex - 1;
            if (previousIndex >= 0)
            {
                await NavigateToIndexAsync(previousIndex, persistCurrentBeforeNavigate: false);
                return;
            }

            ClearSnapshotState("快照中的要素均不可访问。");
        }


        private void BrowseOutputGeodatabase()
        {
            try
            {
                var selectedPath = PresentationServices.Files.SelectGeodatabase(
                    "选择输出地理数据库",
                    string.IsNullOrWhiteSpace(OutputGdbPath) ? Project.Current?.HomeFolderPath : OutputGdbPath);
                if (!string.IsNullOrWhiteSpace(selectedPath))
                {
                    OutputGdbPath = selectedPath;
                }
            }
            catch (Exception ex)
            {
                LogError($"选择输出数据库失败: {ex.Message}");
            }
        }


        private void ShowHelp()
        {
            var helpText =
                "浏览要素工具说明\n\n" +
                "1) 选择目标图层、遍历范围（选择集/全量）与排序规则。\n" +
                "2) 点击“刷新快照”生成遍历列表。\n" +
                "3) 通过首条/上一条/下一条/末条进行导航，地图将自动定位并高亮。\n" +
                "4) 对多部件要素可使用“上一部件/下一部件”逐部件检查。\n" +
                "5) 审阅状态与备注会实时写入 GDB 清单表。\n\n" +
                "注意：\n" +
                "- 选择集模式下，地图选择变化后会提示快照过期，需要手动刷新。\n" +
                "- 目标表存在时会阻止写入，请更换表名后再刷新。";

            PresentationServices.Dialogs.Show(helpText, "浏览要素帮助");
        }


        private void OnMapSelectionChanged(MapSelectionChangedEventArgs args)
        {
            if (SelectedScopeMode?.Value != BrowseScopeMode.Selection || _snapshotItems.Count == 0)
            {
                return;
            }

            MarkSnapshotStale("检测到选择集变化，快照已过期，请刷新。");
        }

        private bool CanGoNext() => !IsBusy && _snapshotItems.Count > 0 && _currentIndex < _snapshotItems.Count - 1;

        private bool CanGoNextPart() => !IsBusy && _currentFeature?.PartEnvelopes != null && _currentFeature.PartEnvelopes.Count > 1 && _currentPartIndex < _currentFeature.PartEnvelopes.Count - 1;


        private void LogInfo(string message)
        {
            AppendLog(message);
        }


        private void LogError(string message)
        {
            AppendLog($"错误: {message}");
            StatusMessage = message;
        }


        private void AppendLog(string message)
        {
            var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            LogContent = string.IsNullOrWhiteSpace(LogContent)
                ? line
                : $"{LogContent}{Environment.NewLine}{line}";
        }


        private void UpdateSelectedSnapshotItem(long objectId)
        {
            var item = _snapshotList.FirstOrDefault(entry => entry.ObjectId == objectId);
            _suppressSnapshotListSelectionChanged = true;
            SelectedSnapshotItem = item;
            _suppressSnapshotListSelectionChanged = false;
        }


        private void UpdateSnapshotItem(long objectId, string reviewStatusText, string notes)
        {
            var item = _snapshotList.FirstOrDefault(entry => entry.ObjectId == objectId);
            if (item != null)
            {
                item.ReviewStatusText = reviewStatusText;
                item.NotesPreview = BuildNotesPreview(notes);
            }
        }


        private static void ShowOverlay(Geometry geometry)
        {
            var mapView = MapView.Active;
            if (mapView == null)
            {
                return;
            }

            ClearOverlays();

            if (geometry is MapPoint mapPoint)
            {
                var symbol = SymbolFactory.Instance
                    .ConstructPointSymbol(CIMColor.CreateRGBColor(255, 165, 0), 11, SimpleMarkerStyle.Circle)
                    .MakeSymbolReference();
                lock (OverlayLock)
                {
                    ActiveOverlays.Add(mapView.AddOverlay(mapPoint, symbol));
                }
                return;
            }

            if (geometry is Polygon polygon)
            {
                var boundary = GeometryEngine.Instance.Boundary(polygon);
                var symbol = SymbolFactory.Instance
                    .ConstructLineSymbol(CIMColor.CreateRGBColor(255, 90, 0), 3.2, SimpleLineStyle.Solid)
                    .MakeSymbolReference();
                lock (OverlayLock)
                {
                    ActiveOverlays.Add(mapView.AddOverlay(boundary, symbol));
                }
                return;
            }

            if (geometry is Polyline polyline)
            {
                var symbol = SymbolFactory.Instance
                    .ConstructLineSymbol(CIMColor.CreateRGBColor(255, 90, 0), 3.2, SimpleLineStyle.Solid)
                    .MakeSymbolReference();
                lock (OverlayLock)
                {
                    ActiveOverlays.Add(mapView.AddOverlay(polyline, symbol));
                }
                return;
            }

            if (geometry is Multipoint multipoint)
            {
                var symbol = SymbolFactory.Instance
                    .ConstructPointSymbol(CIMColor.CreateRGBColor(255, 165, 0), 9, SimpleMarkerStyle.Circle)
                    .MakeSymbolReference();
                foreach (var point in multipoint.Points)
                {
                    lock (OverlayLock)
                    {
                        ActiveOverlays.Add(mapView.AddOverlay(point, symbol));
                    }
                }
            }
        }

    }
}
