using XIAOFUTools.Features.General.HistoricalImageryDownload.Infrastructure.Google;
using XIAOFUTools.Features.General.HistoricalImageryDownload.Services;

namespace XIAOFUTools.Tests.HistoricalImageryDownload;

public class GoogleHistoricalImageryProviderTests
{
    [Fact]
    public void ResolveTileSelection_ReturnsExactMatch_WhenRequestedDateExists()
    {
        var desiredDate = new DateOnly(2025, 5, 20);
        GoogleDatedTileInfo[] datedTiles =
        [
            new GoogleDatedTileInfo(new DateOnly(2025, 5, 24), 101, 1),
            new GoogleDatedTileInfo(desiredDate, 102, 1),
            new GoogleDatedTileInfo(new DateOnly(2025, 5, 18), 103, 1)
        ];

        var resolution = GoogleHistoricalImageryProvider.ResolveTileSelection(
            datedTiles,
            desiredDate,
            allowNearestDateFallback: true);

        Assert.NotNull(resolution);
        Assert.Equal(desiredDate, resolution.DatedTile.Date);
        Assert.False(resolution.UsedNearestDateFallback);
    }

    [Fact]
    public void ResolveTileSelection_ReturnsNull_WhenRequestedDateMissingAndFallbackDisabled()
    {
        var desiredDate = new DateOnly(2025, 5, 20);
        GoogleDatedTileInfo[] datedTiles =
        [
            new GoogleDatedTileInfo(new DateOnly(2025, 5, 24), 101, 1),
            new GoogleDatedTileInfo(new DateOnly(2025, 5, 18), 103, 1)
        ];

        var resolution = GoogleHistoricalImageryProvider.ResolveTileSelection(
            datedTiles,
            desiredDate,
            allowNearestDateFallback: false);

        Assert.Null(resolution);
    }

    [Fact]
    public void ResolveTileSelection_UsesNearestDate_WhenRequestedDateMissingAndFallbackEnabled()
    {
        var desiredDate = new DateOnly(2025, 5, 20);
        GoogleDatedTileInfo[] datedTiles =
        [
            new GoogleDatedTileInfo(new DateOnly(2025, 6, 5), 101, 1),
            new GoogleDatedTileInfo(new DateOnly(2025, 5, 18), 102, 1),
            new GoogleDatedTileInfo(new DateOnly(2025, 4, 30), 103, 1)
        ];

        var resolution = GoogleHistoricalImageryProvider.ResolveTileSelection(
            datedTiles,
            desiredDate,
            allowNearestDateFallback: true);

        Assert.NotNull(resolution);
        Assert.Equal(new DateOnly(2025, 5, 18), resolution.DatedTile.Date);
        Assert.True(resolution.UsedNearestDateFallback);
    }

    [Fact]
    public void ResolveTileSelection_PrefersEarlierDate_WhenTwoCandidatesAreEquallyClose()
    {
        var desiredDate = new DateOnly(2025, 5, 20);
        GoogleDatedTileInfo[] datedTiles =
        [
            new GoogleDatedTileInfo(new DateOnly(2025, 5, 22), 101, 1),
            new GoogleDatedTileInfo(new DateOnly(2025, 5, 18), 102, 1)
        ];

        var resolution = GoogleHistoricalImageryProvider.ResolveTileSelection(
            datedTiles,
            desiredDate,
            allowNearestDateFallback: true);

        Assert.NotNull(resolution);
        Assert.Equal(new DateOnly(2025, 5, 18), resolution.DatedTile.Date);
        Assert.True(resolution.UsedNearestDateFallback);
    }
}
