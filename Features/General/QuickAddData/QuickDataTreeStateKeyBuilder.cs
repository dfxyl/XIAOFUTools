namespace XIAOFUTools.Features.General.QuickAddData
{
    public static class QuickDataTreeStateKeyBuilder
    {
        public static string Build(
            QuickDataTreeNodeKind nodeKind,
            string displayName,
            QuickDataGroup? group,
            QuickDataNode? dataNode,
            QuickDataBucketKind? bucketKind,
            string parentKey)
        {
            return nodeKind switch
            {
                QuickDataTreeNodeKind.Group when group != null => $"group:{group.Id}",
                QuickDataTreeNodeKind.TypeBucket => $"bucket:{parentKey}:{bucketKind}:{displayName}",
                QuickDataTreeNodeKind.DataNode when dataNode != null && !string.IsNullOrWhiteSpace(dataNode.UniqueKey) =>
                    $"data:{dataNode.UniqueKey}",
                QuickDataTreeNodeKind.DataNode when dataNode != null =>
                    $"data:{dataNode.NodeKind}:{dataNode.ContainerPath}:{dataNode.DatasetName}",
                _ => $"node:{parentKey}:{nodeKind}:{displayName}"
            };
        }
    }
}
