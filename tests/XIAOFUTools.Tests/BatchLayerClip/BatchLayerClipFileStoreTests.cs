using XIAOFUTools.Features.DataManagement.BatchLayerClip.Infrastructure;

namespace XIAOFUTools.Tests.BatchLayerClip;

public sealed class BatchLayerClipFileStoreTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void Store_CreatesAndValidatesOutputDirectories()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xiaofutools-layer-clip-{Guid.NewGuid():N}");
        var output = Path.Combine(root, "groups", "first");
        var store = new BatchLayerClipFileStore();

        try
        {
            store.EnsureDirectory(output);

            Assert.True(store.DirectoryExists(output));
            Assert.False(store.DirectoryExists(Path.Combine(root, "missing")));
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
