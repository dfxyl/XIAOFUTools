using XIAOFUTools.Features.General.QuickAddData;

namespace XIAOFUTools.Tests.QuickAddData;

public sealed class QuickDataLibraryHydratorTests
{
    [Fact]
    public void HydrateGeodatabases_FillsMissingChildrenForExistingLegacyNodes()
    {
        var document = new QuickDataLibraryDocument
        {
            Groups =
            {
                new QuickDataGroup
                {
                    Name = "常用",
                    Nodes =
                    {
                        new QuickDataNode
                        {
                            Name = "legacy",
                            NodeKind = QuickDataNodeKind.Geodatabase,
                            ContainerPath = @"D:\Data",
                            DatasetName = "legacy.gdb",
                            SourcePath = @"D:\Data\legacy.gdb",
                            CoordinateSystem = "文件地理数据库",
                            UniqueKey = QuickDataNode.BuildUniqueKey(@"D:\Data", "legacy.gdb", QuickDataNodeKind.Geodatabase)
                        }
                    }
                }
            }
        };

        var service = new FakeImportService(new QuickDataNode
        {
            Name = "legacy",
            NodeKind = QuickDataNodeKind.Geodatabase,
            ContainerPath = @"D:\Data",
            DatasetName = "legacy.gdb",
            SourcePath = @"D:\Data\legacy.gdb",
            CoordinateSystem = "文件地理数据库",
            UniqueKey = QuickDataNode.BuildUniqueKey(@"D:\Data", "legacy.gdb", QuickDataNodeKind.Geodatabase),
            Children =
            {
                new QuickDataNode
                {
                    Name = "BaseSet",
                    NodeKind = QuickDataNodeKind.FeatureDataset,
                    ContainerPath = @"D:\Data\legacy.gdb",
                    DatasetName = "BaseSet",
                    SourcePath = @"D:\Data\legacy.gdb\BaseSet",
                    CoordinateSystem = "CGCS2000",
                    UniqueKey = QuickDataNode.BuildUniqueKey(@"D:\Data\legacy.gdb", "BaseSet", QuickDataNodeKind.FeatureDataset),
                    Children =
                    {
                        new QuickDataNode
                        {
                            Name = "DLTB",
                            NodeKind = QuickDataNodeKind.FeatureClass,
                            GeometryKind = QuickDataGeometryKind.Polygon,
                            ContainerPath = @"D:\Data\legacy.gdb",
                            DatasetName = @"BaseSet\DLTB",
                            SourcePath = @"D:\Data\legacy.gdb\BaseSet\DLTB",
                            CoordinateSystem = "CGCS2000",
                            UniqueKey = QuickDataNode.BuildUniqueKey(@"D:\Data\legacy.gdb", @"BaseSet\DLTB", QuickDataNodeKind.FeatureClass)
                        }
                    }
                }
            }
        });

        var changed = QuickDataLibraryHydrator.HydrateGeodatabases(document, service);

        Assert.True(changed);
        var geodatabase = document.Groups[0].Nodes[0];
        Assert.Single(geodatabase.Children);
        Assert.Equal(QuickDataNodeKind.FeatureDataset, geodatabase.Children[0].NodeKind);
        Assert.Equal("DLTB", geodatabase.Children[0].Children[0].Name);
    }

    [Fact]
    public void HydrateGeodatabases_SkipsNodesThatAlreadyHaveChildren()
    {
        var node = new QuickDataNode
        {
            Name = "legacy",
            NodeKind = QuickDataNodeKind.Geodatabase,
            ContainerPath = @"D:\Data",
            DatasetName = "legacy.gdb",
            SourcePath = @"D:\Data\legacy.gdb",
            CoordinateSystem = "文件地理数据库",
            UniqueKey = QuickDataNode.BuildUniqueKey(@"D:\Data", "legacy.gdb", QuickDataNodeKind.Geodatabase),
            Children =
            {
                new QuickDataNode
                {
                    Name = "existing",
                    NodeKind = QuickDataNodeKind.Table,
                    ContainerPath = @"D:\Data\legacy.gdb",
                    DatasetName = "existing",
                    SourcePath = @"D:\Data\legacy.gdb\existing",
                    CoordinateSystem = "无",
                    UniqueKey = QuickDataNode.BuildUniqueKey(@"D:\Data\legacy.gdb", "existing", QuickDataNodeKind.Table)
                }
            }
        };

        var document = new QuickDataLibraryDocument
        {
            Groups = { new QuickDataGroup { Name = "常用", Nodes = { node } } }
        };

        var changed = QuickDataLibraryHydrator.HydrateGeodatabases(document, new FakeImportService(null));

        Assert.False(changed);
        Assert.Single(node.Children);
        Assert.Equal("existing", node.Children[0].Name);
    }

    private sealed class FakeImportService(QuickDataNode? resultNode) : IQuickDataLegacyHydrationImportService
    {
        public QuickDataNode? ImportGeodatabase(string geodatabasePath)
        {
            _ = geodatabasePath;
            return resultNode?.CloneDeep();
        }
    }
}
