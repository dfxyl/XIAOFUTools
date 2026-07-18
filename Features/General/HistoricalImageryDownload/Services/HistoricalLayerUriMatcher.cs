#nullable enable

using System;
using System.IO;
using System.Text.RegularExpressions;

namespace XIAOFUTools.Features.General.HistoricalImageryDownload.Services
{
    public static class HistoricalLayerUriMatcher
    {
        private static readonly Regex OutputFamilyRegex = new(@"^(?<prefix>.+?)_(?<date>\d{4}-\d{2}-\d{2})_(?<zoom>Z\d+)$", RegexOptions.Compiled);

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

        public static bool IsOutputFamilyMatch(string outputPath, string? layerUri)
        {
            if (string.IsNullOrWhiteSpace(outputPath) || string.IsNullOrWhiteSpace(layerUri))
            {
                return false;
            }

            var normalizedPath = NormalizePath(outputPath);
            var normalizedUri = NormalizePath(layerUri);
            if (string.Equals(normalizedPath, normalizedUri, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.Equals(Path.GetDirectoryName(normalizedPath), Path.GetDirectoryName(normalizedUri), StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetExtension(normalizedPath), Path.GetExtension(normalizedUri), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var pathMatch = OutputFamilyRegex.Match(Path.GetFileNameWithoutExtension(normalizedPath));
            var uriMatch = OutputFamilyRegex.Match(Path.GetFileNameWithoutExtension(normalizedUri));
            if (!pathMatch.Success || !uriMatch.Success)
            {
                return false;
            }

            return string.Equals(pathMatch.Groups["prefix"].Value, uriMatch.Groups["prefix"].Value, StringComparison.OrdinalIgnoreCase)
                && string.Equals(pathMatch.Groups["zoom"].Value, uriMatch.Groups["zoom"].Value, StringComparison.OrdinalIgnoreCase);
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
