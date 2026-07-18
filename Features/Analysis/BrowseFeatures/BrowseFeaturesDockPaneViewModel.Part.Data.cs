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

        private void LoadSettings()
        {
            var settings = SettingsManager.Settings.BrowseFeatures;
            _reviewerName = settings.ReviewerName ?? string.Empty;
            _outputGdbPath = settings.OutputGdbPath ?? string.Empty;
            _outputTableName = settings.OutputTableName ?? string.Empty;
            _batchId = settings.BatchId ?? string.Empty;
            _hasExplicitOutputTableName = !string.IsNullOrWhiteSpace(_outputTableName);
            _hasExplicitBatchId = !string.IsNullOrWhiteSpace(_batchId);
        }


        private async Task AutoResolveReviewStorageAsync()
        {
            var gdbPath = GetEffectiveOutputGdbPath();
            if (string.IsNullOrWhiteSpace(gdbPath))
            {
                return;
            }

            try
            {
                if (!_hasExplicitOutputTableName)
                {
                    var compatibleTables = await QueuedTask.Run(() =>
                        _reviewStore.GetCompatibleTableNames(gdbPath, ReviewTableNamePrefix));
                    var layerUri = SelectedLayer?.URI ?? string.Empty;
                    var projectKey = BuildCurrentProjectKey();
                    string matchedTableName = null;
                    foreach (var tableName in compatibleTables)
                    {
                        var batchId = await QueuedTask.Run(() =>
                            _reviewStore.TryGetLatestBatchId(gdbPath, tableName, projectKey, layerUri));
                        if (!string.IsNullOrWhiteSpace(batchId))
                        {
                            matchedTableName = tableName;
                            break;
                        }
                    }

                    _outputTableName = matchedTableName ?? compatibleTables.FirstOrDefault() ?? $"{ReviewTableNamePrefix}{BrowseFeaturesCore.CreateDefaultBatchId(DateTime.Now)}";
                    NotifyPropertyChanged(() => OutputTableName);
                }

                if (!_hasExplicitBatchId)
                {
                    var layerUri = SelectedLayer?.URI;
                    if (!string.IsNullOrWhiteSpace(layerUri) && !string.IsNullOrWhiteSpace(_outputTableName))
                    {
                        var latestBatchId = await QueuedTask.Run(() =>
                            _reviewStore.TryGetLatestBatchId(gdbPath, _outputTableName, BuildCurrentProjectKey(), layerUri));
                        _batchId = latestBatchId ?? BrowseFeaturesCore.CreateDefaultBatchId(DateTime.Now);
                        NotifyPropertyChanged(() => BatchId);
                    }
                    else if (string.IsNullOrWhiteSpace(_batchId))
                    {
                        _batchId = BrowseFeaturesCore.CreateDefaultBatchId(DateTime.Now);
                        NotifyPropertyChanged(() => BatchId);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"自动识别审阅表失败: {ex.Message}");
            }
        }


        private async Task<long?> TryGetResumeObjectIdAsync()
        {
            if (!_reviewTableReady || SelectedLayer == null)
            {
                return null;
            }

            try
            {
                var batchId = BatchId?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(batchId))
                {
                    return null;
                }

                return await QueuedTask.Run(() =>
                    _reviewStore.TryGetLatestObjectId(
                        GetEffectiveOutputGdbPath(),
                        OutputTableName,
                        BuildCurrentProjectKey(),
                        batchId,
                        SelectedLayer.URI ?? string.Empty));
            }
            catch (Exception ex)
            {
                LogError($"恢复上次浏览位置失败: {ex.Message}");
                return null;
            }
        }


        private async Task<CurrentFeatureContext> LoadFeatureContextAsync(long objectId)
        {
            return await QueuedTask.Run(() =>
            {
                var layer = SelectedLayer;
                if (layer == null)
                {
                    return null;
                }

                using var table = layer.GetTable();
                if (table == null)
                {
                    return null;
                }

                using var cursor = table.Search(new QueryFilter { ObjectIDs = new[] { objectId } }, false);
                if (!cursor.MoveNext())
                {
                    return null;
                }

                using var feature = cursor.Current as Feature;
                var shape = feature?.GetShape();
                if (shape == null || shape.IsEmpty)
                {
                    return null;
                }

                return new CurrentFeatureContext
                {
                    ObjectId = objectId,
                    Geometry = shape,
                    GeometryType = shape.GeometryType.ToString(),
                    GeometryWkt = TryExportToWkt(shape),
                    PartEnvelopes = BuildPartEnvelopes(shape)
                };
            });
        }


        private async Task LoadCurrentReviewAsync()
        {
            var context = _currentFeature;
            if (context == null || !_reviewTableReady)
            {
                return;
            }

            try
            {
                var batchId = BatchId?.Trim() ?? string.Empty;
                var layerUri = SelectedLayer?.URI ?? string.Empty;
                var review = await QueuedTask.Run(() =>
                    _reviewStore.TryGet(GetEffectiveOutputGdbPath(), OutputTableName, BuildCurrentProjectKey(), batchId, layerUri, context.ObjectId));

                _suppressReviewPropertyChanged = true;
                if (review == null)
                {
                    SelectedReviewStatus = "未判定";
                    CurrentNotes = string.Empty;
                    UpdateSnapshotItem(context.ObjectId, "未判定", string.Empty);
                }
                else
                {
                    SelectedReviewStatus = BrowseFeaturesCore.ToDisplayText(review.ReviewStatus);
                    CurrentNotes = review.Notes ?? string.Empty;
                    UpdateSnapshotItem(context.ObjectId, SelectedReviewStatus, CurrentNotes);
                    if (!string.IsNullOrWhiteSpace(review.Reviewer))
                    {
                        ReviewerName = review.Reviewer;
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"读取审阅记录失败: {ex.Message}");
            }
            finally
            {
                _suppressReviewPropertyChanged = false;
            }
        }

        private async Task<bool> EnsureReviewTableReadyAsync()
        {
            var gdbPath = GetEffectiveOutputGdbPath();
            var tableName = OutputTableName?.Trim();
            if (string.IsNullOrWhiteSpace(gdbPath))
            {
                PresentationServices.Dialogs.Show("当前项目没有默认地理数据库，请到设置里指定输出 GDB。", "提示");
                return false;
            }

            if (string.IsNullOrWhiteSpace(tableName))
            {
                PresentationServices.Dialogs.Show("请设置输出表名。", "提示");
                return false;
            }

            var signature = $"{gdbPath}|{tableName}";
            if (_reviewTableReady && string.Equals(_reviewTableSignature, signature, StringComparison.Ordinal))
            {
                return true;
            }

            try
            {
                var existedBefore = await QueuedTask.Run(() => _reviewStore.TableExists(gdbPath, tableName));
                await QueuedTask.Run(() =>
                {
                    if (existedBefore)
                    {
                        if (!_reviewStore.HasRequiredSchema(gdbPath, tableName))
                        {
                            throw new InvalidOperationException($"目标表已存在但结构不兼容：{tableName}。请更换表名后重试。");
                        }

                        return;
                    }

                    _reviewStore.CreateTable(gdbPath, tableName);
                });

                _reviewTableReady = true;
                _reviewTableSignature = signature;
                LogInfo(existedBefore
                    ? $"已连接审阅清单表: {gdbPath}\\{tableName}"
                    : $"审阅清单表已创建: {gdbPath}\\{tableName}");
                return true;
            }
            catch (Exception ex)
            {
                PresentationServices.Dialogs.Show(ex.Message, "输出表冲突");
                LogError($"初始化审阅表失败: {ex.Message}");
                _reviewTableReady = false;
                return false;
            }
        }


        private string GetEffectiveOutputGdbPath()
        {
            return string.IsNullOrWhiteSpace(OutputGdbPath)
                ? Project.Current?.DefaultGeodatabasePath ?? string.Empty
                : OutputGdbPath.Trim();
        }

    }
}
