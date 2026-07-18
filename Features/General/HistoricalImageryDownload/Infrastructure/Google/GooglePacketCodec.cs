#nullable enable

using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace XIAOFUTools.Features.General.HistoricalImageryDownload.Infrastructure.Google
{
    internal static class GooglePacketCodec
    {
        private const int PacketHeaderSize = 8;
        private const uint PacketMagic = 0x7468deadu;
        private const uint PacketMagicSwap = 0xadde6874u;

        public static byte[] Decrypt(byte[] encryptedPayload, byte[] encryptionKey)
        {
            var decryptedPayload = (byte[])encryptedPayload.Clone();
            TransformInPlace(encryptionKey, decryptedPayload);
            return decryptedPayload;
        }

        public static byte[] DecryptAndDecompress(byte[] encryptedPayload, byte[] encryptionKey)
            => Decompress(Decrypt(encryptedPayload, encryptionKey));

        public static byte[] Decompress(byte[] compressedPayload)
        {
            if (!TryGetDecompressedSize(compressedPayload, out var decompressedSize))
            {
                throw new InvalidDataException("Failed to determine packet size.");
            }

            var decompressed = GC.AllocateUninitializedArray<byte>(decompressedSize);
            using var compressedStream = new MemoryStream(compressedPayload[PacketHeaderSize..], writable: false);
            using var outputStream = new MemoryStream(decompressed);
            using var decompressor = new ZLibStream(compressedStream, CompressionMode.Decompress);
            decompressor.CopyTo(outputStream);
            return decompressed;
        }

        private static void TransformInPlace(ReadOnlySpan<byte> key, Span<byte> cipherText)
        {
            if (key.IsEmpty || cipherText.IsEmpty)
            {
                return;
            }

            var offset = 16;
            for (var index = 0; index < cipherText.Length; index++)
            {
                cipherText[index] ^= key[offset++];

                if ((offset & 7) == 0)
                {
                    offset += 16;
                }

                if (offset >= key.Length)
                {
                    offset = (offset + 8) % 24;
                }
            }
        }

        private static bool TryGetDecompressedSize(ReadOnlySpan<byte> buffer, out int decompressedSize)
        {
            if (buffer.Length < PacketHeaderSize)
            {
                decompressedSize = 0;
                return false;
            }

            var intBuffer = MemoryMarshal.Cast<byte, uint>(buffer);
            if (intBuffer[0] == PacketMagic)
            {
                decompressedSize = (int)intBuffer[1];
                return true;
            }

            if (intBuffer[0] == PacketMagicSwap)
            {
                decompressedSize = (int)System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(intBuffer[1]);
                return true;
            }

            decompressedSize = 0;
            return false;
        }
    }
}
