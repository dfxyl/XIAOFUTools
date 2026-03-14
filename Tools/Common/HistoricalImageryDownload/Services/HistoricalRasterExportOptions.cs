#nullable enable

using System;

namespace XIAOFUTools.Tools.HistoricalImageryDownload.Services
{
    internal static class HistoricalRasterExportOptions
    {
        public static string[] CreateWarpParameters(string sourceSpatialReferenceText, string targetSpatialReferenceText)
        {
            return
            [
                "-multi",
                "-wo", $"NUM_THREADS={Math.Max(1, Environment.ProcessorCount / 2)}",
                "-of", "GTiff",
                "-ot", "Byte",
                "-co", "COMPRESS=DEFLATE",
                "-co", "PREDICTOR=2",
                "-co", "ZLEVEL=6",
                "-co", "TILED=TRUE",
                "-co", "BIGTIFF=IF_SAFER",
                "-r", "cubic",
                "-s_srs", sourceSpatialReferenceText,
                "-t_srs", targetSpatialReferenceText
            ];
        }

        public static string[] CreateCopyOptions()
        {
            return
            [
                "COMPRESS=DEFLATE",
                "PREDICTOR=2",
                "ZLEVEL=6",
                "TILED=TRUE",
                "BIGTIFF=IF_SAFER",
                $"NUM_THREADS={Math.Max(1, Environment.ProcessorCount / 2)}"
            ];
        }
    }
}
