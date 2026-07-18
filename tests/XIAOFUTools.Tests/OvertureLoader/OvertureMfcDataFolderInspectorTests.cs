using XIAOFUTools.Features.DataManagement.OvertureLoader.Infrastructure;

namespace XIAOFUTools.Tests.OvertureLoader;

public sealed class OvertureMfcDataFolderInspectorTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Inspector_CreatesOutputFolderAndReportsNestedParquetFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xiaofutools-mfc-inspector-{Guid.NewGuid():N}");
        var dataFolder = Path.Combine(root, "data");
        var nestedFolder = Path.Combine(dataFolder, "base", "land");
        var outputFolder = Path.Combine(root, "connections");
        Directory.CreateDirectory(nestedFolder);
        await File.WriteAllTextAsync(Path.Combine(nestedFolder, "part.parquet"), "data");
        var inspector = new OvertureMfcDataFolderInspector();

        try
        {
            Assert.True(await inspector.EnsureOutputFolderAsync(outputFolder, CancellationToken.None));
            Assert.False(await inspector.EnsureOutputFolderAsync(outputFolder, CancellationToken.None));

            var inspection = await inspector.InspectAsync(dataFolder, CancellationToken.None);

            Assert.True(inspection.Exists);
            Assert.True(inspection.ContainsParquetFiles);
            Assert.Equal(1, inspection.ParquetFileCount);
            Assert.Empty(inspection.ThemeFolders);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Inspector_ReportsThemeAndTypeFoldersWhenParquetFilesAreAbsent()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xiaofutools-mfc-inspector-{Guid.NewGuid():N}");
        var dataFolder = Path.Combine(root, "data");
        Directory.CreateDirectory(Path.Combine(dataFolder, "base", "land"));
        var inspector = new OvertureMfcDataFolderInspector();

        try
        {
            var inspection = await inspector.InspectAsync(dataFolder, CancellationToken.None);

            Assert.True(inspection.Exists);
            Assert.False(inspection.ContainsParquetFiles);
            var theme = Assert.Single(inspection.ThemeFolders);
            Assert.Equal("base", theme.Name);
            var type = Assert.Single(theme.TypeFolders);
            Assert.Equal("land", type.Name);
            Assert.Equal(0, type.ParquetFileCount);
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
