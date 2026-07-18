#nullable enable

using System.IO;

namespace XIAOFUTools.Features.General.HistoricalImageryDownload.Infrastructure
{
    internal static class CachePathProvider
    {
        public static string GetRootPath(string localAppDataPath)
            => Path.Combine(localAppDataPath, "XIAOFUTools", "HistoricalImageryCache");

        public static string GetProviderPath(string rootPath, HistoricalImageryProviderType provider)
            => Path.Combine(rootPath, provider.ToString());
    }
}
