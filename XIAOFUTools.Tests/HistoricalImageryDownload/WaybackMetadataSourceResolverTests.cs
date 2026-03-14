using XIAOFUTools.Tools.HistoricalImageryDownload;
using XIAOFUTools.Tools.HistoricalImageryDownload.Services;

namespace XIAOFUTools.Tests.HistoricalImageryDownload;

public class WaybackMetadataSourceResolverTests
{
    [Fact]
    public void Resolve_ReturnsGlobalLatestWhenNoMapWaybackLayerExists()
    {
        var catalog = CreateCatalog();

        var result = WaybackMetadataSourceResolver.Resolve(catalog, Array.Empty<WaybackMapLayerReference>());

        Assert.False(result.RequiresSelection);
        Assert.NotNull(result.AutoSelectedCandidate);
        Assert.Equal(WaybackMetadataSourceType.GlobalLatest, result.AutoSelectedCandidate!.SourceType);
        Assert.Equal("64001", result.AutoSelectedCandidate.Version.VersionId);
    }

    [Fact]
    public void Resolve_ReturnsCurrentLayerWhenExactlyOneLayerMatches()
    {
        var catalog = CreateCatalog();
        var layers = new[]
        {
            new WaybackMapLayerReference(
                "Wayback 2025-09-04",
                "https://wayback-a.maptiles.arcgis.com/arcgis/rest/services/World_Imagery/WMTS/1.0.0/default028mm/MapServer/tile/52304/{level}/{row}/{col}")
        };

        var result = WaybackMetadataSourceResolver.Resolve(catalog, layers);

        Assert.False(result.RequiresSelection);
        Assert.NotNull(result.AutoSelectedCandidate);
        Assert.Equal(WaybackMetadataSourceType.MapLayer, result.AutoSelectedCandidate!.SourceType);
        Assert.Equal("52304", result.AutoSelectedCandidate.Version.VersionId);
        Assert.Equal("Wayback 2025-09-04", result.AutoSelectedCandidate.LayerName);
    }

    [Fact]
    public void Resolve_ReturnsSelectionCandidatesWhenMultipleLayersMatch()
    {
        var catalog = CreateCatalog();
        var layers = new[]
        {
            new WaybackMapLayerReference(
                "Wayback 2025-09-04",
                "https://wayback-a.maptiles.arcgis.com/arcgis/rest/services/World_Imagery/WMTS/1.0.0/default028mm/MapServer/tile/52304/{level}/{row}/{col}"),
            new WaybackMapLayerReference(
                "Wayback 2025-05-29",
                "https://wayback-a.maptiles.arcgis.com/arcgis/rest/services/World_Imagery/WMTS/1.0.0/default028mm/MapServer/tile/25285/{level}/{row}/{col}")
        };

        var result = WaybackMetadataSourceResolver.Resolve(catalog, layers);

        Assert.True(result.RequiresSelection);
        Assert.Null(result.AutoSelectedCandidate);
        Assert.Collection(
            result.Candidates,
            candidate =>
            {
                Assert.Equal(WaybackMetadataSourceType.MapLayer, candidate.SourceType);
                Assert.Equal("52304", candidate.Version.VersionId);
            },
            candidate =>
            {
                Assert.Equal(WaybackMetadataSourceType.MapLayer, candidate.SourceType);
                Assert.Equal("25285", candidate.Version.VersionId);
            },
            candidate =>
            {
                Assert.Equal(WaybackMetadataSourceType.GlobalLatest, candidate.SourceType);
                Assert.Equal("64001", candidate.Version.VersionId);
            });
    }

    private static HistoricalVersionItem[] CreateCatalog()
        =>
        [
            new HistoricalVersionItem
            {
                Provider = HistoricalImageryProviderType.Wayback,
                VersionId = "64001",
                DisplayDate = "2026-02-26",
                Summary = "World Imagery (Wayback 2026-02-26)",
                MetadataLayerUrl = "https://metadata.maptiles.arcgis.com/arcgis/rest/services/World_Imagery_Metadata_2026_r02/MapServer"
            },
            new HistoricalVersionItem
            {
                Provider = HistoricalImageryProviderType.Wayback,
                VersionId = "52304",
                DisplayDate = "2025-09-04",
                Summary = "World Imagery (Wayback 2025-09-04)",
                MetadataLayerUrl = "https://metadata.maptiles.arcgis.com/arcgis/rest/services/World_Imagery_Metadata_2025_r08/MapServer"
            },
            new HistoricalVersionItem
            {
                Provider = HistoricalImageryProviderType.Wayback,
                VersionId = "25285",
                DisplayDate = "2025-05-29",
                Summary = "World Imagery (Wayback 2025-05-29)",
                MetadataLayerUrl = "https://metadata.maptiles.arcgis.com/arcgis/rest/services/World_Imagery_Metadata_2025_r05/MapServer"
            }
        ];
}
