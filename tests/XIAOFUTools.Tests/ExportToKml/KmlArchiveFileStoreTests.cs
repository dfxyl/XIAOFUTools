using System.IO.Compression;
using System.Text;
using XIAOFUTools.Features.Conversion.ExportToKml.Infrastructure;

namespace XIAOFUTools.Tests.ExportToKml;

public sealed class KmlArchiveFileStoreTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Store_ExtractsReadsAndUpdatesDocKml()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xiaofutools-kml-store-{Guid.NewGuid():N}");
        var kmzPath = Path.Combine(root, "source.kmz");
        var kmlPath = Path.Combine(root, "result.kml");
        var originalKml = "<kml><Document><name>原始</name></Document></kml>";
        var updatedKml = "<kml><Document><name>更新</name></Document></kml>";
        var store = new KmlArchiveFileStore();

        try
        {
            await store.EnsureOutputDirectoryAsync(root, CancellationToken.None);
            using (var archive = ZipFile.Open(kmzPath, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("doc.kml");
                await using var stream = entry.Open();
                await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
                await writer.WriteAsync(originalKml);
            }

            await store.ExtractDocKmlAsync(kmzPath, kmlPath, CancellationToken.None);
            Assert.Equal(originalKml, await File.ReadAllTextAsync(kmlPath));
            Assert.Equal(originalKml, await store.ReadDocKmlAsync(kmzPath, CancellationToken.None));

            await store.WriteDocKmlAsync(kmzPath, updatedKml, CancellationToken.None);
            Assert.Equal(updatedKml, await store.ReadDocKmlAsync(kmzPath, CancellationToken.None));
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
