using XIAOFUTools.Features.DataManagement.OvertureLoader.Infrastructure;

namespace XIAOFUTools.Tests.OvertureLoader;

public sealed class FileSystemOvertureDataFolderStoreTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Store_DetectsParquetAndDeletesThemeFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xiaofutools-overture-{Guid.NewGuid():N}");
        var themeFolder = Path.Combine(root, "land");
        Directory.CreateDirectory(themeFolder);
        await File.WriteAllTextAsync(Path.Combine(themeFolder, "part.parquet"), "data");
        var store = new FileSystemOvertureDataFolderStore();

        try
        {
            Assert.True(await store.ExistsAsync(themeFolder, CancellationToken.None));
            Assert.True(await store.ContainsParquetFilesAsync(themeFolder, CancellationToken.None));

            await store.DeleteAsync(themeFolder, CancellationToken.None);

            Assert.False(Directory.Exists(themeFolder));
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
