using System.Collections.Generic;

namespace XIAOFUTools.Features.General.QuickAddData
{
    public static class QuickDataDragPathCollector
    {
        public static IReadOnlyList<string> Collect(QuickDataNode node)
        {
            var results = new List<string>();
            CollectCore(node, results);
            return results;
        }

        private static void CollectCore(QuickDataNode node, ICollection<string> results)
        {
            if (node == null)
            {
                return;
            }

            if (node.NodeKind == QuickDataNodeKind.Geodatabase || node.NodeKind == QuickDataNodeKind.FeatureDataset)
            {
                foreach (var child in node.Children)
                {
                    CollectCore(child, results);
                }

                return;
            }

            if (!string.IsNullOrWhiteSpace(node.SourcePath))
            {
                results.Add(node.SourcePath);
            }
        }
    }
}
