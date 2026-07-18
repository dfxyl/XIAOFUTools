#nullable enable

using System;

namespace XIAOFUTools.Features.General.InternetTileDownload.Services
{
    internal static class InternetTileDownloadCompletionGuard
    {
        public static void EnsureHasAnyTile(int downloadedTileCount, int totalTileCount)
        {
            if (downloadedTileCount <= 0 && totalTileCount > 0)
            {
                throw new InvalidOperationException("未成功下载任何瓦片，请检查服务链接、权限令牌或网络连通性后重试。");
            }
        }
    }
}
