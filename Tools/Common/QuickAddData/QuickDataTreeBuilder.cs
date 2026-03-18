#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace XIAOFUTools.Tools.QuickAddData
{
    public enum QuickDataTreeNodeKind
    {
        Group,
        TypeBucket,
        DataNode
    }

    public enum QuickDataBucketKind
    {
        Point,
        Polyline,
        Polygon,
        Multipoint,
        Table,
        Raster,
        Geodatabase,
        LayerFile
    }

    public sealed class QuickDataTreeNode
    {
        public string DisplayName { get; set; } = string.Empty;

        public QuickDataTreeNodeKind NodeKind { get; set; }

        public QuickDataBucketKind? BucketKind { get; set; }

        public QuickDataGroup? Group { get; set; }

        public QuickDataNode? DataNode { get; set; }

        public List<QuickDataTreeNode> Children { get; set; } = new List<QuickDataTreeNode>();
    }

    public static class QuickDataTreeBuilder
    {
        public static IReadOnlyList<QuickDataTreeNode> Build(QuickDataLibraryDocument document)
        {
            var groups = new List<QuickDataTreeNode>();
            foreach (var group in document?.Groups ?? Enumerable.Empty<QuickDataGroup>())
            {
                var groupNode = new QuickDataTreeNode
                {
                    DisplayName = group.Name,
                    NodeKind = QuickDataTreeNodeKind.Group,
                    Group = group
                };

                foreach (var dataNode in group.Nodes.OrderBy(node => node.Name))
                {
                    groupNode.Children.Add(BuildDataNode(dataNode));
                }

                groups.Add(groupNode);
            }

            return groups;
        }

        public static QuickDataBucketKind GetBucketKind(QuickDataNode node)
        {
            return node.NodeKind switch
            {
                QuickDataNodeKind.Table => QuickDataBucketKind.Table,
                QuickDataNodeKind.Raster => QuickDataBucketKind.Raster,
                QuickDataNodeKind.Geodatabase => QuickDataBucketKind.Geodatabase,
                QuickDataNodeKind.FeatureDataset => QuickDataBucketKind.Geodatabase,
                QuickDataNodeKind.LayerFile => QuickDataBucketKind.LayerFile,
                _ when node.GeometryKind == QuickDataGeometryKind.Point => QuickDataBucketKind.Point,
                _ when node.GeometryKind == QuickDataGeometryKind.Polyline => QuickDataBucketKind.Polyline,
                _ when node.GeometryKind == QuickDataGeometryKind.Polygon => QuickDataBucketKind.Polygon,
                _ when node.GeometryKind == QuickDataGeometryKind.Multipoint => QuickDataBucketKind.Multipoint,
                _ => QuickDataBucketKind.Geodatabase
            };
        }

        public static string GetBucketDisplayName(QuickDataBucketKind bucketKind)
        {
            return bucketKind switch
            {
                QuickDataBucketKind.Point => "点",
                QuickDataBucketKind.Polyline => "线",
                QuickDataBucketKind.Polygon => "面",
                QuickDataBucketKind.Multipoint => "多点",
                QuickDataBucketKind.Table => "表",
                QuickDataBucketKind.Raster => "栅格",
                QuickDataBucketKind.Geodatabase => "数据库",
                QuickDataBucketKind.LayerFile => "图层文件",
                _ => "其他"
            };
        }

        private static QuickDataTreeNode BuildDataNode(QuickDataNode dataNode)
        {
            var node = new QuickDataTreeNode
            {
                DisplayName = dataNode.Name,
                NodeKind = QuickDataTreeNodeKind.DataNode,
                DataNode = dataNode
            };

            if (dataNode.Children.Count > 0)
            {
                foreach (var child in dataNode.Children.OrderBy(item => item.Name))
                {
                    node.Children.Add(BuildDataNode(child));
                }
            }

            return node;
        }
    }
}
