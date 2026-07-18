using System;
using System.Collections.Generic;
using System.Linq;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;

namespace XIAOFUTools.Features.General.QuickAddData
{
    internal sealed class ArcGisQuickDataGeodatabaseInspector : IQuickDataGeodatabaseInspector
    {
        public IReadOnlyList<QuickDataGeodatabaseChild> Inspect(string geodatabasePath)
        {
            return QueuedTask.Run(() =>
            {
                var children = new List<QuickDataGeodatabaseChild>();

                using var geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(geodatabasePath)));

                var featureDatasetMembers = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                foreach (var featureDatasetDefinition in geodatabase.GetDefinitions<FeatureDatasetDefinition>())
                {
                    var datasetPath = featureDatasetDefinition.GetName();
                    featureDatasetMembers[datasetPath] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    children.Add(new QuickDataGeodatabaseChild
                    {
                        Name = featureDatasetDefinition.GetName(),
                        DatasetPath = datasetPath,
                        NodeKind = QuickDataNodeKind.FeatureDataset,
                        GeometryKind = QuickDataGeometryKind.None,
                        CoordinateSystem = featureDatasetDefinition.GetSpatialReference()?.Name ?? "未知"
                    });

                    foreach (var definition in geodatabase.GetRelatedDefinitions(
                        featureDatasetDefinition,
                        DefinitionRelationshipType.DatasetInFeatureDataset))
                    {
                        featureDatasetMembers[datasetPath].Add(definition.GetName());
                    }
                }

                foreach (var featureClassDefinition in geodatabase.GetDefinitions<FeatureClassDefinition>())
                {
                    var parentDatasetPath = featureDatasetMembers
                        .FirstOrDefault(item => item.Value.Contains(featureClassDefinition.GetName()))
                        .Key;

                    children.Add(new QuickDataGeodatabaseChild
                    {
                        Name = featureClassDefinition.GetName(),
                        ParentDatasetPath = parentDatasetPath ?? string.Empty,
                        DatasetPath = string.IsNullOrWhiteSpace(parentDatasetPath)
                            ? featureClassDefinition.GetName()
                            : $@"{parentDatasetPath}\{featureClassDefinition.GetName()}",
                        NodeKind = QuickDataNodeKind.FeatureClass,
                        GeometryKind = GetGeometryKind(featureClassDefinition.GetShapeType()),
                        CoordinateSystem = featureClassDefinition.GetSpatialReference()?.Name ?? "未知"
                    });
                }

                foreach (var tableDefinition in geodatabase.GetDefinitions<TableDefinition>())
                {
                    if (tableDefinition is FeatureClassDefinition)
                    {
                        continue;
                    }

                    children.Add(new QuickDataGeodatabaseChild
                    {
                        Name = tableDefinition.GetName(),
                        DatasetPath = tableDefinition.GetName(),
                        NodeKind = QuickDataNodeKind.Table,
                        GeometryKind = QuickDataGeometryKind.None,
                        CoordinateSystem = "无"
                    });
                }

                return (IReadOnlyList<QuickDataGeodatabaseChild>)children;
            }).Result;
        }

        private static QuickDataGeometryKind GetGeometryKind(GeometryType geometryType)
        {
            return geometryType switch
            {
                GeometryType.Point => QuickDataGeometryKind.Point,
                GeometryType.Polyline => QuickDataGeometryKind.Polyline,
                GeometryType.Polygon => QuickDataGeometryKind.Polygon,
                GeometryType.Multipoint => QuickDataGeometryKind.Multipoint,
                _ => QuickDataGeometryKind.Unknown
            };
        }
    }
}
