using System.Collections.Generic;
using System.Linq;

namespace XIAOFUTools.Features.General.QuickAddData
{
    public interface IQuickDataLegacyHydrationImportService
    {
        QuickDataNode? ImportGeodatabase(string geodatabasePath);
    }

    public static class QuickDataLibraryHydrator
    {
        public static bool HydrateGeodatabases(QuickDataLibraryDocument document, IQuickDataLegacyHydrationImportService importService)
        {
            if (document?.Groups == null || importService == null)
            {
                return false;
            }

            var changed = false;
            foreach (var geodatabaseNode in document.Groups.SelectMany(group => EnumerateNodes(group.Nodes)).Where(IsLegacyGeodatabaseNode))
            {
                var imported = importService.ImportGeodatabase(geodatabaseNode.SourcePath);
                if (imported?.Children == null || imported.Children.Count == 0)
                {
                    continue;
                }

                geodatabaseNode.Children = imported.Children.Select(child => child.CloneDeep()).ToList();
                geodatabaseNode.Normalize();
                changed = true;
            }

            return changed;
        }

        private static bool IsLegacyGeodatabaseNode(QuickDataNode node)
        {
            return node != null
                && node.NodeKind == QuickDataNodeKind.Geodatabase
                && (node.Children == null || node.Children.Count == 0);
        }

        private static IEnumerable<QuickDataNode> EnumerateNodes(IEnumerable<QuickDataNode> nodes)
        {
            foreach (var node in nodes ?? Enumerable.Empty<QuickDataNode>())
            {
                yield return node;

                foreach (var child in EnumerateNodes(node.Children))
                {
                    yield return child;
                }
            }
        }
    }
}
