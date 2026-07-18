using System.Net;
using System.Net.Http;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Infrastructure;

namespace XIAOFUTools.Tests.OvertureLoader;

public sealed class OvertureReleaseClientTests
{
    [Fact]
    public async Task GetLatestReleaseAsync_ParsesLatestRelease()
    {
        using var httpClient = CreateClient((_, _) =>
            Response(HttpStatusCode.OK, "{\"latest\":\"2026-07-01.0\",\"releases\":[]}"));
        var client = new OvertureReleaseClient(httpClient, initialRetryDelay: TimeSpan.Zero);

        var release = await client.GetLatestReleaseAsync(CancellationToken.None);

        Assert.Equal("2026-07-01.0", release);
    }

    [Fact]
    public async Task GetLatestReleaseAsync_RetriesTransientFailure()
    {
        var attempts = 0;
        using var httpClient = CreateClient((_, _) =>
        {
            attempts++;
            return attempts == 1
                ? Response(HttpStatusCode.ServiceUnavailable, "unavailable")
                : Response(HttpStatusCode.OK, "{\"latest\":\"2026-07-01.0\"}");
        });
        var client = new OvertureReleaseClient(httpClient, initialRetryDelay: TimeSpan.Zero);

        var release = await client.GetLatestReleaseAsync(CancellationToken.None);

        Assert.Equal("2026-07-01.0", release);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task GetLatestReleaseAsync_RejectsMissingLatestValue()
    {
        using var httpClient = CreateClient((_, _) =>
            Response(HttpStatusCode.OK, "{\"releases\":[]}"));
        var client = new OvertureReleaseClient(
            httpClient,
            maxAttempts: 1,
            initialRetryDelay: TimeSpan.Zero);

        await Assert.ThrowsAsync<System.IO.InvalidDataException>(() =>
            client.GetLatestReleaseAsync(CancellationToken.None));
    }

    private static HttpClient CreateClient(
        Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responseFactory)
    {
        return new HttpClient(new StubHttpMessageHandler(responseFactory));
    }

    private static HttpResponseMessage Response(HttpStatusCode statusCode, string content)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content)
        };
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(responseFactory(request, cancellationToken));
        }
    }
}
