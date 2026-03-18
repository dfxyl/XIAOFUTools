using System;
using System.Collections.Generic;
using System.Linq;

namespace XIAOFUTools.Tools.QuickAddData
{
    public static class QuickDataLibraryOrganizer
    {
        public static bool MoveGroup(QuickDataLibraryDocument document, QuickDataGroup draggedGroup, int targetIndex)
        {
            if (document?.Groups == null || draggedGroup == null)
            {
                return false;
            }

            var sourceIndex = document.Groups.IndexOf(draggedGroup);
            if (sourceIndex < 0)
            {
                return false;
            }

            targetIndex = Math.Max(0, Math.Min(targetIndex, document.Groups.Count - 1));
            if (sourceIndex == targetIndex)
            {
                return false;
            }

            document.Groups.RemoveAt(sourceIndex);
            if (sourceIndex < targetIndex)
            {
                targetIndex--;
            }

            document.Groups.Insert(targetIndex, draggedGroup);
            return true;
        }

        public static bool MoveTopLevelNode(QuickDataGroup sourceGroup, QuickDataNode draggedNode, QuickDataGroup targetGroup, int targetIndex)
        {
            if (sourceGroup?.Nodes == null || targetGroup?.Nodes == null || draggedNode == null)
            {
                return false;
            }

            var sourceIndex = sourceGroup.Nodes.IndexOf(draggedNode);
            if (sourceIndex < 0)
            {
                return false;
            }

            sourceGroup.Nodes.RemoveAt(sourceIndex);

            targetIndex = Math.Max(0, Math.Min(targetIndex, targetGroup.Nodes.Count));
            if (ReferenceEquals(sourceGroup, targetGroup) && sourceIndex < targetIndex)
            {
                targetIndex--;
            }

            targetGroup.Nodes.Insert(targetIndex, draggedNode);
            return true;
        }

        public static bool MoveTopLevelNodes(QuickDataGroup sourceGroup, IEnumerable<QuickDataNode> draggedNodes, QuickDataGroup targetGroup, int targetIndex)
        {
            if (sourceGroup?.Nodes == null || targetGroup?.Nodes == null || draggedNodes == null)
            {
                return false;
            }

            var nodesToMove = draggedNodes
                .Where(node => node != null && sourceGroup.Nodes.Contains(node))
                .Distinct()
                .ToList();

            if (nodesToMove.Count == 0)
            {
                return false;
            }

            if (ReferenceEquals(sourceGroup, targetGroup))
            {
                targetIndex -= sourceGroup.Nodes.Take(targetIndex).Count(node => nodesToMove.Contains(node));
            }

            foreach (var node in nodesToMove)
            {
                sourceGroup.Nodes.Remove(node);
            }

            targetIndex = Math.Max(0, Math.Min(targetIndex, targetGroup.Nodes.Count));
            for (var i = 0; i < nodesToMove.Count; i++)
            {
                targetGroup.Nodes.Insert(targetIndex + i, nodesToMove[i]);
            }

            return true;
        }

        public static bool MoveNestedNode(QuickDataGroup sourceGroup, QuickDataNode draggedNode, QuickDataGroup targetGroup, int targetIndex)
        {
            _ = sourceGroup;
            _ = draggedNode;
            _ = targetGroup;
            _ = targetIndex;
            return false;
        }

        public static int GetInsertionIndex(QuickDataGroup targetGroup, QuickDataNode targetNode)
        {
            if (targetGroup?.Nodes == null || targetNode == null)
            {
                return 0;
            }

            var index = targetGroup.Nodes.IndexOf(targetNode);
            return index < 0 ? targetGroup.Nodes.Count : index;
        }
    }
}
