using XIAOFUTools.Features.DataManagement.RangeClipTool.Infrastructure;

namespace XIAOFUTools.Tests.RangeClipTool;

public sealed class RangeClipOutputFolderStoreTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void Store_CreatesAndValidatesOutputDirectories()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xiaofutools-range-clip-{Guid.NewGuid():N}");
        var output = Path.Combine(root, "result", "range-a");
        var store = new RangeClipOutputFolderStore();

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
