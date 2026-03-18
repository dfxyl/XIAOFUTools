using XIAOFUTools.Tools.QuickAddData;

namespace XIAOFUTools.Tests.QuickAddData;

public sealed class QuickDataImportServiceTests
{
    [Fact]
    public void ImportPaths_IdentifiesSupportedTypesAndExpandsGeodatabase()
    {
        var root = CreateTempDirectory();
        try
        {
            CreateShapefile(Path.Combine(root, "roads.shp"), 3);
            File.WriteAllText(Path.Combine(root, "roads.prj"), "GEOGCS[\"GCS_WGS_1984\"]");
            File.WriteAllText(Path.Combine(root, "style.lyrx"), "{}");
            File.WriteAllBytes(Path.Combine(root, "imagery.tif"), new byte[] { 1, 2, 3 });
            File.WriteAllText(Path.Combine(root, "imagery.prj"), "PROJCS[\"CGCS2000_3_Degree_GK_CM_114E\"]");

            var geodatabasePath = Path.Combine(root, "current.gdb");
            Directory.CreateDirectory(geodatabasePath);

            var inspector = new FakeGeodatabaseInspector(
            [
                new QuickDataGeodatabaseChild
                {
                    Name = "基础地物",
                    DatasetPath = "BaseSet",
                    NodeKind = QuickDataNodeKind.FeatureDataset,
                    GeometryKind = QuickDataGeometryKind.None,
                    CoordinateSystem = "CGCS2000"
                },
                new QuickDataGeodatabaseChild
                {
                    Name = "ZD",
                    ParentDatasetPath = "BaseSet",
                    DatasetPath = "BaseSet\\ZD",
                    NodeKind = QuickDataNodeKind.FeatureClass,
                    GeometryKind = QuickDataGeometryKind.Polygon,
                    CoordinateSystem = "CGCS2000"
                },
                new QuickDataGeodatabaseChild
                {
                    Name = "ZD_BZ",
                    NodeKind = QuickDataNodeKind.Table,
                    GeometryKind = QuickDataGeometryKind.None,
                    CoordinateSystem = "无"
                }
            ]);

            var service = new QuickDataImportService(inspector);

            var nodes = service.ImportPaths([root], new QuickDataImportOptions());

            Assert.Equal(4, nodes.Count);

            var shapefile = Assert.Single(nodes, node => node.NodeKind == QuickDataNodeKind.FeatureClass);
            Assert.Equal("线", QuickDataTreeBuilder.GetBucketDisplayName(QuickDataTreeBuilder.GetBucketKind(shapefile)));
            Assert.Equal(QuickDataGeometryKind.Polyline, shapefile.GeometryKind);
            Assert.Equal("WGS 1984", shapefile.CoordinateSystem);

            var layerFile = Assert.Single(nodes, node => node.NodeKind == QuickDataNodeKind.LayerFile);
            Assert.Equal("style", layerFile.Name);

            var raster = Assert.Single(nodes, node => node.NodeKind == QuickDataNodeKind.Raster);
            Assert.Equal("CGCS2000_3_Degree_GK_CM_114E", raster.CoordinateSystem);

            var geodatabase = Assert.Single(nodes, node => node.NodeKind == QuickDataNodeKind.Geodatabase);
            Assert.Equal("current", geodatabase.Name);
            Assert.Equal(2, geodatabase.Children.Count);
            Assert.Contains(geodatabase.Children, child => child.NodeKind == QuickDataNodeKind.FeatureDataset && child.Name == "基础地物");
            Assert.Contains(geodatabase.Children, child => child.NodeKind == QuickDataNodeKind.Table && child.Name == "ZD_BZ");

            var featureDataset = Assert.Single(geodatabase.Children, child => child.NodeKind == QuickDataNodeKind.FeatureDataset);
            var featureClass = Assert.Single(featureDataset.Children);
            Assert.Equal("ZD", featureClass.Name);
            Assert.Equal(QuickDataNodeKind.FeatureClass, featureClass.NodeKind);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void ImportPaths_DeduplicatesNodesByUniqueKey()
    {
        var root = CreateTempDirectory();
        try
        {
            var shapefilePath = Path.Combine(root, "roads.shp");
            CreateShapefile(shapefilePath, 5);

            var service = new QuickDataImportService(new FakeGeodatabaseInspector([]));

            var nodes = service.ImportPaths([shapefilePath, shapefilePath], new QuickDataImportOptions());

            Assert.Single(nodes);
            Assert.Equal(QuickDataGeometryKind.Polygon, nodes[0].GeometryKind);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static void CreateShapefile(string path, int shapeType)
    {
        var header = new byte[100];
        Array.Copy(BitConverter.GetBytes(shapeType), 0, header, 32, 4);
        File.WriteAllBytes(path, header);
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

    private sealed class FakeGeodatabaseInspector(IEnumerable<QuickDataGeodatabaseChild> children) : IQuickDataGeodatabaseInspector
    {
        private readonly IReadOnlyList<QuickDataGeodatabaseChild> _children = children.ToList();

        public IReadOnlyList<QuickDataGeodatabaseChild> Inspect(string geodatabasePath)
        {
            return _children;
        }
    }
}
