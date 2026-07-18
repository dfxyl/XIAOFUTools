#nullable enable

using System;

using System.Collections.Generic;
using System.Linq;

namespace XIAOFUTools.Shared.IO.Wayback
{
    internal static class WaybackUrlNormalizer
    {
        public const string WaybackHost = "wayback.maptiles.arcgis.com";
        public const string WaybackHostA = "wayback-a.maptiles.arcgis.com";
        public const string WaybackHostB = "wayback-b.maptiles.arcgis.com";

        private static readonly string[] KnownHosts = [WaybackHost, WaybackHostA, WaybackHostB];

        public static string? Normalize(string? url)
            => Normalize(url, WaybackHostPreferenceResolver.GetCachedPreferredHostOrDefault());

        public static string? Normalize(string? url, string? preferredHost)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return url;
            }

            if (TryExtractWaybackHost(url) == null)
            {
                return url;
            }

            var targetHost = IsWaybackHost(preferredHost) ? preferredHost! : WaybackHostB;
            return ReplaceKnownHost(url, targetHost);
        }

        public static bool AreEquivalent(string? left, string? right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return false;
            }

            return string.Equals(Canonicalize(left), Canonicalize(right), StringComparison.OrdinalIgnoreCase);
        }

        public static string CanonicalizeCacheKey(string url)
            => Canonicalize(url) ?? url;

        public static IReadOnlyList<string> BuildCandidateUrls(string url, string preferredHost)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !IsWaybackHost(uri.Host))
            {
                return [url];
            }

            var normalizedPreferred = Normalize(url, preferredHost)!;
            var alternateHost = string.Equals(preferredHost, WaybackHostA, StringComparison.OrdinalIgnoreCase)
                ? WaybackHostB
                : WaybackHostA;
            var normalizedAlternate = Normalize(url, alternateHost)!;

            return new[] { normalizedPreferred, normalizedAlternate }
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static string? TryExtractWaybackHost(string? hostOrUrl)
        {
            if (string.IsNullOrWhiteSpace(hostOrUrl))
            {
                return null;
            }

            if (IsWaybackHost(hostOrUrl))
            {
                return hostOrUrl;
            }

            return Uri.TryCreate(hostOrUrl, UriKind.Absolute, out var uri) && IsWaybackHost(uri.Host)
                ? uri.Host
                : null;
        }

        private static string? Canonicalize(string? url)
        {
            if (string.IsNullOrWhiteSpace(url) || TryExtractWaybackHost(url) == null)
            {
                return url;
            }

            return ReplaceKnownHost(url, WaybackHost);
        }

        private static string ReplaceKnownHost(string url, string targetHost)
        {
            foreach (var knownHost in KnownHosts)
            {
                if (url.Contains(knownHost, StringComparison.OrdinalIgnoreCase))
                {
                    return url.Replace(knownHost, targetHost, StringComparison.OrdinalIgnoreCase);
                }
            }

            return url;
        }

        private static bool IsWaybackHost(string? host)
            => !string.IsNullOrWhiteSpace(host) && KnownHosts.Contains(host, StringComparer.OrdinalIgnoreCase);
    }
}
