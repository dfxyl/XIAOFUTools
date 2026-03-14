using XIAOFUTools.Tools.HistoricalImageryDownload;
using XIAOFUTools.Tools.HistoricalImageryDownload.Services;

namespace XIAOFUTools.Tests.HistoricalImageryDownload;

public class WaybackVersionFilterTests
{
    [Fact]
    public void FilterVersions_ReturnsOriginalVersionsWhenAllMetadataMissing()
    {
        var versions = new[]
        {
            new HistoricalVersionItem
            {
                Provider = HistoricalImageryProviderType.Wayback,
                VersionId = "v2",
                DisplayDate = "2024-03-01"
            },
            new HistoricalVersionItem
            {
                Provider = HistoricalImageryProviderType.Wayback,
                VersionId = "v1",
                DisplayDate = "2024-02-01"
            }
        };

        var filtered = WaybackVersionFilter.FilterVersions(versions, includeAllVersions: false);

        Assert.Collection(
            filtered,
            version => Assert.Equal("v2", version.VersionId),
            version => Assert.Equal("v1", version.VersionId));
    }

    [Fact]
    public void FilterVersions_ReturnsOnlyChangedVersionsInHistoryMode()
    {
        var versions = new[]
        {
            new HistoricalVersionItem
            {
                Provider = HistoricalImageryProviderType.Wayback,
                VersionId = "known",
                DisplayDate = "2024-03-01",
                AcquisitionDate = "2024-02-28"
            },
            new HistoricalVersionItem
            {
                Provider = HistoricalImageryProviderType.Wayback,
                VersionId = "unknown",
                DisplayDate = "2024-01-01"
            }
        };

        var filtered = WaybackVersionFilter.FilterVersions(versions, includeAllVersions: false);

        Assert.Collection(
            filtered,
            version => Assert.Equal("known", version.VersionId));
    }

    [Fact]
    public void FilterVersions_ReturnsAllVersionsInAllMode()
    {
        var versions = new[]
        {
            new HistoricalVersionItem
            {
                Provider = HistoricalImageryProviderType.Wayback,
                VersionId = "newest",
                DisplayDate = "2024-03-01",
                AcquisitionDate = "2024-02-28"
            },
            new HistoricalVersionItem
            {
                Provider = HistoricalImageryProviderType.Wayback,
                VersionId = "older",
                DisplayDate = "2024-01-01",
                AcquisitionDate = "2024-01-01"
            }
        };

        var filtered = WaybackVersionFilter.FilterVersions(versions, includeAllVersions: true);

        Assert.Collection(
            filtered,
            version => Assert.Equal("newest", version.VersionId),
            version => Assert.Equal("older", version.VersionId));
    }

    [Fact]
    public void FilterVersions_UsesEffectiveVersionIdToKeepOnlyTrueChangeReleases()
    {
        var versions = new[]
        {
            new HistoricalVersionItem
            {
                Provider = HistoricalImageryProviderType.Wayback,
                VersionId = "64001",
                DisplayDate = "2026-02-26",
                ChangeKey = "64001"
            },
            new HistoricalVersionItem
            {
                Provider = HistoricalImageryProviderType.Wayback,
                VersionId = "22252",
                DisplayDate = "2026-01-29",
                ChangeKey = "52304"
            },
            new HistoricalVersionItem
            {
                Provider = HistoricalImageryProviderType.Wayback,
                VersionId = "13192",
                DisplayDate = "2025-12-18",
                ChangeKey = "52304"
            },
            new HistoricalVersionItem
            {
                Provider = HistoricalImageryProviderType.Wayback,
                VersionId = "52304",
                DisplayDate = "2025-09-04",
                ChangeKey = "52304"
            }
        };

        var filtered = WaybackVersionFilter.FilterVersions(versions, includeAllVersions: false);

        Assert.Collection(
            filtered,
            version => Assert.Equal("64001", version.VersionId),
            version => Assert.Equal("52304", version.VersionId));
    }
}
