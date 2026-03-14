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
            if (!string.IsNullOrWhiteSpace(cacheDirectory))
            {
                var cachePath = GetCachePath(cacheDirectory, cacheKey ?? url);
                if (IsCacheValid(cachePath, cacheDuration))
                {
                    return await File.ReadAllBytesAsync(cachePath, cancellationToken);
                }

                using var response = await HttpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();
                var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                Directory.CreateDirectory(cacheDirectory);
                await File.WriteAllBytesAsync(cachePath, bytes, cancellationToken);
                return bytes;
            }

            using (var response = await HttpClient.GetAsync(url, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsByteArrayAsync(cancellationToken);
            }
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
            using var content = new FormUrlEncodedContent(formValues);
            using var response = await HttpClient.PostAsync(url, content, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(cancellationToken);
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
