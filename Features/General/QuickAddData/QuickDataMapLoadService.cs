using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Features.General.QuickAddData
{
    public sealed class QuickDataLoadResult
    {
        public int AddedCount { get; set; }

        public int SkippedCount { get; set; }

        public int FailedCount { get; set; }

        public bool MissingActiveMap { get; set; }
    }

    public sealed class QuickDataMapLoadService
    {
        public Task<QuickDataLoadResult> LoadNodesToCurrentMapAsync(IEnumerable<QuickDataNode> nodes, string groupName = null, bool preserveTypeBuckets = false)
        {
            return QueuedTask.Run(() =>
            {
                var result = new QuickDataLoadResult();
                var map = MapView.Active?.Map;
                if (map == null)
                {
                    result.MissingActiveMap = true;
                    return result;
                }

                var nodeList = nodes?
                    .Where(node => node != null)
                    .Select(node => node.CloneDeep())
                    .ToList()
                    ?? new List<QuickDataNode>();

                if (nodeList.Count == 0)
                {
                    return result;
                }

                var detector = new QuickDataDuplicateDetector(GetExistingKeys(map));
                if (string.IsNullOrWhiteSpace(groupName))
                {
                    foreach (var node in nodeList)
                    {
                        LoadNodeIntoContainers(node, map, map, detector, result, preserveChildrenByType: false);
                    }

                    return result;
                }

                var rootGroup = LayerFactory.Instance.CreateGroupLayer(map, -1, groupName);
                if (preserveTypeBuckets)
                {
                    foreach (var bucket in nodeList.GroupBy(QuickDataTreeBuilder.GetBucketKind).OrderBy(group => group.Key))
                    {
                        var typeGroup = LayerFactory.Instance.CreateGroupLayer(rootGroup, -1, QuickDataTreeBuilder.GetBucketDisplayName(bucket.Key));
                        foreach (var node in bucket)
                        {
                            LoadNodeIntoContainers(node, typeGroup, typeGroup, detector, result, preserveChildrenByType: true);
                        }
                    }
                }
                else
                {
                    foreach (var node in nodeList)
                    {
                        LoadNodeIntoContainers(node, rootGroup, rootGroup, detector, result, preserveChildrenByType: false);
                    }
                }

                return result;
            });
        }

        public Task<QuickDataLoadResult> LoadGroupToCurrentMapAsync(QuickDataGroup group)
        {
            var nodes = group?.Nodes ?? Enumerable.Empty<QuickDataNode>();
            return LoadNodesToCurrentMapAsync(nodes, group?.Name, preserveTypeBuckets: false);
        }

        public Task<QuickDataLoadResult> LoadBucketToCurrentMapAsync(string bucketName, IEnumerable<QuickDataNode> nodes)
        {
            return LoadNodesToCurrentMapAsync(nodes, bucketName, preserveTypeBuckets: false);
        }

        public Task<QuickDataLoadResult> LoadNodeToCurrentMapAsync(QuickDataNode node)
        {
            if (node == null)
            {
                return Task.FromResult(new QuickDataLoadResult());
            }

            if (node.NodeKind == QuickDataNodeKind.Geodatabase || node.NodeKind == QuickDataNodeKind.FeatureDataset)
            {
                return LoadNodesToCurrentMapAsync(node.Children, node.Name, preserveTypeBuckets: false);
            }

            return LoadNodesToCurrentMapAsync(new[] { node });
        }

        private static void LoadNodeIntoContainers(
            QuickDataNode node,
            ILayerContainerEdit layerContainer,
            IStandaloneTableContainerEdit tableContainer,
            QuickDataDuplicateDetector detector,
            QuickDataLoadResult result,
            bool preserveChildrenByType)
        {
            if (node.NodeKind == QuickDataNodeKind.Geodatabase)
            {
                var geodatabaseGroup = LayerFactory.Instance.CreateGroupLayer(layerContainer, -1, node.Name);
                if (preserveChildrenByType)
                {
                    foreach (var bucket in node.Children.GroupBy(QuickDataTreeBuilder.GetBucketKind).OrderBy(group => group.Key))
                    {
                        var typeGroup = LayerFactory.Instance.CreateGroupLayer(geodatabaseGroup, -1, QuickDataTreeBuilder.GetBucketDisplayName(bucket.Key));
                        foreach (var child in bucket)
                        {
                            LoadNodeIntoContainers(child, typeGroup, typeGroup, detector, result, preserveChildrenByType: false);
                        }
                    }
                }
                else
                {
                    foreach (var child in node.Children)
                    {
                        LoadNodeIntoContainers(child, geodatabaseGroup, geodatabaseGroup, detector, result, preserveChildrenByType: false);
                    }
                }

                return;
            }

            if (node.NodeKind == QuickDataNodeKind.FeatureDataset)
            {
                var featureDatasetGroup = LayerFactory.Instance.CreateGroupLayer(layerContainer, -1, node.Name);
                foreach (var child in node.Children)
                {
                    LoadNodeIntoContainers(child, featureDatasetGroup, featureDatasetGroup, detector, result, preserveChildrenByType: false);
                }

                return;
            }

            if (!detector.TryRegister(node))
            {
                result.SkippedCount++;
                return;
            }

            try
            {
                switch (node.NodeKind)
                {
                    case QuickDataNodeKind.FeatureClass:
                        var featureParams = new LayerCreationParams(new Uri(node.SourcePath, UriKind.Absolute))
                        {
                            Name = node.Name
                        };
                        LayerFactory.Instance.CreateLayer<FeatureLayer>(featureParams, layerContainer);
                        result.AddedCount++;
                        break;
                    case QuickDataNodeKind.Raster:
                        var rasterParams = new LayerCreationParams(new Uri(node.SourcePath, UriKind.Absolute))
                        {
                            Name = node.Name
                        };
                        LayerFactory.Instance.CreateLayer<RasterLayer>(rasterParams, layerContainer);
                        result.AddedCount++;
                        break;
                    case QuickDataNodeKind.Table:
                        var tableParams = new StandaloneTableCreationParams(new Uri(node.SourcePath, UriKind.Absolute))
                        {
                            Name = node.Name
                        };
                        StandaloneTableFactory.Instance.CreateStandaloneTable(tableParams, tableContainer);
                        result.AddedCount++;
                        break;
                    case QuickDataNodeKind.LayerFile:
                        LayerFactory.Instance.CreateLayer(new Uri(node.SourcePath, UriKind.Absolute), layerContainer, -1, node.Name);
                        result.AddedCount++;
                        break;
                }
            }
            catch
            {
                result.FailedCount++;
            }
        }

        private static IEnumerable<string> GetExistingKeys(Map map)
        {
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var mapMember in map.GetMapMembersAsFlattenedList())
            {
                var key = BuildExistingKey(mapMember);
                if (!string.IsNullOrWhiteSpace(key))
                {
                    keys.Add(key);
                }
            }

            return keys;
        }

        private static string BuildExistingKey(MapMember mapMember)
        {
            try
            {
                var path = mapMember.GetPath();
                if (path == null)
                {
                    return null;
                }

                var localPath = path.LocalPath;
                if (string.IsNullOrWhiteSpace(localPath))
                {
                    return null;
                }

                var extension = Path.GetExtension(localPath);
                if (extension.Equals(".lyr", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".lyrx", StringComparison.OrdinalIgnoreCase))
                {
                    return QuickDataNode.BuildUniqueKey(
                        Path.GetDirectoryName(localPath) ?? string.Empty,
                        Path.GetFileName(localPath),
                        QuickDataNodeKind.LayerFile);
                }

                var nodeKind = mapMember switch
                {
                    StandaloneTable => QuickDataNodeKind.Table,
                    RasterLayer => QuickDataNodeKind.Raster,
                    FeatureLayer => QuickDataNodeKind.FeatureClass,
                    _ => QuickDataNodeKind.FeatureClass
                };

                if (localPath.IndexOf(".gdb\\", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var gdbIndex = localPath.IndexOf(".gdb", StringComparison.OrdinalIgnoreCase) + 4;
                    var containerPath = localPath.Substring(0, gdbIndex);
                    var datasetName = localPath.Substring(gdbIndex).TrimStart('\\');
                    return QuickDataNode.BuildUniqueKey(containerPath, datasetName, nodeKind);
                }

                return QuickDataNode.BuildUniqueKey(
                    Path.GetDirectoryName(localPath) ?? string.Empty,
                    Path.GetFileName(localPath),
                    nodeKind);
            }
            catch
            {
                return null;
            }
        }
    }
}
