using System;
using XIAOFUTools.Tools.InternetTileDownload.Services;
using Xunit;

namespace XIAOFUTools.Tests.InternetTileDownload;

public class InternetTileDownloadCompletionGuardTests
{
    [Fact]
    public void EnsureHasAnyTile_ThrowsWhenNothingDownloaded()
    {
        Assert.Throws<InvalidOperationException>(() => InternetTileDownloadCompletionGuard.EnsureHasAnyTile(0, 12));
    }

    [Fact]
    public void EnsureHasAnyTile_AllowsPartialOrCompleteDownload()
    {
        InternetTileDownloadCompletionGuard.EnsureHasAnyTile(1, 12);
        InternetTileDownloadCompletionGuard.EnsureHasAnyTile(12, 12);
    }
}
