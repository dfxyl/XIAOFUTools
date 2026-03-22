using XIAOFUTools.Tools.HistoricalImageryDownload;
using XIAOFUTools.Tools.HistoricalImageryDownload.Infrastructure;

namespace XIAOFUTools.Tests.HistoricalImageryDownload;

public class WaybackCatalogParserTests
{
    [Fact]
    public void Parse_ExtractsVersionDateTileTemplateAndMetadataUrl()
    {
        var json =
            """
            {
              "64001": {
                "itemTitle": "World Imagery (Wayback 2026-02-26)",
                "itemURL": "https://wayback.maptiles.arcgis.com/arcgis/rest/services/World_Imagery/WMTS/1.0.0/default028mm/MapServer/tile/64001/{level}/{row}/{col}",
                "metadataLayerUrl": "https://metadata.maptiles.arcgis.com/arcgis/rest/services/World_Imagery_Metadata_2026_r02/MapServer"
              }
            }
            """;

        var versions = WaybackCatalogParser.Parse(json);

        var version = Assert.Single(versions);
        Assert.Equal(HistoricalImageryProviderType.Wayback, version.Provider);
        Assert.Equal("64001", version.VersionId);
        Assert.Equal("2026-02-26", version.DisplayDate);
        Assert.Equal(
            "https://wayback-b.maptiles.arcgis.com/arcgis/rest/services/World_Imagery/WMTS/1.0.0/default028mm/MapServer/tile/64001/{level}/{row}/{col}",
            version.TileUrlTemplate);
        Assert.Equal(
            "https://metadata.maptiles.arcgis.com/arcgis/rest/services/World_Imagery_Metadata_2026_r02/MapServer",
            version.MetadataLayerUrl);
    }

    [Fact]
    public void Parse_KeepsNonWaybackUrlsUnchanged()
    {
        var json =
            """
            {
              "64001": {
                "itemTitle": "World Imagery (Wayback 2026-02-26)",
                "itemURL": "https://example.com/tiles/{level}/{row}/{col}",
                "metadataLayerUrl": "https://metadata.maptiles.arcgis.com/arcgis/rest/services/World_Imagery_Metadata_2026_r02/MapServer"
              }
            }
            """;

        var versions = WaybackCatalogParser.Parse(json);

        var version = Assert.Single(versions);
        Assert.Equal("https://example.com/tiles/{level}/{row}/{col}", version.TileUrlTemplate);
    }
}
