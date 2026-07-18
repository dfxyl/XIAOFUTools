using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Features.Conversion.FeatureToTxt.Core;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.Conversion.FeatureToTxt.Infrastructure
{
    internal sealed class ArcGisFeatureExportReader
    {
        internal Task<FeatureExportReadResult> ReadAsync(
            FeatureLayer layer,
            bool useSelection,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(layer);
            return QueuedTask.Run(() => ReadCore(layer, useSelection, cancellationToken));
        }

        private static FeatureExportReadResult ReadCore(
            FeatureLayer layer,
            bool useSelection,
            CancellationToken cancellationToken)
        {
            var snapshots = new List<FeatureExportSnapshot>();
            var warnings = new List<string>();
            var useCurrentSelection = useSelection && layer.SelectionCount > 0;
            using var cursor = SelectionUtils.GetSelectionOrAllCursor(
                layer,
                useCurrentSelection,
                null,
                false);
            if (cursor == null)
            {
                throw new InvalidOperationException("无法获取要素游标。");
            }

            var sourceCount = 0;
            while (cursor.MoveNext())
            {
                cancellationToken.ThrowIfCancellationRequested();
                sourceCount++;
                using var feature = cursor.Current as Feature;
                if (feature == null)
                {
                    warnings.Add($"第 {sourceCount} 条记录不是有效要素，已跳过。");
                    continue;
                }

                Geometry geometry;
                try
                {
                    geometry = feature.GetShape();
                }
                catch (Exception ex)
                {
                    warnings.Add($"要素 {feature.GetObjectID()} 读取几何失败：{ex.Message}");
                    continue;
                }

                if (geometry is not Polygon polygon || polygon.IsEmpty)
                {
                    warnings.Add($"要素 {feature.GetObjectID()} 不是有效面几何，已跳过。");
                    continue;
                }

                snapshots.Add(new FeatureExportSnapshot(
                    feature.GetObjectID(),
                    polygon.Area,
                    ExtractRings(polygon),
                    ReadFieldValues(feature)));
            }

            return new FeatureExportReadResult(sourceCount, snapshots, warnings);
        }

        private static IReadOnlyDictionary<string, string> ReadFieldValues(Feature feature)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in feature.GetFields())
            {
                if (field.FieldType == FieldType.Geometry)
                {
                    continue;
                }

                try
                {
                    var value = feature[field.Name];
                    values[field.Name] = value == null || value == DBNull.Value
                        ? string.Empty
                        : Convert.ToString(value) ?? string.Empty;
                }
                catch
                {
                    values[field.Name] = string.Empty;
                }
            }

            return values;
        }

        private static IReadOnlyList<FeatureCoordinateRing> ExtractRings(Polygon polygon)
        {
            var rings = new List<FeatureCoordinateRing>(polygon.PartCount);
            foreach (var part in polygon.Parts)
            {
                var points = new List<FeatureCoordinate>();
                Segment lastSegment = null;
                foreach (var segment in part)
                {
                    points.Add(new FeatureCoordinate(segment.StartPoint.X, segment.StartPoint.Y));
                    lastSegment = segment;
                }

                if (lastSegment != null)
                {
                    points.Add(new FeatureCoordinate(lastSegment.EndPoint.X, lastSegment.EndPoint.Y));
                }

                rings.Add(new FeatureCoordinateRing(points));
            }

            return rings;
        }
    }
}
