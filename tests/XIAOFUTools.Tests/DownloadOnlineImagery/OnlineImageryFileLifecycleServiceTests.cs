using XIAOFUTools.Features.General.DownloadOnlineImagery.Infrastructure;

namespace XIAOFUTools.Tests.DownloadOnlineImagery;

public sealed class OnlineImageryFileLifecycleServiceTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Service_CleansRelatedAndTemporaryFilesWithoutDeletingUnrelatedOutput()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xiaofutools-online-imagery-{Guid.NewGuid():N}");
        var imagePath = Path.Combine(root, "image.tif");
        var worldFilePath = Path.Combine(root, "image.tfw");
        var auxiliaryPath = Path.Combine(root, "image.tif.aux.xml");
        var temporaryPath = Path.Combine(root, "temp_grid_001.tmp");
        var unrelatedPath = Path.Combine(root, "keep.tif");
        var service = new OnlineImageryFileLifecycleService();

        try
        {
            Assert.True(await service.EnsureDirectoryAsync(root, CancellationToken.None));
            await File.WriteAllTextAsync(imagePath, "image");
            await File.WriteAllTextAsync(worldFilePath, "world");
            await File.WriteAllTextAsync(auxiliaryPath, "auxiliary");
            await File.WriteAllTextAsync(temporaryPath, "temporary");
            await File.WriteAllTextAsync(unrelatedPath, "keep");

            var relatedCleanup = await service.DeleteRelatedFilesAsync(
                imagePath,
                includeMainFile: false,
                cancellationToken: CancellationToken.None);
            Assert.Equal(2, relatedCleanup.DeletedCount);
            Assert.True(File.Exists(imagePath));
            Assert.False(File.Exists(worldFilePath));
            Assert.False(File.Exists(auxiliaryPath));

            var temporaryCleanup = await service.DeleteTemporaryFilesAsync(root, CancellationToken.None);
            Assert.Equal(1, temporaryCleanup.DeletedCount);
            Assert.False(File.Exists(temporaryPath));
            Assert.True(File.Exists(unrelatedPath));
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
