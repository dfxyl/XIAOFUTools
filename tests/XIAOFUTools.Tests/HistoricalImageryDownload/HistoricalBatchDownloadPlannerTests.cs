using XIAOFUTools.Features.General.HistoricalImageryDownload;
using XIAOFUTools.Features.General.HistoricalImageryDownload.Services;

namespace XIAOFUTools.Tests.HistoricalImageryDownload;

public class HistoricalBatchDownloadPlannerTests
{
    [Fact]
    public void CreateRequests_ReturnsOneRequestPerSelectedVersion()
    {
        var selections = new[]
        {
            new HistoricalVersionSelectionItem(new HistoricalVersionItem
            {
                Provider = HistoricalImageryProviderType.GoogleEarth,
                DisplayDate = "2024-03-01",
                VersionId = "2024-03-01"
            }) { IsSelected = true },
            new HistoricalVersionSelectionItem(new HistoricalVersionItem
            {
                Provider = HistoricalImageryProviderType.GoogleEarth,
                DisplayDate = "2024-02-01",
                VersionId = "2024-02-01"
            }) { IsSelected = true }
        };

        var requests = HistoricalBatchDownloadPlanner.CreateRequests(
            HistoricalImageryProviderType.GoogleEarth,
            HistoricalAreaSourceType.CurrentView,
            selections,
            18,
            @"D:\Output",
            GoogleNearestDateFallbackMode.SeparateOutputs);

        Assert.Collection(
            requests,
            request => Assert.Equal(@"D:\Output\Google_2024-03-01_Z18.tif", request.OutputFilePath),
            request => Assert.Equal(@"D:\Output\Google_2024-02-01_Z18.tif", request.OutputFilePath));
        Assert.All(requests, request => Assert.Equal(18, request.ZoomLevel));
        Assert.All(requests, request => Assert.Equal(GoogleNearestDateFallbackMode.SeparateOutputs, request.GoogleNearestDateFallbackMode));
    }

    [Fact]
    public void CreateRequests_IgnoresUnselectedVersions()
    {
        var selections = new[]
        {
            new HistoricalVersionSelectionItem(new HistoricalVersionItem
            {
                Provider = HistoricalImageryProviderType.Wayback,
                DisplayDate = "2024-03-01",
                VersionId = "v1"
            }) { IsSelected = false }
        };

        var requests = HistoricalBatchDownloadPlanner.CreateRequests(
            HistoricalImageryProviderType.Wayback,
            HistoricalAreaSourceType.CurrentView,
            selections,
            18,
            @"D:\Output",
            GoogleNearestDateFallbackMode.MixedSingleOutput);

        Assert.Empty(requests);
    }
}
