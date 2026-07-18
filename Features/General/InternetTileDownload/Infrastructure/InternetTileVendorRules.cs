#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace XIAOFUTools.Features.General.InternetTileDownload.Infrastructure
{
    internal sealed class InternetTileVendorNormalizationResult
    {
        public string NormalizedUrl { get; init; } = string.Empty;

        public InternetTileVendorKind VendorKind { get; init; } = InternetTileVendorKind.Unknown;

        public InternetTileRowOrigin RowOrigin { get; init; } = InternetTileRowOrigin.Top;

        public IReadOnlyList<string> Subdomains { get; init; } = Array.Empty<string>();
    }

    internal static class InternetTileVendorRules
    {
        private static readonly Regex NumericRangeRegex = new("\\[(?<prefix>[A-Za-z]+)(?<start>\\d+)-(?<endPrefix>[A-Za-z]+)(?<end>\\d+)\\]", RegexOptions.Compiled);
        private static readonly Regex AlphaRangeRegex = new("\\[(?<start>[a-z])-(?<end>[a-z])\\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex CsvRegex = new("\\[(?<values>[A-Za-z0-9]+(?:,[A-Za-z0-9]+)+)\\]", RegexOptions.Compiled);

        public static InternetTileVendorNormalizationResult Normalize(string rawUrl)
        {
            var normalizedUrl = rawUrl.Trim();
            var vendor = DetectVendor(normalizedUrl);
            var rowOrigin = DetectRowOrigin(normalizedUrl);
            var subdomains = ExtractSubdomains(ref normalizedUrl, vendor);

            normalizedUrl = normalizedUrl
                .Replace("{-y}", "{row}", StringComparison.OrdinalIgnoreCase)
                .Replace("{-row}", "{row}", StringComparison.OrdinalIgnoreCase)
                .Replace("{tmsy}", "{row}", StringComparison.OrdinalIgnoreCase)
                .Replace("{reversey}", "{row}", StringComparison.OrdinalIgnoreCase);

            return new InternetTileVendorNormalizationResult
            {
                NormalizedUrl = normalizedUrl,
                VendorKind = vendor,
                RowOrigin = rowOrigin,
                Subdomains = subdomains
            };
        }

        private static InternetTileVendorKind DetectVendor(string rawUrl)
        {
            if (rawUrl.Contains("tianditu.gov.cn", StringComparison.OrdinalIgnoreCase))
            {
                return InternetTileVendorKind.Tianditu;
            }

            if (rawUrl.Contains("arcgisonline.com", StringComparison.OrdinalIgnoreCase) ||
                rawUrl.Contains("/MapServer/tile/", StringComparison.OrdinalIgnoreCase) ||
                rawUrl.Contains("/ImageServer/tile/", StringComparison.OrdinalIgnoreCase))
            {
                return InternetTileVendorKind.ArcGisOnline;
            }

            if (rawUrl.Contains("maps/vt", StringComparison.OrdinalIgnoreCase) ||
                rawUrl.Contains("google", StringComparison.OrdinalIgnoreCase) ||
                rawUrl.Contains("googlecnapps", StringComparison.OrdinalIgnoreCase))
            {
                return InternetTileVendorKind.GoogleLike;
            }

            if (rawUrl.Contains("jilin", StringComparison.OrdinalIgnoreCase) ||
                rawUrl.Contains("jl1", StringComparison.OrdinalIgnoreCase) ||
                rawUrl.Contains("jlyh", StringComparison.OrdinalIgnoreCase))
            {
                return InternetTileVendorKind.Jilin1;
            }

            return InternetTileVendorKind.Unknown;
        }

        private static InternetTileRowOrigin DetectRowOrigin(string rawUrl)
        {
            return rawUrl.Contains("{-y}", StringComparison.OrdinalIgnoreCase) ||
                   rawUrl.Contains("{-row}", StringComparison.OrdinalIgnoreCase) ||
                   rawUrl.Contains("{tmsy}", StringComparison.OrdinalIgnoreCase) ||
                   rawUrl.Contains("{reversey}", StringComparison.OrdinalIgnoreCase)
                ? InternetTileRowOrigin.Bottom
                : InternetTileRowOrigin.Top;
        }

        private static IReadOnlyList<string> ExtractSubdomains(ref string normalizedUrl, InternetTileVendorKind vendor)
        {
            var values = TryParseSubdomains(normalizedUrl);
            if (values.Count > 0)
            {
                normalizedUrl = NormalizeSubdomainPlaceholder(normalizedUrl);
                return values;
            }

            if (normalizedUrl.Contains("{s}", StringComparison.OrdinalIgnoreCase) && vendor == InternetTileVendorKind.Tianditu)
            {
                normalizedUrl = normalizedUrl.Replace("{s}", "{subdomain}", StringComparison.OrdinalIgnoreCase);
                return Enumerable.Range(0, 8).Select(index => $"t{index}").ToArray();
            }

            if (normalizedUrl.Contains("{s}", StringComparison.OrdinalIgnoreCase))
            {
                normalizedUrl = normalizedUrl.Replace("{s}", "{subdomain}", StringComparison.OrdinalIgnoreCase);
                return ["a", "b", "c"];
            }

            return Array.Empty<string>();
        }

        private static List<string> TryParseSubdomains(string rawUrl)
        {
            var numericMatch = NumericRangeRegex.Match(rawUrl);
            if (numericMatch.Success &&
                string.Equals(numericMatch.Groups["prefix"].Value, numericMatch.Groups["endPrefix"].Value, StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(numericMatch.Groups["start"].Value, out var start) &&
                int.TryParse(numericMatch.Groups["end"].Value, out var end))
            {
                var prefix = numericMatch.Groups["prefix"].Value;
                return Enumerable.Range(Math.Min(start, end), Math.Abs(end - start) + 1)
                    .Select(index => prefix + index)
                    .ToList();
            }

            var alphaMatch = AlphaRangeRegex.Match(rawUrl);
            if (alphaMatch.Success)
            {
                var startChar = alphaMatch.Groups["start"].Value[0];
                var endChar = alphaMatch.Groups["end"].Value[0];
                var rangeStart = Math.Min(startChar, endChar);
                var rangeEnd = Math.Max(startChar, endChar);
                return Enumerable.Range(rangeStart, rangeEnd - rangeStart + 1)
                    .Select(value => ((char)value).ToString())
                    .ToList();
            }

            var csvMatch = CsvRegex.Match(rawUrl);
            if (csvMatch.Success)
            {
                return csvMatch.Groups["values"].Value
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();
            }

            return new List<string>();
        }

        private static string NormalizeSubdomainPlaceholder(string rawUrl)
        {
            var normalized = NumericRangeRegex.Replace(rawUrl, "{subdomain}");
            normalized = AlphaRangeRegex.Replace(normalized, "{subdomain}");
            normalized = CsvRegex.Replace(normalized, "{subdomain}");
            return normalized;
        }
    }
}
