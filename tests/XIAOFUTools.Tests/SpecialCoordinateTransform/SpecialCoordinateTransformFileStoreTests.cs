using XIAOFUTools.Features.Conversion.SpecialCoordinateTransform.Infrastructure;

namespace XIAOFUTools.Tests.SpecialCoordinateTransform;

public sealed class SpecialCoordinateTransformFileStoreTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void Store_ScansBatchInputsAndManagesOutputDirectories()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xiaofutools-coordinate-transform-{Guid.NewGuid():N}");
        var nested = Path.Combine(root, "nested");
        var output = Path.Combine(root, "output");
        var geodatabase = Path.Combine(nested, "source.gdb");
        var store = new SpecialCoordinateTransformFileStore();

        try
        {
            Directory.CreateDirectory(nested);
            File.WriteAllText(Path.Combine(root, "first.shp"), string.Empty);
            File.WriteAllText(Path.Combine(nested, "second.shp"), string.Empty);
            Directory.CreateDirectory(geodatabase);

            Assert.Single(store.EnumeratePaths(
                root,
                includeSubfolders: false,
                searchPattern: "*.shp",
                searchDirectories: false));
            Assert.Equal(2, store.EnumeratePaths(
                root,
                includeSubfolders: true,
                searchPattern: "*.shp",
                searchDirectories: false).Count);
            Assert.Single(store.EnumeratePaths(
                root,
                includeSubfolders: true,
                searchPattern: "*.gdb",
                searchDirectories: true));

            store.EnsureDirectory(output);
            Assert.True(store.DirectoryExists(output));
            store.DeleteDirectoryIfExists(output, recursive: true);
            Assert.False(store.DirectoryExists(output));
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
