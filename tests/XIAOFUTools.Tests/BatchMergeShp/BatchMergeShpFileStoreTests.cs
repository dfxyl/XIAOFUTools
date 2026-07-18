using XIAOFUTools.Features.DataManagement.BatchMergeShp.Infrastructure;

namespace XIAOFUTools.Tests.BatchMergeShp;

public sealed class BatchMergeShpFileStoreTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void Store_ScansShapefilesAndCreatesOutputDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xiaofutools-merge-shp-{Guid.NewGuid():N}");
        var nested = Path.Combine(root, "nested");
        var output = Path.Combine(root, "output");
        var store = new BatchMergeShpFileStore();

        try
        {
            Directory.CreateDirectory(nested);
            File.WriteAllText(Path.Combine(root, "first.shp"), string.Empty);
            File.WriteAllText(Path.Combine(nested, "second.shp"), string.Empty);

            Assert.Single(store.EnumerateShapefiles(root, includeSubfolders: false));
            Assert.Equal(2, store.EnumerateShapefiles(root, includeSubfolders: true).Count);

            store.EnsureDirectory(output);
            Assert.True(store.DirectoryExists(output));
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
