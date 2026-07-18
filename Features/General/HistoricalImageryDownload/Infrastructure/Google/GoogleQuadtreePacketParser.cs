#nullable enable

using System;
using System.Collections.Generic;
using Google.Protobuf;

namespace XIAOFUTools.Features.General.HistoricalImageryDownload.Infrastructure.Google
{
    internal enum GoogleLayerType
    {
        Imagery = 0,
        Terrain = 1,
        Vector = 2,
        ImageryHistory = 3
    }

    internal sealed record GoogleDatedTileInfo(DateOnly Date, int TileEpoch, int Provider);

    internal sealed class GoogleLayerInfo
    {
        public GoogleLayerType Type { get; init; }

        public int LayerEpoch { get; init; }

        public int Provider { get; init; }

        public IReadOnlyList<GoogleDatedTileInfo> DatedTiles { get; init; } = Array.Empty<GoogleDatedTileInfo>();
    }

    internal sealed class GoogleNodeInfo
    {
        public int Index { get; init; }

        public int CacheNodeEpoch { get; init; }

        public IReadOnlyList<GoogleLayerInfo> Layers { get; init; } = Array.Empty<GoogleLayerInfo>();
    }

    internal sealed class GoogleQuadtreePacketInfo
    {
        public int PacketEpoch { get; init; }

        public IReadOnlyList<GoogleNodeInfo> Nodes { get; init; } = Array.Empty<GoogleNodeInfo>();
    }

    internal static class GoogleQuadtreePacketParser
    {
        public static GoogleQuadtreePacketInfo Parse(byte[] payload)
        {
            using var input = new CodedInputStream(payload);
            var packetEpoch = 0;
            var nodes = new List<GoogleNodeInfo>();

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
                    packetEpoch = input.ReadInt32();
                    continue;
                }

                if (fieldNumber == 2 && wireType == WireFormat.WireType.StartGroup)
                {
                    nodes.Add(ParseSparseNode(input));
                    continue;
                }

                input.SkipLastField();
            }

            return new GoogleQuadtreePacketInfo
            {
                PacketEpoch = packetEpoch,
                Nodes = nodes
            };
        }

        private static GoogleNodeInfo ParseSparseNode(CodedInputStream input)
        {
            var index = 0;
            var cacheNodeEpoch = 0;
            var layers = new List<GoogleLayerInfo>();

            while (!input.IsAtEnd)
            {
                var tag = input.ReadTag();
                if (tag == 0)
                {
                    break;
                }

                var fieldNumber = WireFormat.GetTagFieldNumber(tag);
                var wireType = WireFormat.GetTagWireType(tag);

                if (fieldNumber == 2 && wireType == WireFormat.WireType.EndGroup)
                {
                    break;
                }

                if (fieldNumber == 3 && wireType == WireFormat.WireType.Varint)
                {
                    index = input.ReadInt32();
                    continue;
                }

                if (fieldNumber == 4 && wireType == WireFormat.WireType.LengthDelimited)
                {
                    ParseNode(new CodedInputStream(input.ReadBytes().ToByteArray()), out cacheNodeEpoch, layers);
                    continue;
                }

                input.SkipLastField();
            }

            return new GoogleNodeInfo
            {
                Index = index,
                CacheNodeEpoch = cacheNodeEpoch,
                Layers = layers
            };
        }

        private static void ParseNode(CodedInputStream input, out int cacheNodeEpoch, List<GoogleLayerInfo> layers)
        {
            cacheNodeEpoch = 0;

            while (!input.IsAtEnd)
            {
                var tag = input.ReadTag();
                if (tag == 0)
                {
                    break;
                }

                var fieldNumber = WireFormat.GetTagFieldNumber(tag);
                var wireType = WireFormat.GetTagWireType(tag);

                if (fieldNumber == 2 && wireType == WireFormat.WireType.Varint)
                {
                    cacheNodeEpoch = input.ReadInt32();
                    continue;
                }

                if (fieldNumber == 3 && wireType == WireFormat.WireType.LengthDelimited)
                {
                    layers.Add(ParseLayer(new CodedInputStream(input.ReadBytes().ToByteArray())));
                    continue;
                }

                input.SkipLastField();
            }
        }

        private static GoogleLayerInfo ParseLayer(CodedInputStream input)
        {
            var type = GoogleLayerType.Imagery;
            var layerEpoch = 0;
            var provider = 0;
            var datedTiles = new List<GoogleDatedTileInfo>();

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
                    type = (GoogleLayerType)input.ReadEnum();
                    continue;
                }

                if (fieldNumber == 2 && wireType == WireFormat.WireType.Varint)
                {
                    layerEpoch = input.ReadInt32();
                    continue;
                }

                if (fieldNumber == 3 && wireType == WireFormat.WireType.Varint)
                {
                    provider = input.ReadInt32();
                    continue;
                }

                if (fieldNumber == 4 && wireType == WireFormat.WireType.LengthDelimited)
                {
                    datedTiles.AddRange(ParseDatesLayer(new CodedInputStream(input.ReadBytes().ToByteArray())));
                    continue;
                }

                input.SkipLastField();
            }

            return new GoogleLayerInfo
            {
                Type = type,
                LayerEpoch = layerEpoch,
                Provider = provider,
                DatedTiles = datedTiles
            };
        }

        private static IReadOnlyList<GoogleDatedTileInfo> ParseDatesLayer(CodedInputStream input)
        {
            var datedTiles = new List<GoogleDatedTileInfo>();

            while (!input.IsAtEnd)
            {
                var tag = input.ReadTag();
                if (tag == 0)
                {
                    break;
                }

                if (WireFormat.GetTagFieldNumber(tag) == 1 &&
                    WireFormat.GetTagWireType(tag) == WireFormat.WireType.LengthDelimited)
                {
                    datedTiles.Add(ParseDatedTile(new CodedInputStream(input.ReadBytes().ToByteArray())));
                    continue;
                }

                input.SkipLastField();
            }

            return datedTiles;
        }

        private static GoogleDatedTileInfo ParseDatedTile(CodedInputStream input)
        {
            var encodedDate = 0;
            var tileEpoch = 0;
            var provider = 0;

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
                    encodedDate = input.ReadInt32();
                    continue;
                }

                if (fieldNumber == 2 && wireType == WireFormat.WireType.Varint)
                {
                    tileEpoch = input.ReadInt32();
                    continue;
                }

                if (fieldNumber == 3 && wireType == WireFormat.WireType.Varint)
                {
                    provider = input.ReadInt32();
                    continue;
                }

                input.SkipLastField();
            }

            return new GoogleDatedTileInfo(GoogleQuadtreeCodec.DecodeDate(encodedDate), tileEpoch, provider);
        }
    }
}
