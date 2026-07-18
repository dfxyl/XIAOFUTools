using XIAOFUTools.Features.User.Settings.Infrastructure;

namespace XIAOFUTools.Tests.Settings;

public sealed class PresetLayerFileStoreTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void Store_CreatesLayerDirectoryAndCopiesFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xiaofutools-settings-{Guid.NewGuid():N}");
        var source = Path.Combine(root, "source.lyrx");
        var targetFolder = Path.Combine(root, "layers");
        var destination = Path.Combine(targetFolder, "source.lyrx");
        var store = new PresetLayerFileStore();

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(source, "layer");
            store.EnsureDirectory(targetFolder);
            store.CopyFile(source, destination);

            Assert.True(store.FileExists(destination));
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
