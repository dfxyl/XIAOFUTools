using XIAOFUTools.Features.Cartography.ExportLayout.Infrastructure;

namespace XIAOFUTools.Tests.ExportLayout;

public sealed class LayoutExportFileStoreTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Store_CreatesMissingOutputDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xiaofutools-layout-export-{Guid.NewGuid():N}");
        var output = Path.Combine(root, "nested", "output");
        var store = new LayoutExportFileStore();

        try
        {
            await store.EnsureOutputDirectoryAsync(output, CancellationToken.None);
            Assert.True(Directory.Exists(output));
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
