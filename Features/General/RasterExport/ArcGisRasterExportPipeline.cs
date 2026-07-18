#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core.Geoprocessing;

namespace XIAOFUTools.Features.General.RasterExport
{
    internal sealed class ArcGisRasterExportPipeline : IDisposable
    {
        private const string PixelType = "8_BIT_UNSIGNED";
        private const int OutputBandCount = 4;
        private const string MosaicMethod = "BLEND";
        private const string MosaicColorMapMode = "FIRST";
        private const string ResamplingType = "CUBIC";
        private readonly List<string> _tilePaths = new();
        private readonly string _workingDirectory;
        private readonly string _tileDirectory;
        private readonly string _tempMosaicPath;
        private bool _disposed;

        public ArcGisRasterExportPipeline(string outputFilePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(outputFilePath);

            OutputFilePath = outputFilePath;
            _workingDirectory = Path.Combine(Path.GetTempPath(), "XIAOFUTools", "RasterExport", Guid.NewGuid().ToString("N"));
            _tileDirectory = Path.Combine(_workingDirectory, "tiles");
            _tempMosaicPath = Path.Combine(_workingDirectory, "mosaic.tif");
            Directory.CreateDirectory(_tileDirectory);
        }

        public string OutputFilePath { get; }

        public void AddTile(
            string levelId,
            int row,
            int column,
            double minX,
            double minY,
            double maxX,
            double maxY,
            byte[] imageBytes,
            Polygon? preciseClip)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentException.ThrowIfNullOrWhiteSpace(levelId);
            ArgumentNullException.ThrowIfNull(imageBytes);

            string tilePath = ArcGisRasterTileCache.BuildTilePath(_tileDirectory, levelId, row, column);
            WritePngTile(tilePath, imageBytes, minX, minY, maxX, maxY, preciseClip);
            _tilePaths.Add(tilePath);
        }

        public async System.Threading.Tasks.Task ExportAsync(
            string sourceSpatialReferenceText,
            string? targetSpatialReferenceText,
            System.Threading.CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentException.ThrowIfNullOrWhiteSpace(sourceSpatialReferenceText);

            if (_tilePaths.Count == 0)
            {
                throw new InvalidOperationException("没有可导出的栅格瓦片。");
            }

            string outputDirectory = Path.GetDirectoryName(OutputFilePath)
                ?? throw new DirectoryNotFoundException("无法识别输出目录。");
            Directory.CreateDirectory(outputDirectory);

            var sourceSpatialReference = ParseSpatialReference(sourceSpatialReferenceText);
            var targetSpatialReference = string.IsNullOrWhiteSpace(targetSpatialReferenceText)
                ? null
                : ParseSpatialReference(targetSpatialReferenceText);

            bool needsProjection = targetSpatialReference != null
                && !string.Equals(sourceSpatialReferenceText, targetSpatialReferenceText, StringComparison.OrdinalIgnoreCase);

            string mosaicPath = needsProjection ? _tempMosaicPath : OutputFilePath;

            await ExecuteToolAsync(
                "management.MosaicToNewRaster",
                new object?[]
                {
                    string.Join(";", _tilePaths),
                    Path.GetDirectoryName(mosaicPath),
                    Path.GetFileName(mosaicPath),
                    sourceSpatialReference,
                    PixelType,
                    string.Empty,
                    OutputBandCount,
                    MosaicMethod,
                    MosaicColorMapMode,
                },
                cancellationToken).ConfigureAwait(false);

            await ExecuteToolAsync(
                "management.DefineProjection",
                new object?[]
                {
                    mosaicPath,
                    sourceSpatialReference,
                },
                cancellationToken).ConfigureAwait(false);

            if (needsProjection)
            {
                await ExecuteToolAsync(
                    "management.ProjectRaster",
                    new object?[]
                    {
                        mosaicPath,
                        OutputFilePath,
                        targetSpatialReference,
                        ResamplingType,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        sourceSpatialReference,
                    },
                    cancellationToken).ConfigureAwait(false);
            }

            await TryBuildPyramidsAsync(cancellationToken).ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                if (Directory.Exists(_workingDirectory))
                {
                    Directory.Delete(_workingDirectory, true);
                }
            }
            catch
            {
            }
        }

        private static void WritePngTile(
            string tilePath,
            byte[] imageBytes,
            double minX,
            double minY,
            double maxX,
            double maxY,
            Polygon? preciseClip)
        {
            using var stream = new MemoryStream(imageBytes, writable: false);
            BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            BitmapFrame frame = decoder.Frames.FirstOrDefault()
                ?? throw new InvalidOperationException("无法读取瓦片影像数据。");

            BitmapSource source = frame.Format == PixelFormats.Bgra32
                ? frame
                : new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);

            int width = source.PixelWidth;
            int height = source.PixelHeight;
            int stride = width * 4;
            byte[] pixels = new byte[stride * height];
            source.CopyPixels(pixels, stride, 0);

            if (preciseClip != null)
            {
                ApplyClipMask(pixels, stride, width, height, minX, minY, maxX, maxY, preciseClip);
            }

            BitmapSource outputBitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
            outputBitmap.Freeze();

            Directory.CreateDirectory(Path.GetDirectoryName(tilePath) ?? throw new DirectoryNotFoundException("无法识别瓦片目录。"));
            using (var fileStream = new FileStream(tilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(outputBitmap));
                encoder.Save(fileStream);
            }

            File.WriteAllText(
                ArcGisRasterTileCache.GetWorldFilePath(tilePath),
                ArcGisRasterTileCache.BuildWorldFileContent(minX, minY, maxX, maxY, width, height));
        }

        private static void ApplyClipMask(
            byte[] pixels,
            int stride,
            int width,
            int height,
            double minX,
            double minY,
            double maxX,
            double maxY,
            Polygon preciseClip)
        {
            double pixelWidth = (maxX - minX) / width;
            double pixelHeight = (maxY - minY) / height;
            var clipExtent = preciseClip.Extent;
            var pixelWindow = clipExtent == null
                ? new ArcGisRasterTileCache.PixelWindow(0, width, 0, height)
                : ArcGisRasterTileCache.GetPixelWindow(
                    minX,
                    minY,
                    maxX,
                    maxY,
                    width,
                    height,
                    clipExtent.XMin,
                    clipExtent.YMin,
                    clipExtent.XMax,
                    clipExtent.YMax);

            if (pixelWindow.IsEmpty)
            {
                ClearAlphaRange(pixels, stride, width, 0, height, 0, width);
                return;
            }

            if (pixelWindow.StartY > 0)
            {
                ClearAlphaRange(pixels, stride, width, 0, pixelWindow.StartY, 0, width);
            }

            if (pixelWindow.EndYExclusive < height)
            {
                ClearAlphaRange(pixels, stride, width, pixelWindow.EndYExclusive, height, 0, width);
            }

            for (int y = pixelWindow.StartY; y < pixelWindow.EndYExclusive; y++)
            {
                if (pixelWindow.StartX > 0)
                {
                    ClearAlphaRange(pixels, stride, width, y, y + 1, 0, pixelWindow.StartX);
                }

                if (pixelWindow.EndXExclusive < width)
                {
                    ClearAlphaRange(pixels, stride, width, y, y + 1, pixelWindow.EndXExclusive, width);
                }

                for (int x = pixelWindow.StartX; x < pixelWindow.EndXExclusive; x++)
                {
                    double centerX = minX + (x + 0.5d) * pixelWidth;
                    double centerY = maxY - (y + 0.5d) * pixelHeight;
                    MapPoint point = MapPointBuilderEx.CreateMapPoint(centerX, centerY, preciseClip.SpatialReference);
                    if (GeometryEngine.Instance.Intersects(preciseClip, point))
                    {
                        continue;
                    }

                    int alphaIndex = y * stride + x * 4 + 3;
                    pixels[alphaIndex] = 0;
                }
            }
        }

        private static void ClearAlphaRange(byte[] pixels, int stride, int width, int startY, int endYExclusive, int startX, int endXExclusive)
        {
            if (startY >= endYExclusive || startX >= endXExclusive)
            {
                return;
            }

            startY = Math.Max(0, startY);
            endYExclusive = Math.Min(pixels.Length / stride, Math.Max(startY, endYExclusive));
            startX = Math.Max(0, startX);
            endXExclusive = Math.Min(width, endXExclusive);

            for (int y = startY; y < endYExclusive; y++)
            {
                for (int x = startX; x < endXExclusive; x++)
                {
                    int alphaIndex = y * stride + x * 4 + 3;
                    pixels[alphaIndex] = 0;
                }
            }
        }

        private async System.Threading.Tasks.Task ExecuteToolAsync(
            string toolName,
            object?[] parameters,
            System.Threading.CancellationToken cancellationToken)
        {
            var environments = Geoprocessing.MakeEnvironmentArray(
                "overwriteoutput", "True",
                "addOutputsToMap", "False");
            IGPResult result = await Geoprocessing.ExecuteToolAsync(
                toolName,
                Geoprocessing.MakeValueArray(parameters),
                environments,
                null,
                null,
                GPExecuteToolFlags.GPThread).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            if (result.IsFailed)
            {
                throw new InvalidOperationException(BuildGpMessage(toolName, result));
            }
        }

        private static SpatialReference ParseSpatialReference(string spatialReferenceText)
        {
            if (string.IsNullOrWhiteSpace(spatialReferenceText))
            {
                throw new InvalidOperationException("缺少坐标系定义。");
            }

            if (spatialReferenceText.StartsWith("EPSG:", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(spatialReferenceText[5..], out var wkid))
            {
                return SpatialReferenceBuilder.CreateSpatialReference(wkid);
            }

            return SpatialReferenceBuilder.CreateSpatialReference(spatialReferenceText);
        }

        private async System.Threading.Tasks.Task TryBuildPyramidsAsync(System.Threading.CancellationToken cancellationToken)
        {
            try
            {
                await ExecuteToolAsync(
                    "management.BuildPyramids",
                    new object?[] { OutputFilePath },
                    cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Pyramid generation is a best-effort optimization and should not fail the export.
            }
        }

        private static string BuildGpMessage(string toolName, IGPResult result)
        {
            if (result.Messages == null || !result.Messages.Any())
            {
                return $"GP 工具执行失败: {toolName}";
            }

            return $"GP 工具执行失败: {toolName}; {string.Join("; ", result.Messages.Select(message => message.Text).Where(text => !string.IsNullOrWhiteSpace(text)))}";
        }
    }
}
