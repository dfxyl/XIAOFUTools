using XIAOFUTools.Features.Cartography.MapSeriesExport.Infrastructure;

namespace XIAOFUTools.Tests.MapSeriesExport;

public sealed class MapSeriesOutputFolderStoreTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Store_CreatesMissingExportDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xiaofutools-map-series-{Guid.NewGuid():N}");
        var output = Path.Combine(root, "nested", "output");
        var store = new MapSeriesOutputFolderStore();

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
