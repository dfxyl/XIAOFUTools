using XIAOFUTools.Features.Conversion.TxtToFeature.Infrastructure;

namespace XIAOFUTools.Tests.TxtToFeature;

public sealed class TxtToFeatureFileStoreTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Store_FindsTextFilesAndCreatesOutputFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xiaofutools-txt-store-{Guid.NewGuid():N}");
        var input = Path.Combine(root, "input");
        var nested = Path.Combine(input, "nested");
        var output = Path.Combine(root, "output");
        var temporaryDirectory = Path.Combine(root, "temporary.gdb");
        Directory.CreateDirectory(nested);
        await File.WriteAllTextAsync(Path.Combine(input, "first.txt"), "one");
        await File.WriteAllTextAsync(Path.Combine(nested, "second.txt"), "two");
        var store = new TxtToFeatureFileStore();

        try
        {
            Assert.True(await store.DirectoryExistsAsync(input, CancellationToken.None));
            var topLevelFiles = await store.FindTextFilesAsync(
                input,
                includeSubfolders: false,
                cancellationToken: CancellationToken.None);
            Assert.Single(topLevelFiles);
            Assert.Equal(
                2,
                (await store.FindTextFilesAsync(
                    input,
                    includeSubfolders: true,
                    cancellationToken: CancellationToken.None)).Count);

            await store.EnsureDirectoryAsync(output, CancellationToken.None);
            Assert.True(Directory.Exists(output));

            Directory.CreateDirectory(temporaryDirectory);
            await File.WriteAllTextAsync(Path.Combine(temporaryDirectory, "temp.txt"), "temporary");
            var cleanup = await store.CleanupTemporaryDirectoryAsync(
                temporaryDirectory,
                CancellationToken.None);
            Assert.True(cleanup.Existed);
            Assert.True(cleanup.Deleted);
            Assert.False(Directory.Exists(temporaryDirectory));
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
