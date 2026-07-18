#nullable enable

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using XIAOFUTools.Features.General.InternetTileDownload.Services;

namespace XIAOFUTools.Features.General.InternetTileDownload.Infrastructure
{
    internal sealed class InternetTileHttpClient : IInternetTileHttpClient
    {
        private static readonly HttpClient HttpClient = CreateHttpClient();

        public async Task<byte[]> GetBytesAsync(string url, CancellationToken cancellationToken = default)
        {
            using var request = CreateRequest(url);
            using var response = await HttpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }

        public async Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default)
        {
            using var request = CreateRequest(url);
            using var response = await HttpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
            return client;
        }

        private static HttpRequestMessage CreateRequest(string url)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                request.Headers.Referrer = new UriBuilder(uri.Scheme, uri.Host, uri.Port).Uri;
            }

            return request;
        }
    }
}
