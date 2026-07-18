using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using XIAOFUTools.Features.General.InternetTileDownload;
using XIAOFUTools.Features.General.InternetTileDownload.Services;
using Xunit;

namespace XIAOFUTools.Tests.InternetTileDownload;

public class InternetTileCoverageProbeServiceTests
{
    [Fact]
    public async Task ProbeAsync_WhenAllSampleTilesFail_ReturnsNoCoverage()
    {
        var client = new ProbeHttpClient();
        var definition = CreateDefinition();
        var tiles = CreateTiles();

        var result = await InternetTileCoverageProbeService.ProbeAsync(client, definition, "4", tiles);

        Assert.False(result.HasCoverage);
        Assert.Equal(tiles.Count, result.CheckedTileCount);
        Assert.Equal(0, result.AvailableTileCount);
    }

    [Fact]
    public async Task ProbeAsync_WhenAnySampleTileSucceeds_ReturnsCoverage()
    {
        var client = new ProbeHttpClient
        {
            SuccessUrls =
            [
                "https://example.com/tiles/4/2/1.png"
            ]
        };
        var definition = CreateDefinition();
        var tiles = CreateTiles();

        var result = await InternetTileCoverageProbeService.ProbeAsync(client, definition, "4", tiles);

        Assert.True(result.HasCoverage);
        Assert.Equal(tiles.Count, result.CheckedTileCount);
        Assert.Equal(1, result.AvailableTileCount);
    }

    private static InternetTileServiceDefinition CreateDefinition()
    {
        return new InternetTileServiceDefinition
        {
            ServiceKind = InternetTileServiceKind.Xyz,
            TemplateMode = InternetTileTemplateMode.PathSegments,
            UrlTemplate = "https://example.com/tiles/{level}/{row}/{col}.png",
            SourceSpatialReferenceText = "EPSG:3857",
            MatrixProfile = InternetTileMatrixProfile.WebMercator,
            Levels =
            [
                new InternetTileLevelDefinition
                {
                    LevelId = "4",
                    Resolution = 9783.93962050256d,
                    TopLeftX = -20037508.342789244d,
                    TopLeftY = 20037508.342789244d,
                    TileWidth = 256,
                    TileHeight = 256,
                    MatrixWidth = 16,
                    MatrixHeight = 16
                }
            ]
        };
    }

    private static IReadOnlyList<InternetTileDefinition> CreateTiles()
    {
        return
        [
            new InternetTileDefinition { LevelId = "4", Row = 1, Column = 1 },
            new InternetTileDefinition { LevelId = "4", Row = 2, Column = 1 },
            new InternetTileDefinition { LevelId = "4", Row = 3, Column = 1 }
        ];
    }

    private sealed class ProbeHttpClient : IInternetTileHttpClient
    {
        public HashSet<string> SuccessUrls { get; init; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<byte[]> GetBytesAsync(string url, CancellationToken cancellationToken = default)
        {
            if (SuccessUrls.Contains(url))
            {
                return Task.FromResult(new byte[] { 1, 2, 3 });
            }

            throw new InvalidOperationException("404");
        }

        public Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
