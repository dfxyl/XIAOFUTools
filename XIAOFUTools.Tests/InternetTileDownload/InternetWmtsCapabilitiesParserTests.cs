using System;
using XIAOFUTools.Tools.InternetTileDownload;
using XIAOFUTools.Tools.InternetTileDownload.Infrastructure;
using Xunit;

namespace XIAOFUTools.Tests.InternetTileDownload;

public class InternetWmtsCapabilitiesParserTests
{
    [Fact]
    public void Parse_ExtractsMatchingLayerAndMatrixLevels()
    {
        const string xml =
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <Capabilities xmlns="http://www.opengis.net/wmts/1.0"
                          xmlns:ows="http://www.opengis.net/ows/1.1">
              <Contents>
                <Layer>
                  <ows:Identifier>img</ows:Identifier>
                  <Style isDefault="true">
                    <ows:Identifier>default</ows:Identifier>
                  </Style>
                  <Format>tiles</Format>
                  <TileMatrixSetLink>
                    <TileMatrixSet>w</TileMatrixSet>
                  </TileMatrixSetLink>
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
                  <TileMatrix>
                    <ows:Identifier>1</ows:Identifier>
                    <ScaleDenominator>279541132.0143589</ScaleDenominator>
                    <TopLeftCorner>-20037508.342789244 20037508.342789244</TopLeftCorner>
                    <TileWidth>256</TileWidth>
                    <TileHeight>256</TileHeight>
                    <MatrixWidth>2</MatrixWidth>
                    <MatrixHeight>2</MatrixHeight>
                  </TileMatrix>
                </TileMatrixSet>
              </Contents>
            </Capabilities>
            """;

        var templateInfo = InternetTileTemplateParser.Parse(
            "https://t0.tianditu.gov.cn/img_w/wmts?SERVICE=WMTS&VERSION=1.0.0&REQUEST=GetTile&LAYER=img&STYLE=default&FORMAT=tiles&TILEMATRIXSET=w&TILEMATRIX={level}&TILEROW={row}&TILECOL={col}&tk=test");

        var definition = InternetWmtsCapabilitiesParser.Parse(xml, templateInfo);

        Assert.Equal(InternetTileServiceKind.Wmts, definition.ServiceKind);
        Assert.Equal("EPSG:3857", definition.SourceSpatialReferenceText);
        Assert.Equal(InternetTileMatrixProfile.WebMercator, definition.MatrixProfile);
        Assert.Equal("img", definition.LayerIdentifier);
        Assert.Equal("w", definition.MatrixSetIdentifier);

        var levels = Assert.IsAssignableFrom<IReadOnlyList<InternetTileLevelDefinition>>(definition.Levels);
        Assert.Equal(2, levels.Count);
        Assert.Equal("0", levels[0].LevelId);
        Assert.Equal(256, levels[0].TileWidth);
        Assert.Equal(2, levels[1].MatrixWidth);
        Assert.Equal(-20037508.342789244d, levels[0].TopLeftX, 6);
        Assert.Equal(20037508.342789244d, levels[0].TopLeftY, 6);
        Assert.True(levels[0].Resolution > levels[1].Resolution);
    }

    [Fact]
    public void Parse_Epsg4326Matrix_ConvertsScaleDenominatorToDegreeResolution()
    {
        const string xml =
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <Capabilities xmlns="http://www.opengis.net/wmts/1.0"
                          xmlns:ows="http://www.opengis.net/ows/1.1">
              <Contents>
                <Layer>
                  <ows:Identifier>img</ows:Identifier>
                  <Style isDefault="true"><ows:Identifier>default</ows:Identifier></Style>
                  <Format>tiles</Format>
                  <TileMatrixSetLink><TileMatrixSet>c</TileMatrixSet></TileMatrixSetLink>
                </Layer>
                <TileMatrixSet>
                  <ows:Identifier>c</ows:Identifier>
                  <ows:SupportedCRS>urn:ogc:def:crs:EPSG::4326</ows:SupportedCRS>
                  <TileMatrix>
                    <ows:Identifier>0</ows:Identifier>
                    <ScaleDenominator>279541132.0143589</ScaleDenominator>
                    <TopLeftCorner>-180 90</TopLeftCorner>
                    <TileWidth>256</TileWidth>
                    <TileHeight>256</TileHeight>
                    <MatrixWidth>2</MatrixWidth>
                    <MatrixHeight>1</MatrixHeight>
                  </TileMatrix>
                </TileMatrixSet>
              </Contents>
            </Capabilities>
            """;

        var templateInfo = InternetTileTemplateParser.Parse(
            "https://t0.tianditu.gov.cn/img_c/wmts?SERVICE=WMTS&VERSION=1.0.0&REQUEST=GetTile&LAYER=img&STYLE=default&FORMAT=tiles&TILEMATRIXSET=c&TILEMATRIX={level}&TILEROW={row}&TILECOL={col}&tk=test");

        var definition = InternetWmtsCapabilitiesParser.Parse(xml, templateInfo);

        Assert.Equal("EPSG:4326", definition.SourceSpatialReferenceText);
        Assert.Equal(0.703125d, definition.Levels[0].Resolution, 6);
    }

    [Fact]
    public void Parse_Epsg900913Matrix_NormalizesToWebMercator()
    {
        const string xml =
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
                  <ows:SupportedCRS>urn:ogc:def:crs:EPSG::900913</ows:SupportedCRS>
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
            """;

        var templateInfo = InternetTileTemplateParser.Parse(
            "https://t0.tianditu.gov.cn/img_w/wmts?SERVICE=WMTS&VERSION=1.0.0&REQUEST=GetTile&LAYER=img&STYLE=default&FORMAT=tiles&TILEMATRIXSET=w&TILEMATRIX={level}&TILEROW={row}&TILECOL={col}&tk=test");

        var definition = InternetWmtsCapabilitiesParser.Parse(xml, templateInfo);

        Assert.Equal("EPSG:3857", definition.SourceSpatialReferenceText);
        Assert.Equal(InternetTileMatrixProfile.WebMercator, definition.MatrixProfile);
    }

    [Fact]
    public void Parse_TiandituTopLeftCorner_NormalizesSwappedOrigin()
    {
        const string xml =
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
                  <ows:SupportedCRS>urn:ogc:def:crs:EPSG::900913</ows:SupportedCRS>
                  <TileMatrix>
                    <ows:Identifier>18</ows:Identifier>
                    <ScaleDenominator>2256.998866688275</ScaleDenominator>
                    <TopLeftCorner>20037508.3427892 -20037508.3427892</TopLeftCorner>
                    <TileWidth>256</TileWidth>
                    <TileHeight>256</TileHeight>
                    <MatrixWidth>262144</MatrixWidth>
                    <MatrixHeight>262144</MatrixHeight>
                  </TileMatrix>
                </TileMatrixSet>
              </Contents>
            </Capabilities>
            """;

        var templateInfo = InternetTileTemplateParser.Parse(
            "https://t0.tianditu.gov.cn/img_w/wmts?SERVICE=WMTS&VERSION=1.0.0&REQUEST=GetTile&LAYER=img&STYLE=default&FORMAT=tiles&TILEMATRIXSET=w&TILEMATRIX={level}&TILEROW={row}&TILECOL={col}&tk=test");

        var definition = InternetWmtsCapabilitiesParser.Parse(xml, templateInfo);

        Assert.Equal(-20037508.3427892d, definition.Levels[0].TopLeftX, 6);
        Assert.Equal(20037508.3427892d, definition.Levels[0].TopLeftY, 6);
    }

    [Fact]
    public void Parse_TiandituMatrix_UsesMatrixWidthForResolution()
    {
        const string xml =
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
                  <ows:SupportedCRS>urn:ogc:def:crs:EPSG::900913</ows:SupportedCRS>
                  <TileMatrix>
                    <ows:Identifier>18</ows:Identifier>
                    <ScaleDenominator>2256.998866688275</ScaleDenominator>
                    <TopLeftCorner>20037508.3427892 -20037508.3427892</TopLeftCorner>
                    <TileWidth>256</TileWidth>
                    <TileHeight>256</TileHeight>
                    <MatrixWidth>262144</MatrixWidth>
                    <MatrixHeight>262144</MatrixHeight>
                  </TileMatrix>
                </TileMatrixSet>
              </Contents>
            </Capabilities>
            """;

        var templateInfo = InternetTileTemplateParser.Parse(
            "https://t0.tianditu.gov.cn/img_w/wmts?SERVICE=WMTS&VERSION=1.0.0&REQUEST=GetTile&LAYER=img&STYLE=default&FORMAT=tiles&TILEMATRIXSET=w&TILEMATRIX={level}&TILEROW={row}&TILECOL={col}&tk=test");

        var definition = InternetWmtsCapabilitiesParser.Parse(xml, templateInfo);

        Assert.Equal(0.597164283478d, definition.Levels[0].Resolution, 6);
    }
}
