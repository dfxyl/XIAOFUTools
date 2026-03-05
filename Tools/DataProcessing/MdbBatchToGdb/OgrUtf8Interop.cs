using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using OSGeo.OGR;
using OSGeo.OSR;

namespace XIAOFUTools.Tools.DataProcessing.MdbBatchToGdb
{
    internal static class OgrUtf8Interop
    {
        private static readonly Encoding Utf8Encoding;
        private static readonly Encoding DefaultTextEncoding;
        private static readonly Encoding GbkTextEncoding;
        private static readonly Encoding Gb18030TextEncoding;

        static OgrUtf8Interop()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            Utf8Encoding = new UTF8Encoding(false, false);
            DefaultTextEncoding = Encoding.Default;
            GbkTextEncoding = GetEncodingOrDefault(936, DefaultTextEncoding);
            Gb18030TextEncoding = GetEncodingOrDefault("GB18030", GbkTextEncoding);
        }

        [DllImport("ogr_wrap", EntryPoint = "CSharp_OSGeofOGR_Layer_GetName___", CallingConvention = CallingConvention.Winapi)]
        private static extern IntPtr Layer_GetName_Utf8(HandleRef layerHandle);

        [DllImport("ogr_wrap", EntryPoint = "CSharp_OSGeofOGR_Feature_GetFieldAsString__SWIG_0___", CallingConvention = CallingConvention.Winapi)]
        private static extern IntPtr Feature_GetFieldAsString_Utf8(HandleRef featureHandle, int fieldIndex);

        [DllImport("ogr_wrap", EntryPoint = "CSharp_OSGeofOGR_DataSource_CreateLayer___", CallingConvention = CallingConvention.Winapi)]
        private static extern IntPtr DataSource_CreateLayer_Utf8(
            HandleRef dataSourceHandle,
            byte[] layerNameUtf8,
            HandleRef spatialReferenceHandle,
            int geometryType,
            IntPtr[] layerOptions);

        public static string GetLayerName(Layer layer)
        {
            if (layer == null)
            {
                return string.Empty;
            }

            try
            {
                return PtrToBestString(Layer_GetName_Utf8(Layer.getCPtr(layer)));
            }
            catch
            {
                return layer.GetName() ?? string.Empty;
            }
        }

        public static string GetFieldAsString(Feature feature, int fieldIndex)
        {
            if (feature == null || fieldIndex < 0 || fieldIndex >= feature.GetFieldCount())
            {
                return string.Empty;
            }

            try
            {
                return PtrToBestString(Feature_GetFieldAsString_Utf8(Feature.getCPtr(feature), fieldIndex));
            }
            catch
            {
                return feature.GetFieldAsString(fieldIndex) ?? string.Empty;
            }
        }

        public static Layer CreateLayer(
            DataSource targetDataSource,
            string layerName,
            SpatialReference targetSpatialReference,
            wkbGeometryType geometryType,
            string[] layerOptions)
        {
            if (targetDataSource == null)
            {
                throw new ArgumentNullException(nameof(targetDataSource));
            }

            byte[] layerNameUtf8 = ToUtf8Bytes(layerName);
            List<IntPtr> allocations = null;
            IntPtr[] nativeOptions = BuildNativeStringList(layerOptions, out allocations);

            try
            {
                HandleRef dataSourceHandle = DataSource.getCPtr(targetDataSource);
                HandleRef spatialReferenceHandle = targetSpatialReference == null
                    ? new HandleRef(null, IntPtr.Zero)
                    : SpatialReference.getCPtr(targetSpatialReference);

                IntPtr layerPtr = DataSource_CreateLayer_Utf8(
                    dataSourceHandle,
                    layerNameUtf8,
                    spatialReferenceHandle,
                    (int)geometryType,
                    nativeOptions);

                if (layerPtr == IntPtr.Zero)
                {
                    return null;
                }

                return new Layer(layerPtr, false, targetDataSource);
            }
            finally
            {
                ReleaseNativeStringList(allocations);
            }
        }

        private static string PtrToBestString(IntPtr strPtr)
        {
            if (strPtr == IntPtr.Zero)
            {
                return string.Empty;
            }

            byte[] bytes = ReadNullTerminatedBytes(strPtr);
            if (bytes.Length == 0)
            {
                return string.Empty;
            }

            string best = string.Empty;
            int bestScore = int.MinValue;
            if (IsAscii(bytes))
            {
                return Encoding.ASCII.GetString(bytes);
            }

            if (LooksLikeUtf8(bytes))
            {
                ConsiderCandidate(Utf8Encoding.GetString(bytes), ref best, ref bestScore);
            }

            ConsiderCandidate(DefaultTextEncoding.GetString(bytes), ref best, ref bestScore);
            ConsiderCandidate(GbkTextEncoding.GetString(bytes), ref best, ref bestScore);

            if (!ReferenceEquals(Gb18030TextEncoding, GbkTextEncoding))
            {
                ConsiderCandidate(Gb18030TextEncoding.GetString(bytes), ref best, ref bestScore);
            }

            if (string.IsNullOrEmpty(best))
            {
                best = DefaultTextEncoding.GetString(bytes);
            }

            return best ?? string.Empty;
        }

        private static void ConsiderCandidate(string value, ref string best, ref int bestScore)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            int score = ScoreDecodedString(value);
            if (score > bestScore)
            {
                best = value;
                bestScore = score;
            }
        }

        private static bool IsAscii(byte[] bytes)
        {
            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] > 0x7F)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool LooksLikeUtf8(byte[] bytes)
        {
            int i = 0;
            while (i < bytes.Length)
            {
                byte value = bytes[i];
                if ((value & 0x80) == 0)
                {
                    i++;
                    continue;
                }

                int expectedContinuationCount;
                if ((value & 0xE0) == 0xC0)
                {
                    if (value < 0xC2)
                    {
                        return false;
                    }

                    expectedContinuationCount = 1;
                }
                else if ((value & 0xF0) == 0xE0)
                {
                    expectedContinuationCount = 2;
                }
                else if ((value & 0xF8) == 0xF0)
                {
                    if (value > 0xF4)
                    {
                        return false;
                    }

                    expectedContinuationCount = 3;
                }
                else
                {
                    return false;
                }

                if (i + expectedContinuationCount >= bytes.Length)
                {
                    return false;
                }

                for (int j = 1; j <= expectedContinuationCount; j++)
                {
                    if ((bytes[i + j] & 0xC0) != 0x80)
                    {
                        return false;
                    }
                }

                i += expectedContinuationCount + 1;
            }

            return true;
        }

        private static Encoding GetEncodingOrDefault(int codePage, Encoding fallback)
        {
            try
            {
                return Encoding.GetEncoding(codePage);
            }
            catch
            {
                return fallback;
            }
        }

        private static Encoding GetEncodingOrDefault(string encodingName, Encoding fallback)
        {
            try
            {
                return Encoding.GetEncoding(encodingName);
            }
            catch
            {
                return fallback;
            }
        }

        private static byte[] ReadNullTerminatedBytes(IntPtr ptr)
        {
            int length = 0;
            while (Marshal.ReadByte(ptr, length) != 0)
            {
                length++;
            }

            var bytes = new byte[length];
            Marshal.Copy(ptr, bytes, 0, length);
            return bytes;
        }

        private static int ScoreDecodedString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return int.MinValue / 2;
            }

            int score = 0;
            foreach (char ch in value)
            {
                if (ch == '\uFFFD')
                {
                    score -= 100;
                    continue;
                }

                if (ch == '?')
                {
                    score -= 4;
                    continue;
                }

                if (char.IsControl(ch))
                {
                    score -= 20;
                    continue;
                }

                if (ch >= '\uE000' && ch <= '\uF8FF')
                {
                    score -= 20;
                    continue;
                }

                if ((ch >= '\u4E00' && ch <= '\u9FFF') || (ch >= '\u3400' && ch <= '\u4DBF'))
                {
                    score += 3;
                    continue;
                }

                if (char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' || ch == ' ' || ch == '\\' || ch == '/')
                {
                    score += 1;
                    continue;
                }

                score += 0;
            }

            return score;
        }

        private static byte[] ToUtf8Bytes(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            var buffer = new byte[bytes.Length + 1];
            Buffer.BlockCopy(bytes, 0, buffer, 0, bytes.Length);
            buffer[buffer.Length - 1] = 0;
            return buffer;
        }

        private static IntPtr[] BuildNativeStringList(string[] values, out List<IntPtr> allocations)
        {
            allocations = null;
            if (values == null || values.Length == 0)
            {
                return null;
            }

            allocations = new List<IntPtr>(values.Length);
            var nativeList = new IntPtr[values.Length + 1];

            for (int i = 0; i < values.Length; i++)
            {
                byte[] utf8 = ToUtf8Bytes(values[i]);
                IntPtr ptr = Marshal.AllocHGlobal(utf8.Length);
                Marshal.Copy(utf8, 0, ptr, utf8.Length);
                nativeList[i] = ptr;
                allocations.Add(ptr);
            }

            nativeList[nativeList.Length - 1] = IntPtr.Zero;
            return nativeList;
        }

        private static void ReleaseNativeStringList(IEnumerable<IntPtr> allocations)
        {
            if (allocations == null)
            {
                return;
            }

            foreach (IntPtr ptr in allocations)
            {
                if (ptr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }
        }
    }
}
