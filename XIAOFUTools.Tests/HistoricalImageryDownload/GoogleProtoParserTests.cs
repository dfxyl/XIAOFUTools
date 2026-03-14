using System;
using System.IO;
using Google.Protobuf;
using XIAOFUTools.Tools.HistoricalImageryDownload.Infrastructure.Google;

namespace XIAOFUTools.Tests.HistoricalImageryDownload;

public class GoogleProtoParserTests
{
    [Fact]
    public void ParseDbRootQuadtreeVersion_ReturnsExpectedValue()
    {
        var payload = BuildDbRootPayload(321);

        var info = GoogleDbRootParser.ParseDbRoot(payload);

        Assert.Equal((uint)321, info.QuadtreeVersion);
    }

    [Fact]
    public void ParseQuadtreePacket_ReturnsLayerEpochAndDatedTiles()
    {
        var encodedDate = GoogleQuadtreeCodec.EncodeDate(new DateOnly(2026, 2, 26));
        var payload = BuildQuadtreePacketPayload(encodedDate);

        var packet = GoogleQuadtreePacketParser.Parse(payload);

        Assert.Equal(77, packet.PacketEpoch);
        var node = Assert.Single(packet.Nodes);
        Assert.Equal(5, node.Index);
        Assert.Equal(88, node.CacheNodeEpoch);

        var historyLayer = Assert.Single(node.Layers, layer => layer.Type == GoogleLayerType.ImageryHistory);
        var datedTile = Assert.Single(historyLayer.DatedTiles);
        Assert.Equal(new DateOnly(2026, 2, 26), datedTile.Date);
        Assert.Equal(99, datedTile.TileEpoch);
        Assert.Equal(7, datedTile.Provider);

        var imageryLayer = Assert.Single(node.Layers, layer => layer.Type == GoogleLayerType.Imagery);
        Assert.Equal(123, imageryLayer.LayerEpoch);
    }

    private static byte[] BuildDbRootPayload(uint quadtreeVersion)
    {
        using var stream = new MemoryStream();
        using var output = new CodedOutputStream(stream, leaveOpen: true);

        output.WriteTag(13, WireFormat.WireType.LengthDelimited);

        using var innerStream = new MemoryStream();
        using var innerOutput = new CodedOutputStream(innerStream, leaveOpen: true);
        innerOutput.WriteTag(1, WireFormat.WireType.Varint);
        innerOutput.WriteUInt32(quadtreeVersion);
        innerOutput.Flush();

        var innerBytes = innerStream.ToArray();
        output.WriteBytes(ByteString.CopyFrom(innerBytes));
        output.Flush();

        return stream.ToArray();
    }

    private static byte[] BuildQuadtreePacketPayload(int encodedDate)
    {
        using var stream = new MemoryStream();
        using var output = new CodedOutputStream(stream, leaveOpen: true);

        output.WriteTag(1, WireFormat.WireType.Varint);
        output.WriteInt32(77);

        output.WriteTag(2, WireFormat.WireType.StartGroup);
        output.WriteTag(3, WireFormat.WireType.Varint);
        output.WriteInt32(5);
        output.WriteTag(4, WireFormat.WireType.LengthDelimited);

        using var nodeStream = new MemoryStream();
        using var nodeOutput = new CodedOutputStream(nodeStream, leaveOpen: true);
        nodeOutput.WriteTag(2, WireFormat.WireType.Varint);
        nodeOutput.WriteInt32(88);

        WriteImageryHistoryLayer(nodeOutput, encodedDate);
        WriteImageryLayer(nodeOutput);
        nodeOutput.Flush();

        var nodeBytes = nodeStream.ToArray();
        output.WriteBytes(ByteString.CopyFrom(nodeBytes));
        output.WriteTag(2, WireFormat.WireType.EndGroup);
        output.Flush();

        return stream.ToArray();
    }

    private static void WriteImageryHistoryLayer(CodedOutputStream output, int encodedDate)
    {
        output.WriteTag(3, WireFormat.WireType.LengthDelimited);

        using var layerStream = new MemoryStream();
        using var layerOutput = new CodedOutputStream(layerStream, leaveOpen: true);
        layerOutput.WriteTag(1, WireFormat.WireType.Varint);
        layerOutput.WriteEnum((int)GoogleLayerType.ImageryHistory);
        layerOutput.WriteTag(4, WireFormat.WireType.LengthDelimited);

        using var datesStream = new MemoryStream();
        using var datesOutput = new CodedOutputStream(datesStream, leaveOpen: true);
        datesOutput.WriteTag(1, WireFormat.WireType.LengthDelimited);

        using var tileStream = new MemoryStream();
        using var tileOutput = new CodedOutputStream(tileStream, leaveOpen: true);
        tileOutput.WriteTag(1, WireFormat.WireType.Varint);
        tileOutput.WriteInt32(encodedDate);
        tileOutput.WriteTag(2, WireFormat.WireType.Varint);
        tileOutput.WriteInt32(99);
        tileOutput.WriteTag(3, WireFormat.WireType.Varint);
        tileOutput.WriteInt32(7);
        tileOutput.Flush();

        var tileBytes = tileStream.ToArray();
        datesOutput.WriteBytes(ByteString.CopyFrom(tileBytes));
        datesOutput.Flush();

        var datesBytes = datesStream.ToArray();
        layerOutput.WriteBytes(ByteString.CopyFrom(datesBytes));
        layerOutput.Flush();

        var layerBytes = layerStream.ToArray();
        output.WriteBytes(ByteString.CopyFrom(layerBytes));
    }

    private static void WriteImageryLayer(CodedOutputStream output)
    {
        output.WriteTag(3, WireFormat.WireType.LengthDelimited);

        using var layerStream = new MemoryStream();
        using var layerOutput = new CodedOutputStream(layerStream, leaveOpen: true);
        layerOutput.WriteTag(1, WireFormat.WireType.Varint);
        layerOutput.WriteEnum((int)GoogleLayerType.Imagery);
        layerOutput.WriteTag(2, WireFormat.WireType.Varint);
        layerOutput.WriteInt32(123);
        layerOutput.Flush();

        var layerBytes = layerStream.ToArray();
        output.WriteBytes(ByteString.CopyFrom(layerBytes));
    }
}
