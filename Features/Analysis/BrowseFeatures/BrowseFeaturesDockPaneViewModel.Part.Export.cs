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

        private void SaveSettings()
        {
            var settings = SettingsManager.Settings.BrowseFeatures;
            settings.ReviewerName = ReviewerName ?? string.Empty;
            settings.OutputGdbPath = OutputGdbPath ?? string.Empty;
            settings.OutputTableName = OutputTableName ?? string.Empty;
            settings.BatchId = BatchId ?? string.Empty;
            SettingsManager.SaveSettings();
        }


        private void RebuildSnapshotList()
        {
            _snapshotList.Clear();
            for (var i = 0; i < _snapshotItems.Count; i++)
            {
                _snapshotList.Add(new SnapshotListItem
                {
                    DisplayIndex = i + 1,
                    ObjectId = _snapshotItems[i].ObjectId
                });
            }
        }


        private async Task<(List<FeatureSnapshotItem> Items, bool UsedFallbackSort)> BuildSnapshotAsync()
        {
            return await QueuedTask.Run(() =>
            {
                var items = new List<FeatureSnapshotItem>();
                var usedFallbackSort = false;
                var layer = SelectedLayer;
                if (layer == null)
                {
                    return (items, usedFallbackSort);
                }

                using var table = layer.GetTable();
                if (table == null)
                {
                    return (items, usedFallbackSort);
                }

                bool canUseFieldSort = false;
                if (SelectedSortMode?.Value == BrowseSortMode.Field && SelectedSortField != null)
                {
                    var definition = table.GetDefinition();
                    if (definition?.FindField(SelectedSortField.Name) >= 0)
                    {
                        canUseFieldSort = true;
                    }
                    else
                    {
                        usedFallbackSort = true;
                    }
                }

                RowCursor cursor;
                var queryFilter = new QueryFilter();
                if (SelectedScopeMode?.Value == BrowseScopeMode.Selection)
                {
                    if (layer.SelectionCount <= 0)
                    {
                        return (items, usedFallbackSort);
                    }

                    cursor = layer.GetSelection().Search(queryFilter, false);
                }
                else
                {
                    cursor = table.Search(queryFilter, false);
                }

                using (cursor)
                {
                    while (cursor.MoveNext())
                    {
                        using var row = cursor.Current;
                        if (row == null)
                        {
                            continue;
                        }

                        object sortValue = null;
                        if (canUseFieldSort && SelectedSortField != null)
                        {
                            try
                            {
                                sortValue = row[SelectedSortField.Name];
                            }
                            catch
                            {
                                sortValue = null;
                            }
                        }

                        items.Add(new FeatureSnapshotItem
                        {
                            ObjectId = row.GetObjectID(),
                            SortValue = sortValue
                        });
                    }
                }

                if (canUseFieldSort && SelectedSortField != null)
                {
                    items.Sort((left, right) =>
                    {
                        var compare = BrowseFeaturesCore.CompareSortValue(left.SortValue, right.SortValue, SortDescending);
                        if (compare != 0)
                        {
                            return compare;
                        }

                        return left.ObjectId.CompareTo(right.ObjectId);
                    });
                }
                else
                {
                    if (usedFallbackSort)
                    {
                        items.Sort((left, right) => left.ObjectId.CompareTo(right.ObjectId));
                    }
                    else
                    {
                        items.Sort((left, right) => SortDescending
                            ? right.ObjectId.CompareTo(left.ObjectId)
                            : left.ObjectId.CompareTo(right.ObjectId));
                    }
                }

                return (items, usedFallbackSort);
            });
        }


        private string BuildPartInfoText()
        {
            var count = _currentFeature?.PartEnvelopes?.Count ?? 0;
            if (count <= 0)
            {
                return "-";
            }

            return $"{_currentPartIndex + 1}/{count}";
        }


        private string BuildCurrentProjectKey()
        {
            return Project.Current?.HomeFolderPath ??
                   Project.Current?.Name ??
                   string.Empty;
        }


        private async Task WriteNotesToFeatureFieldAsync(string notes)
        {
            var settings = SettingsManager.Settings.BrowseFeatures;
            if (!settings.WriteNotesToFeatureField || string.IsNullOrWhiteSpace(settings.NotesFieldName) || SelectedLayer == null || _currentFeature == null)
            {
                return;
            }

            try
            {
                await QueuedTask.Run(() =>
                {
                    using var table = SelectedLayer.GetTable();
                    var definition = table?.GetDefinition();
                    if (definition == null || definition.FindField(settings.NotesFieldName) < 0)
                    {
                        return;
                    }

                    using var cursor = table.Search(new QueryFilter { ObjectIDs = new[] { _currentFeature.ObjectId } }, false);
                    if (!cursor.MoveNext())
                    {
                        return;
                    }

                    using var row = cursor.Current;
                    row[settings.NotesFieldName] = notes ?? string.Empty;
                    row.Store();
                });
            }
            catch (Exception ex)
            {
                LogError($"备注写回要素字段失败: {ex.Message}");
            }
        }


        private static string BuildNotesPreview(string notes)
        {
            if (string.IsNullOrWhiteSpace(notes))
            {
                return string.Empty;
            }

            var normalized = notes.Replace("\r", " ").Replace("\n", " ").Trim();
            return normalized.Length <= 20 ? normalized : normalized.Substring(0, 20) + "...";
        }


        private static IReadOnlyList<Envelope> BuildPartEnvelopes(Geometry geometry)
        {
            if (geometry == null || geometry.IsEmpty)
            {
                return Array.Empty<Envelope>();
            }

            if (geometry is Multipart multipart)
            {
                var envelopes = new List<Envelope>();
                foreach (var part in multipart.Parts)
                {
                    var hasPoint = false;
                    var xMin = 0.0;
                    var yMin = 0.0;
                    var xMax = 0.0;
                    var yMax = 0.0;

                    foreach (var segment in part)
                    {
                        AddPoint(segment.StartPoint);
                        AddPoint(segment.EndPoint);
                    }

                    if (hasPoint)
                    {
                        envelopes.Add(EnvelopeBuilderEx.CreateEnvelope(xMin, yMin, xMax, yMax, multipart.SpatialReference));
                    }

                    void AddPoint(MapPoint point)
                    {
                        if (point == null)
                        {
                            return;
                        }

                        if (!hasPoint)
                        {
                            xMin = xMax = point.X;
                            yMin = yMax = point.Y;
                            hasPoint = true;
                            return;
                        }

                        xMin = Math.Min(xMin, point.X);
                        yMin = Math.Min(yMin, point.Y);
                        xMax = Math.Max(xMax, point.X);
                        yMax = Math.Max(yMax, point.Y);
                    }
                }

                if (envelopes.Count > 0)
                {
                    return envelopes;
                }
            }

            return geometry.Extent == null
                ? Array.Empty<Envelope>()
                : new[] { geometry.Extent };
        }


        private static string TryExportToWkt(Geometry geometry)
        {
            if (geometry == null || geometry.IsEmpty)
            {
                return string.Empty;
            }

            try
            {
                // 兼容不同 ArcGIS Pro SDK 版本可能存在的导出重载差异。
                var methods = GeometryEngine.Instance.GetType()
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .Where(method => string.Equals(method.Name, "ExportToWKT", StringComparison.Ordinal))
                    .ToList();

                foreach (var method in methods)
                {
                    var parameters = method.GetParameters();
                    if (parameters.Length == 1 && parameters[0].ParameterType.IsAssignableFrom(typeof(Geometry)))
                    {
                        var value = method.Invoke(GeometryEngine.Instance, new object[] { geometry });
                        if (value is string text && !string.IsNullOrWhiteSpace(text))
                        {
                            return text;
                        }
                    }
                    else if (parameters.Length == 2 &&
                             parameters[1].ParameterType.IsAssignableFrom(typeof(Geometry)) &&
                             parameters[0].ParameterType.IsEnum)
                    {
                        var flag = Enum.ToObject(parameters[0].ParameterType, 0);
                        var value = method.Invoke(GeometryEngine.Instance, new[] { flag, geometry });
                        if (value is string text && !string.IsNullOrWhiteSpace(text))
                        {
                            return text;
                        }
                    }
                }
            }
            catch
            {
                // WKT 导出失败时返回空字符串，调用侧记录日志但不中断流程。
            }

            return string.Empty;
        }

    }
}
