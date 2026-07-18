using XIAOFUTools.Features.DataManagement.OvertureLoader.Infrastructure;

namespace XIAOFUTools.Tests.OvertureLoader;

public sealed class FileSystemOvertureDuckDbExtensionCatalogTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Catalog_ReturnsOnlyTopLevelDuckDbExtensions()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"xiaofutools-duckdb-extensions-{Guid.NewGuid():N}");
        var nested = Path.Combine(root, "nested");
        Directory.CreateDirectory(nested);

        try
        {
            var spatial = Path.Combine(root, "spatial.duckdb_extension");
            var httpfs = Path.Combine(root, "httpfs.duckdb_extension");
            await File.WriteAllTextAsync(spatial, string.Empty);
            await File.WriteAllTextAsync(httpfs, string.Empty);
            await File.WriteAllTextAsync(
                Path.Combine(nested, "unexpected.duckdb_extension"),
                string.Empty);
            await File.WriteAllTextAsync(Path.Combine(root, "README.txt"), string.Empty);

            var files = new FileSystemOvertureDuckDbExtensionCatalog()
                .GetExtensionFiles(root);

            Assert.Equal(
                new[] { httpfs, spatial }.OrderBy(path => path, StringComparer.OrdinalIgnoreCase),
                files.OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
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
