#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace XIAOFUTools.Shared.IO.Wayback
{
    internal static class WaybackHostSelector
    {
        public static async Task<string> SelectPreferredHostAsync(
            IReadOnlyList<string> candidateHosts,
            Func<string, CancellationToken, Task<TimeSpan?>> measureLatencyAsync,
            CancellationToken cancellationToken = default)
        {
            if (candidateHosts == null || candidateHosts.Count == 0)
            {
                throw new ArgumentException("At least one Wayback host is required.", nameof(candidateHosts));
            }

            if (measureLatencyAsync == null)
            {
                throw new ArgumentNullException(nameof(measureLatencyAsync));
            }

            var measurements = await Task.WhenAll(candidateHosts.Select(async host =>
            {
                var latency = await measureLatencyAsync(host, cancellationToken);
                return new HostMeasurement(host, latency);
            }));

            return measurements
                .Where(item => item.Latency.HasValue)
                .OrderBy(item => item.Latency!.Value)
                .Select(item => item.Host)
                .FirstOrDefault() ?? candidateHosts[0];
        }

        private sealed record HostMeasurement(string Host, TimeSpan? Latency);
    }
}
