using XIAOFUTools.Features.General.HistoricalImageryDownload;
using XIAOFUTools.Features.General.HistoricalImageryDownload.Services;

namespace XIAOFUTools.Tests.HistoricalImageryDownload;

public class HistoricalCoverageProbeSamplerTests
{
    [Fact]
    public void CreateSample_WhenTileCountIsSmall_ReturnsAllTiles()
    {
        var tiles = Enumerable.Range(0, 6)
            .Select(index => new HistoricalTileDefinition { Row = index, Column = index, ZoomLevel = 12 })
            .ToArray();

        var sample = HistoricalCoverageProbeSampler.CreateSample(tiles, maxSampleCount: 16);

        Assert.Equal(tiles.Length, sample.Count);
    }

    [Fact]
    public void CreateSample_WhenTileCountIsLarge_ReturnsBoundedDistinctSample()
    {
        var tiles = Enumerable.Range(0, 100)
            .Select(index => new HistoricalTileDefinition { Row = index / 10, Column = index % 10, ZoomLevel = 12 })
            .ToArray();

        var sample = HistoricalCoverageProbeSampler.CreateSample(tiles, maxSampleCount: 16);

        Assert.InRange(sample.Count, 8, 16);
        Assert.Equal(sample.Count, sample.Distinct().Count());
    }
}
