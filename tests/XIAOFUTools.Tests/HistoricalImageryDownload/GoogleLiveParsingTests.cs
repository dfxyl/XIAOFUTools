using System;
using System.Net.Http;
using System.Linq;
using System.Threading;
using Xunit.Abstractions;
using XIAOFUTools.Features.General.HistoricalImageryDownload.Infrastructure.Google;
using XIAOFUTools.Features.General.HistoricalImageryDownload.Services;

namespace XIAOFUTools.Tests.HistoricalImageryDownload;

[Trait("Category", "LiveNetwork")]
public class GoogleLiveParsingTests
{
    private readonly ITestOutputHelper _output;

    public GoogleLiveParsingTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ParseCurrentDbRootEnvelope_ReturnsPositiveQuadtreeVersion()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var payload = await httpClient.GetByteArrayAsync(
            "https://khmdb.google.com/dbRoot.v5?db=tm&hl=en&gl=us&output=proto",
            timeout.Token);

        var info = GoogleDbRootParser.ParseEncryptedEnvelope(payload);

        Assert.True(info.QuadtreeVersion > 0);
    }

    [Fact]
    public async Task ParseRootQuadtreePacket_ReturnsNodes()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var dbRootPayload = await httpClient.GetByteArrayAsync(
            "https://khmdb.google.com/dbRoot.v5?db=tm&hl=en&gl=us&output=proto",
            timeout.Token);
        var envelope = GoogleDbRootParser.ParseEnvelope(dbRootPayload);
        var dbRootInfo = GoogleDbRootParser.ParseEncryptedEnvelope(dbRootPayload);
        var packetPayload = await httpClient.GetByteArrayAsync(
            $"https://khmdb.google.com/flatfile?db=tm&qp-0-q.{dbRootInfo.QuadtreeVersion}",
            timeout.Token);
        var decodedPacketPayload = GooglePacketCodec.DecryptAndDecompress(packetPayload, envelope.EncryptionData);

        _output.WriteLine($"version={dbRootInfo.QuadtreeVersion}");
        _output.WriteLine($"packet-length={packetPayload.Length}");
        _output.WriteLine(BitConverter.ToString(packetPayload.Take(Math.Min(64, packetPayload.Length)).ToArray()));

        var packet = GoogleQuadtreePacketParser.Parse(decodedPacketPayload);

        Assert.NotEmpty(packet.Nodes);
    }

    [Fact]
    public async Task QueryVersionsAtBeijing_ReturnsHistoricalVersions()
    {
        var provider = new GoogleHistoricalImageryProvider();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var versions = await provider.QueryVersionsAsync(new HistoricalVersionQuery
        {
            Longitude = 116.391,
            Latitude = 39.907,
            ZoomLevel = 18
        }, timeout.Token);

        Assert.NotEmpty(versions);
    }
}
