using XIAOFUTools.Tools.InternetTileDownload;
using XIAOFUTools.Tools.InternetTileDownload.Services;
using Xunit;

namespace XIAOFUTools.Tests.InternetTileDownload;

public class InternetTileServiceSummaryBuilderTests
{
    [Fact]
    public void Build_IncludesProtocolVendorAndRowOrigin()
    {
        var definition = new InternetTileServiceDefinition
        {
            ServiceKind = InternetTileServiceKind.QuadKey,
            TemplateMode = InternetTileTemplateMode.QuadKey,
            VendorKind = InternetTileVendorKind.GoogleLike,
            RowOrigin = InternetTileRowOrigin.Bottom,
            SourceSpatialReferenceText = "EPSG:3857",
            MatrixSetIdentifier = "w",
            LayerIdentifier = "img",
            Levels = [new InternetTileLevelDefinition { LevelId = "0" }]
        };

        var text = InternetTileServiceSummaryBuilder.Build(definition);

        Assert.Contains("QuadKey", text);
        Assert.Contains("GoogleLike", text);
        Assert.Contains("Bottom", text);
        Assert.Contains("EPSG:3857", text);
    }
}
