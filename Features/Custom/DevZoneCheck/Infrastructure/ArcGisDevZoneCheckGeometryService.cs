using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Features.Custom.DevZoneCheck.Infrastructure
{
    internal sealed class ArcGisDevZoneCheckGeometryService
    {
        public Task<List<(string Name, Geometry Geometry)>> LoadParkFeaturesAsync(
            FeatureLayer parkLayer,
            string groupField,
            Action<string> log)
        {
            return QueuedTask.Run(() =>
            {
                var result = new List<(string Name, Geometry Geometry)>();
                var featureClass = parkLayer.GetFeatureClass();

                if (!string.IsNullOrEmpty(groupField))
                {
                    log($"按字段 [{groupField}] 分组合并...");
                    var groups = new Dictionary<string, List<Geometry>>();
                    using (var cursor = featureClass.Search())
                    {
                        while (cursor.MoveNext())
                        {
                            using (var feature = cursor.Current as Feature)
                            {
                                var geometry = feature.GetShape();
                                if (geometry == null || geometry.IsEmpty)
                                {
                                    continue;
                                }

                                var groupValue = feature[groupField]?.ToString() ?? "未分组";
                                if (!groups.TryGetValue(groupValue, out var geometries))
                                {
                                    geometries = new List<Geometry>();
                                    groups[groupValue] = geometries;
                                }

                                geometries.Add(geometry);
                            }
                        }
                    }

                    foreach (var group in groups)
                    {
                        AddUnionOrIndividuals(result, group.Key, group.Value);
                    }

                    return result;
                }

                log("合并所有要素为整体...");
                var allGeometries = new List<Geometry>();
                using (var cursor = featureClass.Search())
                {
                    while (cursor.MoveNext())
                    {
                        using (var feature = cursor.Current as Feature)
                        {
                            var geometry = feature.GetShape();
                            if (geometry != null && !geometry.IsEmpty)
                            {
                                allGeometries.Add(geometry);
                            }
                        }
                    }
                }

                AddUnionOrIndividuals(result, parkLayer.Name, allGeometries);
                return result;
            });
        }

        public Task<double> CalculateApprovedNotSuppliedAreaAsync(
            FeatureLayer approvedLandLayer,
            FeatureLayer approvedNotSuppliedLayer,
            Geometry parkGeometry,
            Func<bool> isCancellationRequested,
            Func<Geometry, double> calculateArea)
        {
            return QueuedTask.Run(() =>
            {
                var approvedFeatureClass = approvedLandLayer.GetFeatureClass();
                var notSuppliedFeatureClass = approvedNotSuppliedLayer.GetFeatureClass();
                if (approvedFeatureClass == null || notSuppliedFeatureClass == null)
                {
                    return 0.0;
                }

                var targetSpatialReference = parkGeometry.SpatialReference;
                Geometry approvedGeometry = null;
                var approvedSpatialReference = approvedFeatureClass.GetDefinition().GetSpatialReference();
                var parkGeometryForApproved = parkGeometry;
                if (approvedSpatialReference != null &&
                    parkGeometry.SpatialReference?.Wkid != approvedSpatialReference.Wkid)
                {
                    try
                    {
                        parkGeometryForApproved = GeometryEngine.Instance.Project(parkGeometry, approvedSpatialReference);
                    }
                    catch
                    {
                        return 0.0;
                    }
                }

                var approvedFilter = new SpatialQueryFilter
                {
                    FilterGeometry = parkGeometryForApproved,
                    SpatialRelationship = SpatialRelationship.Intersects
                };

                using (var cursor = approvedFeatureClass.Search(approvedFilter))
                {
                    while (cursor.MoveNext())
                    {
                        if (isCancellationRequested())
                        {
                            break;
                        }

                        using (var feature = cursor.Current as Feature)
                        {
                            var geometry = feature.GetShape();
                            if (geometry == null || geometry.IsEmpty)
                            {
                                continue;
                            }

                            geometry = ProjectToTarget(geometry, targetSpatialReference);
                            var intersection = GeometryEngine.Instance.Intersection(geometry, parkGeometry);
                            if (intersection == null || intersection.IsEmpty)
                            {
                                continue;
                            }

                            intersection = ProjectToTarget(intersection, targetSpatialReference);
                            approvedGeometry = approvedGeometry == null
                                ? intersection
                                : GeometryEngine.Instance.Union(approvedGeometry, intersection);
                            if (approvedGeometry != null && !approvedGeometry.IsEmpty)
                            {
                                approvedGeometry = ProjectToTarget(approvedGeometry, targetSpatialReference);
                            }
                        }
                    }
                }

                if (approvedGeometry == null || approvedGeometry.IsEmpty)
                {
                    return 0.0;
                }

                var notSuppliedSpatialReference = notSuppliedFeatureClass.GetDefinition().GetSpatialReference();
                var approvedGeometryForNotSupplied = TryProject(approvedGeometry, notSuppliedSpatialReference);
                var notSuppliedFilter = new SpatialQueryFilter
                {
                    FilterGeometry = approvedGeometryForNotSupplied,
                    SpatialRelationship = SpatialRelationship.Intersects
                };

                double totalArea = 0;
                using (var cursor = notSuppliedFeatureClass.Search(notSuppliedFilter))
                {
                    while (cursor.MoveNext())
                    {
                        if (isCancellationRequested())
                        {
                            break;
                        }

                        using (var feature = cursor.Current as Feature)
                        {
                            var geometry = feature.GetShape();
                            if (geometry == null || geometry.IsEmpty)
                            {
                                continue;
                            }

                            geometry = ProjectToTarget(geometry, targetSpatialReference);
                            var intersection = GeometryEngine.Instance.Intersection(geometry, approvedGeometry);
                            if (intersection != null && !intersection.IsEmpty)
                            {
                                totalArea += calculateArea(intersection);
                            }
                        }
                    }
                }

                return totalArea;
            });
        }

        public Task<double> CalculateAvailableAreaAsync(
            FeatureLayer planningLayer,
            FeatureLayer suppliedLayer,
            FeatureLayer evidenceLayer,
            Geometry parkGeometry,
            Func<bool> isCancellationRequested,
            Func<Geometry, double> calculateArea)
        {
            return QueuedTask.Run(() =>
            {
                var planningFeatureClass = planningLayer.GetFeatureClass();
                var suppliedFeatureClass = suppliedLayer.GetFeatureClass();
                var evidenceFeatureClass = evidenceLayer?.GetFeatureClass();
                if (planningFeatureClass == null || suppliedFeatureClass == null)
                {
                    return 0.0;
                }

                var targetSpatialReference = parkGeometry.SpatialReference ??
                    planningFeatureClass.GetDefinition().GetSpatialReference();
                var planningSpatialReference = planningFeatureClass.GetDefinition().GetSpatialReference();
                var parkGeometryForPlanning = TryProject(parkGeometry, planningSpatialReference);
                var planningFilter = new SpatialQueryFilter
                {
                    FilterGeometry = parkGeometryForPlanning,
                    SpatialRelationship = SpatialRelationship.Intersects
                };

                Geometry remainingGeometry = null;
                using (var cursor = planningFeatureClass.Search(planningFilter))
                {
                    while (cursor.MoveNext())
                    {
                        if (isCancellationRequested())
                        {
                            break;
                        }

                        using (var feature = cursor.Current as Feature)
                        {
                            var geometry = feature.GetShape();
                            if (geometry == null || geometry.IsEmpty)
                            {
                                continue;
                            }

                            try
                            {
                                geometry = ProjectToTarget(geometry, targetSpatialReference);
                                var intersection = GeometryEngine.Instance.Intersection(geometry, parkGeometry);
                                if (intersection == null || intersection.IsEmpty)
                                {
                                    continue;
                                }

                                remainingGeometry = remainingGeometry == null
                                    ? intersection
                                    : GeometryEngine.Instance.Union(remainingGeometry, intersection);
                            }
                            catch
                            {
                                continue;
                            }
                        }
                    }
                }

                if (remainingGeometry == null || remainingGeometry.IsEmpty)
                {
                    return 0.0;
                }

                remainingGeometry = EraseFeatures(
                    remainingGeometry,
                    suppliedFeatureClass,
                    targetSpatialReference,
                    isCancellationRequested);
                if (remainingGeometry == null || remainingGeometry.IsEmpty)
                {
                    return 0.0;
                }

                if (evidenceFeatureClass != null)
                {
                    remainingGeometry = EraseFeatures(
                        remainingGeometry,
                        evidenceFeatureClass,
                        targetSpatialReference,
                        isCancellationRequested);
                }

                return remainingGeometry == null || remainingGeometry.IsEmpty
                    ? 0.0
                    : calculateArea(remainingGeometry);
            });
        }

        private static Geometry EraseFeatures(
            Geometry sourceGeometry,
            FeatureClass eraseFeatureClass,
            SpatialReference targetSpatialReference,
            Func<bool> isCancellationRequested)
        {
            var eraseSpatialReference = eraseFeatureClass.GetDefinition().GetSpatialReference();
            var sourceGeometryForFilter = TryProject(sourceGeometry, eraseSpatialReference);
            var filter = new SpatialQueryFilter
            {
                FilterGeometry = sourceGeometryForFilter,
                SpatialRelationship = SpatialRelationship.Intersects
            };

            var remainingGeometry = sourceGeometry;
            using (var cursor = eraseFeatureClass.Search(filter))
            {
                while (cursor.MoveNext())
                {
                    if (isCancellationRequested())
                    {
                        break;
                    }

                    using (var feature = cursor.Current as Feature)
                    {
                        var geometry = feature.GetShape();
                        if (geometry == null || geometry.IsEmpty)
                        {
                            continue;
                        }

                        try
                        {
                            geometry = ProjectToTarget(geometry, targetSpatialReference);
                            var difference = GeometryEngine.Instance.Difference(remainingGeometry, geometry);
                            if (difference == null || difference.IsEmpty)
                            {
                                return null;
                            }

                            remainingGeometry = difference;
                        }
                        catch
                        {
                            continue;
                        }
                    }
                }
            }

            return remainingGeometry;
        }

        private static void AddUnionOrIndividuals(
            ICollection<(string Name, Geometry Geometry)> target,
            string name,
            IReadOnlyList<Geometry> geometries)
        {
            if (geometries.Count == 0)
            {
                return;
            }

            if (geometries.Count == 1)
            {
                target.Add((name, geometries[0]));
                return;
            }

            try
            {
                target.Add((name, GeometryEngine.Instance.Union(geometries)));
            }
            catch
            {
                for (var index = 0; index < geometries.Count; index++)
                {
                    target.Add(($"{name}_{index + 1}", geometries[index]));
                }
            }
        }

        private static Geometry ProjectToTarget(Geometry geometry, SpatialReference targetSpatialReference)
        {
            if (targetSpatialReference == null ||
                geometry.SpatialReference?.Wkid == targetSpatialReference.Wkid)
            {
                return geometry;
            }

            return GeometryEngine.Instance.Project(geometry, targetSpatialReference);
        }

        private static Geometry TryProject(Geometry geometry, SpatialReference targetSpatialReference)
        {
            try
            {
                return ProjectToTarget(geometry, targetSpatialReference);
            }
            catch
            {
                return geometry;
            }
        }
    }
}
