using System;
using System.Threading;
using System.Threading.Tasks;
using XIAOFUTools.Features.General.InternetTileDownload;
using XIAOFUTools.Features.General.InternetTileDownload.Services;
using Xunit;

namespace XIAOFUTools.Tests.InternetTileDownload;

public class InternetTileServiceResolverTests
{
    [Fact]
    public async Task ResolveAsync_XyzTemplate_ReturnsStandardWebMercatorDefinition()
    {
        var resolver = new InternetTileServiceResolver(new StubInternetTileHttpClient());

        var definition = await resolver.ResolveAsync("https://gac-geo.googlecnapps.club/maps/vt?lyrs=s&x={col}&y={row}&z={level}");

        Assert.Equal(InternetTileServiceKind.Xyz, definition.ServiceKind);
        Assert.Equal("EPSG:3857", definition.SourceSpatialReferenceText);
        Assert.Equal(InternetTileMatrixProfile.WebMercator, definition.MatrixProfile);
        Assert.Equal(23, definition.Levels.Count);
        Assert.Equal("0", definition.Levels[0].LevelId);
        Assert.Equal(256, definition.Levels[0].TileWidth);
    }

    [Fact]
    public async Task ResolveAsync_WmtsTemplate_UsesCapabilitiesDocument()
    {
        var client = new StubInternetTileHttpClient(
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <Capabilities xmlns="http://www.opengis.net/wmts/1.0"
                          xmlns:ows="http://www.opengis.net/ows/1.1">
              <Contents>
                <Layer>
                  <ows:Identifier>img</ows:Identifier>
                  <Style isDefault="true"><ows:Identifier>default</ows:Identifier></Style>
                  <Format>tiles</Format>
                  <TileMatrixSetLink><TileMatrixSet>w</TileMatrixSet></TileMatrixSetLink>
                </Layer>
                <TileMatrixSet>
                  <ows:Identifier>w</ows:Identifier>
                  <ows:SupportedCRS>urn:ogc:def:crs:EPSG::3857</ows:SupportedCRS>
                  <TileMatrix>
                    <ows:Identifier>0</ows:Identifier>
                    <ScaleDenominator>559082264.0287178</ScaleDenominator>
                    <TopLeftCorner>-20037508.342789244 20037508.342789244</TopLeftCorner>
                    <TileWidth>256</TileWidth>
                    <TileHeight>256</TileHeight>
                    <MatrixWidth>1</MatrixWidth>
                    <MatrixHeight>1</MatrixHeight>
                  </TileMatrix>
                </TileMatrixSet>
              </Contents>
            </Capabilities>
            """);
        var resolver = new InternetTileServiceResolver(client);

        var definition = await resolver.ResolveAsync("https://t0.tianditu.gov.cn/img_w/wmts?SERVICE=WMTS&VERSION=1.0.0&REQUEST=GetTile&LAYER=img&STYLE=default&FORMAT=tiles&TILEMATRIXSET=w&TILEMATRIX={level}&TILEROW={row}&TILECOL={col}&tk=test");

        Assert.Equal(InternetTileServiceKind.Wmts, definition.ServiceKind);
        Assert.Equal("EPSG:3857", definition.SourceSpatialReferenceText);
        Assert.Single(definition.Levels);
        Assert.Contains("REQUEST=GetCapabilities", client.LastStringUrl!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveAsync_RestfulWmtsTemplate_UsesRestfulCapabilitiesUrl()
    {
        var client = new StubInternetTileHttpClient(
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <Capabilities xmlns="http://www.opengis.net/wmts/1.0"
                          xmlns:ows="http://www.opengis.net/ows/1.1">
              <Contents>
                <Layer>
                  <ows:Identifier>ch.swisstopo.pixelkarte-farbe</ows:Identifier>
                  <Style isDefault="true"><ows:Identifier>default</ows:Identifier></Style>
                  <Format>image/jpeg</Format>
                  <TileMatrixSetLink><TileMatrixSet>3857</TileMatrixSet></TileMatrixSetLink>
                </Layer>
                <TileMatrixSet>
                  <ows:Identifier>3857</ows:Identifier>
                  <ows:SupportedCRS>urn:ogc:def:crs:EPSG::3857</ows:SupportedCRS>
                  <TileMatrix>
                    <ows:Identifier>0</ows:Identifier>
                    <ScaleDenominator>559082264.0287178</ScaleDenominator>
                    <TopLeftCorner>-20037508.342789244 20037508.342789244</TopLeftCorner>
                    <TileWidth>256</TileWidth>
                    <TileHeight>256</TileHeight>
                    <MatrixWidth>1</MatrixWidth>
                    <MatrixHeight>1</MatrixHeight>
                  </TileMatrix>
                </TileMatrixSet>
              </Contents>
            </Capabilities>
            """);
        var resolver = new InternetTileServiceResolver(client);

        var definition = await resolver.ResolveAsync("https://wmts.geo.admin.ch/1.0.0/ch.swisstopo.pixelkarte-farbe/default/current/3857/{TileMatrix}/{TileRow}/{TileCol}.jpeg");

        Assert.Equal(InternetTileServiceKind.Wmts, definition.ServiceKind);
        Assert.Equal(InternetTileTemplateMode.RestfulWmts, definition.TemplateMode);
        Assert.Equal("3857", definition.MatrixSetIdentifier);
        Assert.Contains("WMTSCapabilities.xml", client.LastStringUrl!, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StubInternetTileHttpClient : IInternetTileHttpClient
    {
        private readonly string _responseText;

        public StubInternetTileHttpClient(string responseText = "")
        {
            _responseText = responseText;
        }

        public string? LastStringUrl { get; private set; }

        public Task<byte[]> GetBytesAsync(string url, CancellationToken cancellationToken = default)
        {
            LastStringUrl = url;
            return Task.FromResult(Array.Empty<byte>());
        }

        public Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default)
        {
            LastStringUrl = url;
            return Task.FromResult(_responseText);
        }
    }
}
