using XIAOFUTools.Tools.QuickAddData;

namespace XIAOFUTools.Tests.QuickAddData;

public sealed class QuickDataLibraryOrganizerTests
{
    [Fact]
    public void MoveNodeToGroup_MovesTopLevelNodeBetweenGroupsAndKeepsOrder()
    {
        var sourceGroup = new QuickDataGroup
        {
            Name = "源分组",
            Nodes =
            {
                CreateNode("A"),
                CreateNode("B")
            }
        };
        var targetGroup = new QuickDataGroup
        {
            Name = "目标分组",
            Nodes =
            {
                CreateNode("C")
            }
        };

        var moved = QuickDataLibraryOrganizer.MoveTopLevelNode(sourceGroup, sourceGroup.Nodes[1], targetGroup, 0);

        Assert.True(moved);
        Assert.Equal(["A"], sourceGroup.Nodes.Select(node => node.Name).ToArray());
        Assert.Equal(["B", "C"], targetGroup.Nodes.Select(node => node.Name).ToArray());
    }

    [Fact]
    public void MoveGroup_ReordersGroups()
    {
        var document = new QuickDataLibraryDocument
        {
            Groups =
            {
                new QuickDataGroup { Name = "一组" },
                new QuickDataGroup { Name = "二组" },
                new QuickDataGroup { Name = "三组" }
            }
        };

        var moved = QuickDataLibraryOrganizer.MoveGroup(document, document.Groups[2], 0);

        Assert.True(moved);
        Assert.Equal(["三组", "一组", "二组"], document.Groups.Select(group => group.Name).ToArray());
    }

    [Fact]
    public void MoveTopLevelNodes_MovesMultipleNodesTogether()
    {
        var sourceGroup = new QuickDataGroup
        {
            Name = "源分组",
            Nodes =
            {
                CreateNode("A"),
                CreateNode("B"),
                CreateNode("C")
            }
        };
        var targetGroup = new QuickDataGroup
        {
            Name = "目标分组",
            Nodes =
            {
                CreateNode("X")
            }
        };

        var moved = QuickDataLibraryOrganizer.MoveTopLevelNodes(sourceGroup, [sourceGroup.Nodes[0], sourceGroup.Nodes[2]], targetGroup, 1);

        Assert.True(moved);
        Assert.Equal(["B"], sourceGroup.Nodes.Select(node => node.Name).ToArray());
        Assert.Equal(["X", "A", "C"], targetGroup.Nodes.Select(node => node.Name).ToArray());
    }

    [Fact]
    public void MoveTopLevelNode_DoesNotMoveNestedDatabaseChildren()
    {
        var geodatabase = new QuickDataNode
        {
            Name = "current",
            NodeKind = QuickDataNodeKind.Geodatabase,
            ContainerPath = @"D:\Data",
            DatasetName = "current.gdb",
            SourcePath = @"D:\Data\current.gdb",
            UniqueKey = QuickDataNode.BuildUniqueKey(@"D:\Data", "current.gdb", QuickDataNodeKind.Geodatabase),
            Children =
            {
                new QuickDataNode
                {
                    Name = "BaseSet",
                    NodeKind = QuickDataNodeKind.FeatureDataset,
                    ContainerPath = @"D:\Data\current.gdb",
                    DatasetName = "BaseSet",
                    SourcePath = @"D:\Data\current.gdb\BaseSet",
                    UniqueKey = QuickDataNode.BuildUniqueKey(@"D:\Data\current.gdb", "BaseSet", QuickDataNodeKind.FeatureDataset)
                }
            }
        };
        var sourceGroup = new QuickDataGroup { Name = "源", Nodes = { geodatabase } };
        var targetGroup = new QuickDataGroup { Name = "目标" };

        var moved = QuickDataLibraryOrganizer.MoveNestedNode(sourceGroup, geodatabase.Children[0], targetGroup, 0);

        Assert.False(moved);
        Assert.Empty(targetGroup.Nodes);
        Assert.Single(geodatabase.Children);
    }

    private static QuickDataNode CreateNode(string name)
    {
        return new QuickDataNode
        {
            Name = name,
            NodeKind = QuickDataNodeKind.FeatureClass,
            GeometryKind = QuickDataGeometryKind.Polygon,
            ContainerPath = @"D:\Data",
            DatasetName = $"{name}.shp",
            SourcePath = $@"D:\Data\{name}.shp",
            UniqueKey = QuickDataNode.BuildUniqueKey(@"D:\Data", $"{name}.shp", QuickDataNodeKind.FeatureClass)
        };
    }
}
