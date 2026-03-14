#nullable enable

using System;
using System.IO;
using Google.Protobuf;
namespace XIAOFUTools.Tools.HistoricalImageryDownload.Infrastructure.Google
{
    internal sealed record GoogleDbRootEnvelope
    {
        public int EncryptionType { get; init; }

        public byte[] EncryptionData { get; init; } = Array.Empty<byte>();

        public byte[] EncryptedDbRootData { get; init; } = Array.Empty<byte>();
    }

    internal sealed class GoogleDbRootInfo
    {
        public uint QuadtreeVersion { get; init; }
    }

    internal static class GoogleDbRootParser
    {
        public static GoogleDbRootEnvelope ParseEnvelope(byte[] payload)
        {
            using var input = new CodedInputStream(payload);
            var envelope = new GoogleDbRootEnvelope();

            while (!input.IsAtEnd)
            {
                var tag = input.ReadTag();
                if (tag == 0)
                {
                    break;
                }

                var fieldNumber = WireFormat.GetTagFieldNumber(tag);
                var wireType = WireFormat.GetTagWireType(tag);

                if (fieldNumber == 1 && wireType == WireFormat.WireType.Varint)
                {
                    envelope = envelope with { EncryptionType = input.ReadEnum() };
                    continue;
                }

                if (fieldNumber == 2 && wireType == WireFormat.WireType.LengthDelimited)
                {
                    envelope = envelope with { EncryptionData = input.ReadBytes().ToByteArray() };
                    continue;
                }

                if (fieldNumber == 3 && wireType == WireFormat.WireType.LengthDelimited)
                {
                    envelope = envelope with { EncryptedDbRootData = input.ReadBytes().ToByteArray() };
                    continue;
                }

                input.SkipLastField();
            }

            return envelope;
        }

        public static GoogleDbRootInfo ParseDbRoot(byte[] payload)
        {
            using var input = new CodedInputStream(payload);
            uint quadtreeVersion = 0;

            while (!input.IsAtEnd)
            {
                var tag = input.ReadTag();
                if (tag == 0)
                {
                    break;
                }

                if (WireFormat.GetTagFieldNumber(tag) == 13 &&
                    WireFormat.GetTagWireType(tag) == WireFormat.WireType.LengthDelimited)
                {
                    quadtreeVersion = ParseDatabaseVersion(new CodedInputStream(input.ReadBytes().ToByteArray()));
                    continue;
                }

                input.SkipLastField();
            }

            return new GoogleDbRootInfo
            {
                QuadtreeVersion = quadtreeVersion
            };
        }

        public static GoogleDbRootInfo ParseEncryptedEnvelope(byte[] payload)
        {
            var envelope = ParseEnvelope(payload);
            var dbRootBytes = GooglePacketCodec.DecryptAndDecompress(envelope.EncryptedDbRootData, envelope.EncryptionData);
            return ParseDbRoot(dbRootBytes);
        }

        private static uint ParseDatabaseVersion(CodedInputStream input)
        {
            uint quadtreeVersion = 0;

            while (!input.IsAtEnd)
            {
                var tag = input.ReadTag();
                if (tag == 0)
                {
                    break;
                }

                if (WireFormat.GetTagFieldNumber(tag) == 1 &&
                    WireFormat.GetTagWireType(tag) == WireFormat.WireType.Varint)
                {
                    quadtreeVersion = input.ReadUInt32();
                    continue;
                }

                input.SkipLastField();
            }

            return quadtreeVersion;
        }
    }
}
