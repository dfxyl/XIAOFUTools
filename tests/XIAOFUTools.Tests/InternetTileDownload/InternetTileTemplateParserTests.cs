using System;
using XIAOFUTools.Features.General.InternetTileDownload;
using XIAOFUTools.Features.General.InternetTileDownload.Infrastructure;
using Xunit;

namespace XIAOFUTools.Tests.InternetTileDownload;

public class InternetTileTemplateParserTests
{
    [Fact]
    public void Parse_WmtsQueryTemplate_ExtractsCoreFields()
    {
        const string url = "https://t0.tianditu.gov.cn/img_w/wmts?SERVICE=WMTS&VERSION=1.0.0&REQUEST=GetTile&LAYER=img&STYLE=default&FORMAT=tiles&TILEMATRIXSET=w&TILEMATRIX={level}&TILEROW={row}&TILECOL={col}&tk=85c9d12d5d691d168ba5cb6ecaa749eb";

        var result = InternetTileTemplateParser.Parse(url);

        Assert.Equal(InternetTileServiceKind.Wmts, result.ServiceKind);
        Assert.Equal("img", result.LayerIdentifier);
        Assert.Equal("w", result.MatrixSetIdentifier);
        Assert.Contains("REQUEST=GetCapabilities", result.CapabilitiesUrl, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TILEMATRIX={level}", result.NormalizedTemplate, StringComparison.Ordinal);
        Assert.Contains("TILEROW={row}", result.NormalizedTemplate, StringComparison.Ordinal);
        Assert.Contains("TILECOL={col}", result.NormalizedTemplate, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_XyzTemplate_NormalizesPlaceholderAliases()
    {
        const string url = "https://example.com/tiles?x={x}&y={y}&z={z}&lang=zh-CN";

        var result = InternetTileTemplateParser.Parse(url);

        Assert.Equal(InternetTileServiceKind.Xyz, result.ServiceKind);
        Assert.Contains("x={col}", result.NormalizedTemplate, StringComparison.Ordinal);
        Assert.Contains("y={row}", result.NormalizedTemplate, StringComparison.Ordinal);
        Assert.Contains("z={level}", result.NormalizedTemplate, StringComparison.Ordinal);
        Assert.Equal("EPSG:3857", result.SourceSpatialReferenceText);
        Assert.Null(result.CapabilitiesUrl);
    }

    [Fact]
    public void Parse_ArcGisRestTileTemplate_DetectsArcGisRestService()
    {
        const string url = "https://services.arcgisonline.com/arcgis/rest/services/World_Imagery/MapServer/tile/{level}/{row}/{col}";

        var result = InternetTileTemplateParser.Parse(url);

        Assert.Equal(InternetTileServiceKind.ArcGisRestTile, result.ServiceKind);
        Assert.Equal(InternetTileTemplateMode.ArcGisRestTile, result.TemplateMode);
        Assert.Equal("EPSG:3857", result.SourceSpatialReferenceText);
    }

    [Fact]
    public void Parse_RestfulWmtsTemplate_DetectsRestfulWmtsAndCapabilitiesUrl()
    {
        const string url = "https://wmts.geo.admin.ch/1.0.0/ch.swisstopo.pixelkarte-farbe/default/current/3857/{TileMatrix}/{TileRow}/{TileCol}.jpeg";

        var result = InternetTileTemplateParser.Parse(url);

        Assert.Equal(InternetTileServiceKind.Wmts, result.ServiceKind);
        Assert.Equal(InternetTileTemplateMode.RestfulWmts, result.TemplateMode);
        Assert.Equal("ch.swisstopo.pixelkarte-farbe", result.LayerIdentifier);
        Assert.Equal("3857", result.MatrixSetIdentifier);
        Assert.Equal("https://wmts.geo.admin.ch/1.0.0/WMTSCapabilities.xml", result.CapabilitiesUrl);
    }

    [Fact]
    public void Parse_SubdomainRangeTemplate_ExtractsSubdomains()
    {
        const string url = "https://[t0-t7].tianditu.gov.cn/img_w/wmts?SERVICE=WMTS&VERSION=1.0.0&REQUEST=GetTile&LAYER=img&STYLE=default&FORMAT=tiles&TILEMATRIXSET=w&TILEMATRIX={level}&TILEROW={row}&TILECOL={col}&tk=test";

        var result = InternetTileTemplateParser.Parse(url);

        Assert.Equal(8, result.Subdomains.Count);
        Assert.Contains("{subdomain}", result.NormalizedTemplate, StringComparison.Ordinal);
        Assert.Equal(InternetTileVendorKind.Tianditu, result.VendorKind);
    }

    [Fact]
    public void Parse_GenericSubdomainTemplate_DefaultsToAbcSubdomains()
    {
        const string url = "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png";

        var result = InternetTileTemplateParser.Parse(url);

        Assert.Equal(3, result.Subdomains.Count);
        Assert.Contains("a", result.Subdomains);
        Assert.Contains("{subdomain}", result.NormalizedTemplate, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_QuadKeyTemplate_DetectsQuadKeyMode()
    {
        const string url = "https://ecn.t0.tiles.virtualearth.net/tiles/a{quadkey}.jpeg?g=1";

        var result = InternetTileTemplateParser.Parse(url);

        Assert.Equal(InternetTileServiceKind.QuadKey, result.ServiceKind);
        Assert.Equal(InternetTileTemplateMode.QuadKey, result.TemplateMode);
        Assert.Equal("EPSG:3857", result.SourceSpatialReferenceText);
    }

    [Fact]
    public void Parse_TmsTemplate_DetectsBottomOriginRowMode()
    {
        const string url = "https://example.com/tms/{level}/{col}/{-y}.png";

        var result = InternetTileTemplateParser.Parse(url);

        Assert.Equal(InternetTileServiceKind.Xyz, result.ServiceKind);
        Assert.Equal(InternetTileRowOrigin.Bottom, result.RowOrigin);
    }
}
