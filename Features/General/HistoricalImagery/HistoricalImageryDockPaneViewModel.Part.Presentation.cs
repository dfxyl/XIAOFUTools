using ArcGIS.Core.CIM;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping.Events;
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
using XIAOFUTools.Features.General.HistoricalImageryDownload;
using XIAOFUTools.Features.General.HistoricalImageryDownload.Services;

namespace XIAOFUTools.Features.General.HistoricalImagery
{
    internal partial class HistoricalImageryDockPaneViewModel
    {

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
        /// 添加选中的图层到地图
        /// </summary>
        private async void AddSelectedLayer()
        {
            if (SelectedNode?.Version == null)
            {
                PresentationServices.Dialogs.Show(
                    "请选择一个历史影像版本",
                    "提示",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }

            var version = SelectedNode.Version;
            if (!TryCreateLayerRequest(version, out var request, out var errorMessage))
            {
                PresentationServices.Dialogs.Show(
                    errorMessage,
                    "提示",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }

            try
            {
                var result = await _mapLoadService.LoadAsync(request);
                if (!result.Succeeded)
                {
                    PresentationServices.Dialogs.Show(
                        result.ErrorMessage,
                        "错误",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                    return;
                }

                PresentationServices.Dialogs.Show(
                    $"已添加历史影像图层：{request.LayerName}",
                    "成功",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                PresentationServices.Dialogs.Show(
                    $"添加图层失败：{ex.Message}",
                    "错误",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
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


        private void ShowHelp()
        {
            PresentationServices.Dialogs.Show(
                HistoricalImageryHelpTextBuilder.Build(),
                "历史影像工具说明");
        }

    }
}
