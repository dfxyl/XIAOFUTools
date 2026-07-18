using XIAOFUTools.Features.DataManagement.ShapefileBuilder.Infrastructure;

namespace XIAOFUTools.Tests.ShapefileBuilder;

public sealed class ShapefileBuilderFileStoreTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void Store_CreatesOutputDirectoryAndCopiesTemplate()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xiaofutools-shapefile-builder-{Guid.NewGuid():N}");
        var source = Path.Combine(root, "template.xls");
        var output = Path.Combine(root, "output");
        var destination = Path.Combine(output, "template.xls");
        var store = new ShapefileBuilderFileStore();

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(source, "template");
            store.EnsureDirectory(output);
            store.CopyFile(source, destination);

            Assert.True(store.FileExists(destination));
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
