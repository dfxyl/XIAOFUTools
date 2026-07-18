using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Application;

namespace XIAOFUTools.Features.DataManagement.OvertureLoader.Infrastructure
{
    internal sealed class OvertureReleaseClient : IOvertureReleaseClient
    {
        private static readonly Uri ReleasesUri =
            new("https://labs.overturemaps.org/data/releases.json");

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static readonly HttpClient SharedHttpClient = CreateHttpClient();

        private readonly HttpClient _httpClient;
        private readonly int _maxAttempts;
        private readonly TimeSpan _initialRetryDelay;

        internal OvertureReleaseClient(
            HttpClient httpClient,
            int maxAttempts = 3,
            TimeSpan? initialRetryDelay = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            if (maxAttempts < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxAttempts));
            }

            _maxAttempts = maxAttempts;
            _initialRetryDelay = initialRetryDelay ?? TimeSpan.FromSeconds(1);
        }

        public static OvertureReleaseClient CreateDefault()
        {
            return new OvertureReleaseClient(SharedHttpClient);
        }

        public async Task<string> GetLatestReleaseAsync(CancellationToken cancellationToken)
        {
            var retryDelay = _initialRetryDelay;
            for (var attempt = 1; attempt <= _maxAttempts; attempt++)
            {
                try
                {
                    var json = await _httpClient
                        .GetStringAsync(ReleasesUri, cancellationToken)
                        .ConfigureAwait(false);
                    var catalog = JsonSerializer.Deserialize<ReleaseCatalog>(json, JsonOptions)
                        ?? throw new InvalidDataException("Overture 版本目录响应为空。");
                    if (string.IsNullOrWhiteSpace(catalog.Latest))
                    {
                        throw new InvalidDataException("Overture 版本目录缺少 latest 字段。");
                    }

                    return catalog.Latest;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch when (attempt < _maxAttempts)
                {
                    await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
                    retryDelay += retryDelay;
                }
            }

            throw new InvalidOperationException("无法获取 Overture 最新版本。");
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ArcGIS-Pro-Overture-Plugin/1.0");
            return client;
        }

        private sealed class ReleaseCatalog
        {
            public string Latest { get; set; }
        }
    }
}
