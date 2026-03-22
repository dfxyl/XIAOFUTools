#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XIAOFUTools.Tools.HistoricalImageryDownload.Infrastructure
{
    internal sealed class HistoricalHttpClient
    {
        private static readonly HttpClient HttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        public async Task<byte[]> GetBytesAsync(
            string url,
            string? cacheDirectory = null,
            string? cacheKey = null,
            TimeSpan? cacheDuration = null,
            CancellationToken cancellationToken = default)
        {
            var normalizedCacheKey = WaybackUrlNormalizer.CanonicalizeCacheKey(cacheKey ?? url);

            if (!string.IsNullOrWhiteSpace(cacheDirectory))
            {
                var cachePath = GetCachePath(cacheDirectory, normalizedCacheKey);
                if (IsCacheValid(cachePath, cacheDuration))
                {
                    return await File.ReadAllBytesAsync(cachePath, cancellationToken);
                }

                var bytes = await GetBytesCoreAsync(url, cancellationToken);
                Directory.CreateDirectory(cacheDirectory);
                await File.WriteAllBytesAsync(cachePath, bytes, cancellationToken);
                return bytes;
            }

            return await GetBytesCoreAsync(url, cancellationToken);
        }

        public async Task<string> GetStringAsync(
            string url,
            string? cacheDirectory = null,
            string? cacheKey = null,
            TimeSpan? cacheDuration = null,
            CancellationToken cancellationToken = default)
        {
            var bytes = await GetBytesAsync(url, cacheDirectory, cacheKey, cacheDuration, cancellationToken);
            return Encoding.UTF8.GetString(bytes);
        }

        public async Task<string> PostFormAsync(
            string url,
            IReadOnlyDictionary<string, string> formValues,
            CancellationToken cancellationToken = default)
        {
            Exception? lastError = null;
            foreach (var candidateUrl in await GetCandidateUrlsAsync(url, cancellationToken))
            {
                try
                {
                    using var content = new FormUrlEncodedContent(formValues);
                    using var response = await HttpClient.PostAsync(candidateUrl, content, cancellationToken);
                    response.EnsureSuccessStatusCode();
                    WaybackHostPreferenceResolver.RememberPreferredHost(candidateUrl);
                    return await response.Content.ReadAsStringAsync(cancellationToken);
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
                {
                    lastError = ex;
                }
            }

            throw lastError ?? new HttpRequestException($"Request failed: {url}");
        }

        private static async Task<byte[]> GetBytesCoreAsync(string url, CancellationToken cancellationToken)
        {
            Exception? lastError = null;
            foreach (var candidateUrl in await GetCandidateUrlsAsync(url, cancellationToken))
            {
                try
                {
                    using var response = await HttpClient.GetAsync(candidateUrl, cancellationToken);
                    response.EnsureSuccessStatusCode();
                    WaybackHostPreferenceResolver.RememberPreferredHost(candidateUrl);
                    return await response.Content.ReadAsByteArrayAsync(cancellationToken);
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
                {
                    lastError = ex;
                }
            }

            throw lastError ?? new HttpRequestException($"Request failed: {url}");
        }

        private static async Task<IReadOnlyList<string>> GetCandidateUrlsAsync(string url, CancellationToken cancellationToken)
        {
            var preferredHost = WaybackUrlNormalizer.TryExtractWaybackHost(url) == null
                ? (string?)null
                : await WaybackHostPreferenceResolver.GetPreferredHostAsync(HttpClient, cancellationToken);

            return preferredHost == null
                ? [url]
                : WaybackUrlNormalizer.BuildCandidateUrls(url, preferredHost);
        }

        private static string GetCachePath(string cacheDirectory, string cacheKey)
        {
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cacheKey))).ToLowerInvariant();
            return Path.Combine(cacheDirectory, $"{hash}.cache");
        }

        private static bool IsCacheValid(string cachePath, TimeSpan? cacheDuration)
        {
            if (!File.Exists(cachePath))
            {
                return false;
            }

            if (cacheDuration == null)
            {
                return true;
            }

            return DateTime.UtcNow - File.GetLastWriteTimeUtc(cachePath) <= cacheDuration.Value;
        }
    }
}
