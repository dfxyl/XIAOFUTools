using XIAOFUTools.Features.Analysis.BrowseFeatures.Core;

namespace XIAOFUTools.Tests.BrowseFeatures;

public sealed class BrowseFeaturesCoreTests
{
    [Fact]
    public void CreateDefaultBatchId_UsesTimestampFormat()
    {
        var batchId = BrowseFeaturesCore.CreateDefaultBatchId(new DateTime(2026, 3, 29, 10, 20, 30));

        Assert.Equal("20260329_102030", batchId);
    }

    [Fact]
    public void NormalizeReviewStatus_MapsUnknownToPending()
    {
        var status = BrowseFeaturesCore.NormalizeReviewStatus("xxx");

        Assert.Equal(ReviewStatus.Pending, status);
    }

    [Fact]
    public void CompareSortValue_NullAlwaysLast_WhenAscending()
    {
        var result = BrowseFeaturesCore.CompareSortValue(null, 100, descending: false);

        Assert.True(result > 0);
    }

    [Fact]
    public void CompareSortValue_NullAlwaysLast_WhenDescending()
    {
        var result = BrowseFeaturesCore.CompareSortValue(null, 100, descending: true);

        Assert.True(result > 0);
    }

    [Fact]
    public void CompareSortValue_RespectsDescendingForNumbers()
    {
        var ascending = BrowseFeaturesCore.CompareSortValue(1, 2, descending: false);
        var descending = BrowseFeaturesCore.CompareSortValue(1, 2, descending: true);

        Assert.True(ascending < 0);
        Assert.True(descending > 0);
    }
}
