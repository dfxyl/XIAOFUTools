#nullable enable

using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace XIAOFUTools.Tools.HistoricalImageryDownload.Infrastructure
{
    internal static class WaybackHostPreferenceResolver
    {
        private static readonly object Gate = new();
        private static readonly string[] Hosts =
        [
            WaybackUrlNormalizer.WaybackHostA,
            WaybackUrlNormalizer.WaybackHostB
        ];

        private static readonly TimeSpan PreferenceLifetime = TimeSpan.FromMinutes(20);
        private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);

        private static string? _preferredHost;
        private static DateTime _expiresAtUtc;
        private static Task<string>? _pendingResolutionTask;

        public static string GetCachedPreferredHostOrDefault()
        {
            lock (Gate)
            {
                if (IsPreferenceValid())
                {
                    return _preferredHost!;
                }

                return WaybackUrlNormalizer.WaybackHostB;
            }
        }

        public static void RememberPreferredHost(string? hostOrUrl)
        {
            var host = WaybackUrlNormalizer.TryExtractWaybackHost(hostOrUrl);
            if (host == null)
            {
                return;
            }

            lock (Gate)
            {
                _preferredHost = host;
                _expiresAtUtc = DateTime.UtcNow.Add(PreferenceLifetime);
            }
        }

        public static Task<string> GetPreferredHostAsync(HttpClient httpClient, CancellationToken cancellationToken = default)
        {
            lock (Gate)
            {
                if (IsPreferenceValid())
                {
                    return Task.FromResult(_preferredHost!);
                }

                _pendingResolutionTask ??= ResolvePreferredHostAsync(httpClient, cancellationToken);
                return _pendingResolutionTask;
            }
        }

        private static async Task<string> ResolvePreferredHostAsync(HttpClient httpClient, CancellationToken cancellationToken)
        {
            try
            {
                var preferredHost = await WaybackHostSelector.SelectPreferredHostAsync(
                    Hosts,
                    (host, token) => MeasureLatencyAsync(httpClient, host, token),
                    cancellationToken);

                RememberPreferredHost(preferredHost);
                return preferredHost;
            }
            finally
            {
                lock (Gate)
                {
                    _pendingResolutionTask = null;
                }
            }
        }

        private static async Task<TimeSpan?> MeasureLatencyAsync(HttpClient httpClient, string host, CancellationToken cancellationToken)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(ProbeTimeout);

            try
            {
                var uri = new UriBuilder(Uri.UriSchemeHttps, host)
                {
                    Path = "/arcgis/rest/info",
                    Query = "f=json"
                }.Uri;

                var stopwatch = Stopwatch.StartNew();
                using var response = await httpClient.GetAsync(uri, timeoutCts.Token);
                stopwatch.Stop();

                return response.IsSuccessStatusCode ? stopwatch.Elapsed : null;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsPreferenceValid()
            => !string.IsNullOrWhiteSpace(_preferredHost)
               && Hosts.Contains(_preferredHost, StringComparer.OrdinalIgnoreCase)
               && DateTime.UtcNow <= _expiresAtUtc;
    }
}
