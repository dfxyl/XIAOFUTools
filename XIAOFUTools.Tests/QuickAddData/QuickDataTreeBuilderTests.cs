using XIAOFUTools.Tools.QuickAddData;

namespace XIAOFUTools.Tests.QuickAddData;

public sealed class QuickDataTreeBuilderTests
{
    [Fact]
    public void Build_AttachesDataNodesDirectlyUnderGroupAndKeepsDatabaseChildren()
    {
        var document = new QuickDataLibraryDocument
        {
            Groups =
            {
                new QuickDataGroup
                {
                    Name = "项目一",
                    Nodes =
                    {
                        new QuickDataNode
                        {
                            Name = "宗地点",
                            NodeKind = QuickDataNodeKind.FeatureClass,
                            GeometryKind = QuickDataGeometryKind.Point,
                            ContainerPath = @"D:\Data",
                            DatasetName = "point.shp",
                            SourcePath = @"D:\Data\point.shp",
                            UniqueKey = QuickDataNode.BuildUniqueKey(@"D:\Data", "point.shp", QuickDataNodeKind.FeatureClass)
                        },
                        new QuickDataNode
                        {
                            Name = "影像",
                            NodeKind = QuickDataNodeKind.Raster,
                            GeometryKind = QuickDataGeometryKind.None,
                            ContainerPath = @"D:\Data",
                            DatasetName = "image.tif",
                            SourcePath = @"D:\Data\image.tif",
                            UniqueKey = QuickDataNode.BuildUniqueKey(@"D:\Data", "image.tif", QuickDataNodeKind.Raster)
                        },
                        new QuickDataNode
                        {
                            Name = "现状库",
                            NodeKind = QuickDataNodeKind.Geodatabase,
                            GeometryKind = QuickDataGeometryKind.None,
                            ContainerPath = @"D:\Data",
                            DatasetName = "current.gdb",
                            SourcePath = @"D:\Data\current.gdb",
                            UniqueKey = QuickDataNode.BuildUniqueKey(@"D:\Data", "current.gdb", QuickDataNodeKind.Geodatabase),
                            Children =
                            {
                                new QuickDataNode
                                {
                                    Name = "宗地表",
                                    NodeKind = QuickDataNodeKind.Table,
                                    GeometryKind = QuickDataGeometryKind.None,
                                    ContainerPath = @"D:\Data\current.gdb",
                                    DatasetName = "ZD_BZ",
                                    SourcePath = @"D:\Data\current.gdb\ZD_BZ",
                                    UniqueKey = QuickDataNode.BuildUniqueKey(@"D:\Data\current.gdb", "ZD_BZ", QuickDataNodeKind.Table)
                                }
                            }
                        }
                    }
                }
            }
        };

        var tree = QuickDataTreeBuilder.Build(document);

        var group = Assert.Single(tree);
        Assert.Equal("项目一", group.DisplayName);
        Assert.Equal(["现状库", "影像", "宗地点"], group.Children.Select(child => child.DisplayName).ToArray());

        var databaseNode = group.Children.Single(child => child.DisplayName == "现状库");
        Assert.Equal("宗地表", Assert.Single(databaseNode.Children).DisplayName);
    }
}
