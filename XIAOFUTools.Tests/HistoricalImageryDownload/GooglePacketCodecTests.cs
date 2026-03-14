using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using XIAOFUTools.Tools.HistoricalImageryDownload.Infrastructure.Google;

namespace XIAOFUTools.Tests.HistoricalImageryDownload;

public class GooglePacketCodecTests
{
    [Fact]
    public void DecryptAndDecompress_RoundTripsOriginalPayload()
    {
        byte[] originalPayload = [0x08, 0x4D, 0x13, 0x18, 0x05, 0x22, 0x02, 0x08, 0x01];
        var encryptionKey = Enumerable.Range(0, 64).Select(index => (byte)index).ToArray();
        var encodedPayload = EncodeCompressedPacket(originalPayload, encryptionKey);

        var decodedPayload = GooglePacketCodec.DecryptAndDecompress(encodedPayload, encryptionKey);

        Assert.Equal(originalPayload, decodedPayload);
    }

    private static byte[] EncodeCompressedPacket(byte[] payload, byte[] encryptionKey)
    {
        using var compressedStream = new MemoryStream();
        using (var zlibStream = new ZLibStream(compressedStream, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlibStream.Write(payload, 0, payload.Length);
        }

        var packet = new byte[8 + compressedStream.Length];
        MemoryMarshal.Write(packet.AsSpan(0, 4), 0x7468deadu);
        MemoryMarshal.Write(packet.AsSpan(4, 4), payload.Length);
        compressedStream.ToArray().CopyTo(packet, 8);

        return Transform(packet, encryptionKey);
    }

    private static byte[] Transform(byte[] payload, byte[] key)
    {
        var transformed = (byte[])payload.Clone();
        var offset = 16;
        for (var index = 0; index < transformed.Length; index++)
        {
            transformed[index] ^= key[offset++];

            if ((offset & 7) == 0)
            {
                offset += 16;
            }

            if (offset >= key.Length)
            {
                offset = (offset + 8) % 24;
            }
        }

        return transformed;
    }
}
