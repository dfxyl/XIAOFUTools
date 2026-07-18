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

        public void Cleanup()
        {
            if (_selectionChangedToken != null)
            {
                MapSelectionChangedEvent.Unsubscribe(_selectionChangedToken);
                _selectionChangedToken = null;
            }

            Interlocked.Increment(ref _flashVersion);
            _ = QueuedTask.Run(ClearOverlays);
        }


        private async Task NavigateToSnapshotItemAsync(SnapshotListItem item)
        {
            if (item == null)
            {
                return;
            }

            var index = _snapshotItems.FindIndex(snapshot => snapshot.ObjectId == item.ObjectId);
            if (index >= 0 && index != _currentIndex)
            {
                await NavigateToIndexAsync(index);
            }
        }

        private async Task NavigateToIndexAsync(int targetIndex, bool persistCurrentBeforeNavigate = true)
        {
            if (targetIndex < 0 || targetIndex >= _snapshotItems.Count)
            {
                return;
            }

            if (persistCurrentBeforeNavigate)
            {
                await PersistCurrentReviewRecordAsync(forcePersist: true);
            }

            _currentIndex = targetIndex;
            var targetItem = _snapshotItems[_currentIndex];
            var context = await LoadFeatureContextAsync(targetItem.ObjectId);
            if (context == null || context.Geometry == null || context.Geometry.IsEmpty)
            {
                LogError($"OID={targetItem.ObjectId} 已失效或几何为空。");
                await TryNavigateToNextValidAsync(targetIndex);
                return;
            }

            _currentFeature = context;
            _currentPartIndex = 0;
            _currentFeatureVisitedAt = DateTime.Now;
            SnapshotInfo = $"{_currentIndex + 1}/{_snapshotItems.Count}";
            CurrentOidText = context.ObjectId.ToString(CultureInfo.InvariantCulture);
            PartInfoText = BuildPartInfoText();
            StatusMessage = $"正在浏览第 {_currentIndex + 1} 条要素。";
            UpdateSelectedSnapshotItem(context.ObjectId);

            await FocusCurrentPartAsync();
            await LoadCurrentReviewAsync();
            await PersistCurrentReviewRecordAsync(forcePersist: true);

            RaiseCommandStates();
        }


        private async Task NavigatePartAsync(int delta)
        {
            if (_currentFeature?.PartEnvelopes == null || _currentFeature.PartEnvelopes.Count <= 1)
            {
                return;
            }

            var nextIndex = _currentPartIndex + delta;
            if (nextIndex < 0 || nextIndex >= _currentFeature.PartEnvelopes.Count)
            {
                return;
            }

            _currentPartIndex = nextIndex;
            PartInfoText = BuildPartInfoText();
            await FocusCurrentPartAsync();
            RaiseCommandStates();
        }


        private async Task FocusCurrentPartAsync()
        {
            var context = _currentFeature;
            if (context?.Geometry == null)
            {
                return;
            }

            await QueuedTask.Run(() =>
            {
                var mapView = MapView.Active;
                if (mapView == null)
                {
                    return;
                }

                var envelope = context.Geometry.Extent;
                if (context.PartEnvelopes != null && context.PartEnvelopes.Count > 0 && _currentPartIndex >= 0 && _currentPartIndex < context.PartEnvelopes.Count)
                {
                    envelope = context.PartEnvelopes[_currentPartIndex];
                }

                if (envelope == null)
                {
                    return;
                }

                var safeEnvelope = ExpandEnvelopeIfNeeded(envelope, mapView.Extent);
                mapView.ZoomTo(safeEnvelope);
            });

            await FlashGeometryAsync(context.Geometry);
        }


        private async Task FlashGeometryAsync(Geometry geometry)
        {
            if (geometry == null)
            {
                return;
            }

            var version = Interlocked.Increment(ref _flashVersion);
            for (var i = 0; i < 2; i++)
            {
                if (version != Interlocked.Read(ref _flashVersion))
                {
                    return;
                }

                await QueuedTask.Run(() => ShowOverlay(geometry));
                await Task.Delay(180);

                if (version != Interlocked.Read(ref _flashVersion))
                {
                    return;
                }

                await QueuedTask.Run(ClearOverlays);
                await Task.Delay(90);
            }
        }


        private async Task PersistCurrentReviewRecordAsync(bool forcePersist = false)
        {
            if (_suppressReviewPropertyChanged || !_reviewTableReady || _currentFeature == null || _currentIndex < 0)
            {
                return;
            }

            if (_isPersistingCurrentRecord && !forcePersist)
            {
                return;
            }

            _isPersistingCurrentRecord = true;
            try
            {
                var batchId = BatchId?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(batchId))
                {
                    return;
                }

                var record = new ReviewRecord
                {
                    ProjectKey = BuildCurrentProjectKey(),
                    BatchId = batchId,
                    BatchName = batchId,
                    Reviewer = ReviewerName?.Trim() ?? string.Empty,
                    ReviewStatus = BrowseFeaturesCore.NormalizeReviewStatus(SelectedReviewStatus),
                    Notes = CurrentNotes ?? string.Empty,
                    LayerName = SelectedLayer?.Name ?? string.Empty,
                    LayerUri = SelectedLayer?.URI ?? string.Empty,
                    SourceObjectId = _currentFeature.ObjectId,
                    GeometryType = _currentFeature.GeometryType ?? string.Empty,
                    GeometryWkt = _currentFeature.GeometryWkt ?? string.Empty,
                    VisitedAt = _currentFeatureVisitedAt == DateTime.MinValue ? DateTime.Now : _currentFeatureVisitedAt,
                    UpdatedAt = DateTime.Now
                };

                await QueuedTask.Run(() => _reviewStore.Upsert(GetEffectiveOutputGdbPath(), OutputTableName, record));
                await WriteNotesToFeatureFieldAsync(record.Notes);
                UpdateSnapshotItem(_currentFeature.ObjectId, BrowseFeaturesCore.ToDisplayText(record.ReviewStatus), record.Notes);
            }
            catch (Exception ex)
            {
                LogError($"写入审阅记录失败: {ex.Message}");
            }
            finally
            {
                _isPersistingCurrentRecord = false;
            }
        }

        private void ResetReviewTableState()
        {
            _reviewTableReady = false;
            _reviewTableSignature = string.Empty;
        }


        private void MarkSnapshotStale(string message)
        {
            if (!HasSnapshot)
            {
                return;
            }

            IsSnapshotStale = true;
            StaleHint = message;
            StatusMessage = message;
        }


        private void ClearSnapshotState(string message)
        {
            _snapshotItems.Clear();
            _currentIndex = -1;
            _currentFeature = null;
            _currentPartIndex = 0;
            _currentFeatureVisitedAt = DateTime.MinValue;
            SnapshotInfo = "0/0";
            CurrentOidText = "-";
            PartInfoText = "-";
            _suppressSnapshotListSelectionChanged = true;
            SelectedSnapshotItem = null;
            _suppressSnapshotListSelectionChanged = false;
            _snapshotList.Clear();
            IsSnapshotStale = false;
            StaleHint = string.Empty;
            StatusMessage = message;
            RaiseCommandStates();
        }


        private bool CanGoFirst() => !IsBusy && _snapshotItems.Count > 0 && _currentIndex > 0;

        private bool CanGoPrevious() => !IsBusy && _snapshotItems.Count > 0 && _currentIndex > 0;

        private bool CanGoLast() => !IsBusy && _snapshotItems.Count > 0 && _currentIndex >= 0 && _currentIndex < _snapshotItems.Count - 1;

        private bool CanGoPreviousPart() => !IsBusy && _currentFeature?.PartEnvelopes != null && _currentFeature.PartEnvelopes.Count > 1 && _currentPartIndex > 0;


        private void RaiseCommandStates()
        {
            _refreshLayersCommand.RaiseCanExecuteChanged();
            _refreshSnapshotCommand.RaiseCanExecuteChanged();
            _browseOutputGdbCommand.RaiseCanExecuteChanged();
            _firstFeatureCommand.RaiseCanExecuteChanged();
            _previousFeatureCommand.RaiseCanExecuteChanged();
            _nextFeatureCommand.RaiseCanExecuteChanged();
            _lastFeatureCommand.RaiseCanExecuteChanged();
            _previousPartCommand.RaiseCanExecuteChanged();
            _nextPartCommand.RaiseCanExecuteChanged();
        }


        private static bool IsSupportedLayer(FeatureLayer layer)
        {
            var shapeType = layer.ShapeType;
            return shapeType == esriGeometryType.esriGeometryPoint ||
                   shapeType == esriGeometryType.esriGeometryMultipoint ||
                   shapeType == esriGeometryType.esriGeometryPolyline ||
                   shapeType == esriGeometryType.esriGeometryPolygon;
        }


        private static bool IsSortableField(FieldType fieldType)
        {
            return fieldType == FieldType.String ||
                   fieldType == FieldType.Integer ||
                   fieldType == FieldType.SmallInteger ||
                   fieldType == FieldType.Double ||
                   fieldType == FieldType.Single ||
                   fieldType == FieldType.BigInteger ||
                   fieldType == FieldType.Date ||
                   fieldType == FieldType.DateOnly ||
                   fieldType == FieldType.GUID ||
                   fieldType == FieldType.GlobalID;
        }


        private static bool IsSystemField(string fieldName)
        {
            return string.Equals(fieldName, "OBJECTID", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fieldName, "OID", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fieldName, "FID", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fieldName, "GLOBALID", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fieldName, "SHAPE", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fieldName, "SHAPE_LENGTH", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fieldName, "SHAPE_AREA", StringComparison.OrdinalIgnoreCase);
        }
        private static Envelope ExpandEnvelopeIfNeeded(Envelope sourceEnvelope, Envelope currentViewExtent)
        {
            var width = sourceEnvelope.Width;
            var height = sourceEnvelope.Height;
            var sr = sourceEnvelope.SpatialReference;
            var xCenter = (sourceEnvelope.XMin + sourceEnvelope.XMax) / 2.0;
            var yCenter = (sourceEnvelope.YMin + sourceEnvelope.YMax) / 2.0;
            const double paddingRatio = 0.2;
            const double fallbackViewRatio = 0.03;

            if (width > 0 && height > 0)
            {
                var paddingX = width * paddingRatio;
                var paddingY = height * paddingRatio;
                return EnvelopeBuilderEx.CreateEnvelope(
                    sourceEnvelope.XMin - paddingX,
                    sourceEnvelope.YMin - paddingY,
                    sourceEnvelope.XMax + paddingX,
                    sourceEnvelope.YMax + paddingY,
                    sr);
            }

            var viewWidth = currentViewExtent?.Width ?? 0;
            var viewHeight = currentViewExtent?.Height ?? 0;
            var fallbackWidth = viewWidth > 0 ? viewWidth * fallbackViewRatio : 1.0;
            var fallbackHeight = viewHeight > 0 ? viewHeight * fallbackViewRatio : 1.0;

            if (width > 0 || height > 0)
            {
                var paddingX = width > 0 ? width * paddingRatio : fallbackWidth;
                var paddingY = height > 0 ? height * paddingRatio : fallbackHeight;
                return EnvelopeBuilderEx.CreateEnvelope(
                    sourceEnvelope.XMin - paddingX,
                    sourceEnvelope.YMin - paddingY,
                    sourceEnvelope.XMax + paddingX,
                    sourceEnvelope.YMax + paddingY,
                    sr);
            }

            return EnvelopeBuilderEx.CreateEnvelope(
                xCenter - fallbackWidth,
                yCenter - fallbackHeight,
                xCenter + fallbackWidth,
                yCenter + fallbackHeight,
                sr);
        }


        private static void ClearOverlays()
        {
            lock (OverlayLock)
            {
                foreach (var overlay in ActiveOverlays)
                {
                    overlay?.Dispose();
                }
                ActiveOverlays.Clear();
            }
        }

    }
}
