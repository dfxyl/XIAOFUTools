#nullable enable

using System;
using System.IO;

namespace XIAOFUTools.Tools.HistoricalImageryDownload.Services
{
    public static class HistoricalLayerUriMatcher
    {
        public static bool IsMatch(string outputPath, string? layerUri)
        {
            if (string.IsNullOrWhiteSpace(outputPath) || string.IsNullOrWhiteSpace(layerUri))
            {
                return false;
            }

            var normalizedPath = NormalizePath(outputPath);
            var normalizedUri = NormalizePath(layerUri);
            return string.Equals(normalizedPath, normalizedUri, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string value)
        {
            var trimmed = value.Trim();

            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && uri.IsFile)
            {
                return Path.GetFullPath(uri.LocalPath);
            }

            return Path.GetFullPath(trimmed.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
