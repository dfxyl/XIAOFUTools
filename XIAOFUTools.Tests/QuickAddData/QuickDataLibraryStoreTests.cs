using System.Text.Json;
using XIAOFUTools.Tools.QuickAddData;

namespace XIAOFUTools.Tests.QuickAddData;

public sealed class QuickDataLibraryStoreTests
{
    [Fact]
    public void Load_WhenFileMissing_ReturnsEmptyDocument()
    {
        var filePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "QuickDataLibrary.json");
        var store = new QuickDataLibraryStore(filePath);

        var document = store.Load();

        Assert.Equal(QuickDataLibraryStore.CurrentVersion, document.Version);
        Assert.Empty(document.Groups);
        Assert.NotNull(document.TreeViewState);
        Assert.Empty(document.TreeViewState.ExpandedKeys);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsGroupsAndNodes()
    {
        var root = CreateTempDirectory();
        try
        {
            var filePath = Path.Combine(root, "QuickDataLibrary.json");
            var store = new QuickDataLibraryStore(filePath);
            var document = new QuickDataLibraryDocument
            {
                Groups =
                {
                    new QuickDataGroup
                    {
                        Name = "基础数据",
                        Nodes =
                        {
                            new QuickDataNode
                            {
                                Name = "道路",
                                NodeKind = QuickDataNodeKind.FeatureClass,
                                GeometryKind = QuickDataGeometryKind.Polyline,
                                ContainerPath = @"D:\Data",
                                DatasetName = "roads.shp",
                                SourcePath = @"D:\Data\roads.shp",
                                CoordinateSystem = "WGS 1984",
                                UniqueKey = QuickDataNode.BuildUniqueKey(@"D:\Data", "roads.shp", QuickDataNodeKind.FeatureClass)
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
                                        Name = "宗地",
                                        NodeKind = QuickDataNodeKind.FeatureClass,
                                        GeometryKind = QuickDataGeometryKind.Polygon,
                                        ContainerPath = @"D:\Data\current.gdb",
                                        DatasetName = "ZD",
                                        SourcePath = @"D:\Data\current.gdb\ZD",
                                        CoordinateSystem = "CGCS2000",
                                        UniqueKey = QuickDataNode.BuildUniqueKey(@"D:\Data\current.gdb", "ZD", QuickDataNodeKind.FeatureClass)
                                    }
                                }
                            }
                        }
                    }
                }
            };

            store.Save(document);
            var loaded = store.Load();

            var group = Assert.Single(loaded.Groups);
            Assert.Equal("基础数据", group.Name);
            Assert.Equal(2, group.Nodes.Count);
            Assert.Equal(QuickDataNodeKind.FeatureClass, group.Nodes[0].NodeKind);
            Assert.Single(group.Nodes[1].Children);
            Assert.Equal("宗地", group.Nodes[1].Children[0].Name);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void Load_WhenJsonInvalid_FallsBackToEmptyDocument()
    {
        var root = CreateTempDirectory();
        try
        {
            var filePath = Path.Combine(root, "QuickDataLibrary.json");
            File.WriteAllText(filePath, "{invalid json");
            var store = new QuickDataLibraryStore(filePath);

            var loaded = store.Load();

            Assert.Equal(QuickDataLibraryStore.CurrentVersion, loaded.Version);
            Assert.Empty(loaded.Groups);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void Load_WhenFutureVersionFileExists_KeepsGroups()
    {
        var root = CreateTempDirectory();
        try
        {
            var filePath = Path.Combine(root, "QuickDataLibrary.json");
            var payload = JsonSerializer.Serialize(new QuickDataLibraryDocument
            {
                Version = QuickDataLibraryStore.CurrentVersion + 10,
                Groups =
                {
                    new QuickDataGroup
                    {
                        Name = "未来版本",
                        Nodes =
                        {
                            new QuickDataNode
                            {
                                Name = "样式",
                                NodeKind = QuickDataNodeKind.LayerFile,
                                GeometryKind = QuickDataGeometryKind.None,
                                ContainerPath = @"D:\Style",
                                DatasetName = "style.lyrx",
                                SourcePath = @"D:\Style\style.lyrx",
                                UniqueKey = QuickDataNode.BuildUniqueKey(@"D:\Style", "style.lyrx", QuickDataNodeKind.LayerFile)
                            }
                        }
                    }
                }
            });
            File.WriteAllText(filePath, payload);
            var store = new QuickDataLibraryStore(filePath);

            var loaded = store.Load();

            Assert.Single(loaded.Groups);
            Assert.Equal("未来版本", loaded.Groups[0].Name);
            Assert.Equal(QuickDataNodeKind.LayerFile, loaded.Groups[0].Nodes[0].NodeKind);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void SaveAndLoad_RoundTripsTreeViewExpandedKeys()
    {
        var root = CreateTempDirectory();
        try
        {
            var filePath = Path.Combine(root, "QuickDataLibrary.json");
            var store = new QuickDataLibraryStore(filePath);
            var document = new QuickDataLibraryDocument
            {
                TreeViewState = new QuickDataTreeViewState
                {
                    ExpandedKeys =
                    {
                        "group:base",
                        string.Empty,
                        "data:roads",
                        "group:base"
                    }
                }
            };

            store.Save(document);
            var loaded = store.Load();

            Assert.Equal(["group:base", "data:roads"], loaded.TreeViewState.ExpandedKeys);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
    }
}
