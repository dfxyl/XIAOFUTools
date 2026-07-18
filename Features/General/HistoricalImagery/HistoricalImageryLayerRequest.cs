#nullable disable
using System;

namespace XIAOFUTools.Features.General.HistoricalImagery
{
    internal sealed record HistoricalImageryLayerRequest(string LayerName, Uri LayerUri);

    internal static class HistoricalImageryLayerRequestFactory
    {
        public static bool TryCreate(string releaseDate, string itemTitle, string url, int releaseNum, out HistoricalImageryLayerRequest request, out string errorMessage)
        {
            request = null;
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(url))
            {
                errorMessage = "所选历史影像版本没有可用的图层地址。";
                return false;
            }

            var normalizedUrl = WaybackUrlNormalizer.Normalize(url);
            if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var layerUri))
            {
                errorMessage = "所选历史影像版本的图层地址无效。";
                return false;
            }

            request = new HistoricalImageryLayerRequest(BuildLayerName(releaseDate, itemTitle, releaseNum), layerUri);
            return true;
        }

        private static string BuildLayerName(string releaseDate, string itemTitle, int releaseNum)
        {
            if (!string.IsNullOrWhiteSpace(releaseDate))
            {
                return $"Wayback {releaseDate}";
            }

            if (!string.IsNullOrWhiteSpace(itemTitle))
            {
                return $"Wayback {itemTitle}";
            }

            return $"Wayback {releaseNum}";
        }
    }
}
