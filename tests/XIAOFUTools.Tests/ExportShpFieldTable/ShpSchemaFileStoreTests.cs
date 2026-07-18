using XIAOFUTools.Features.DataManagement.ExportShpFieldTable.Infrastructure;

namespace XIAOFUTools.Tests.ExportShpFieldTable;

public sealed class ShpSchemaFileStoreTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void Store_FindsTopLevelShapefilesAndCreatesOutputDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xiaofutools-shp-schema-{Guid.NewGuid():N}");
        var nested = Path.Combine(root, "nested");
        var outputPath = Path.Combine(root, "output", "schema.xlsx");
        var store = new ShpSchemaFileStore();

        try
        {
            Directory.CreateDirectory(nested);
            File.WriteAllText(Path.Combine(root, "first.shp"), string.Empty);
            File.WriteAllText(Path.Combine(nested, "second.shp"), string.Empty);

            Assert.Single(store.GetTopLevelShapefiles(root));
            store.EnsureOutputDirectory(outputPath);
            Assert.True(store.DirectoryExists(Path.GetDirectoryName(outputPath)!));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
