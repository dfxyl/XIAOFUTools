using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PDFtoImage;
using SkiaSharp;

namespace XIAOFUTools.Features.Conversion.PdfToImages.Infrastructure
{
    internal sealed record PdfImageConversionRequest(
        string InputFolder,
        string OutputFolder,
        bool SaveToSourcePath,
        bool KeepOriginalStructure,
        bool CreateSeparateFolder,
        bool TraverseSubfolders,
        int Resolution,
        string OutputFormat);

    internal sealed record PdfImageConversionProgress(
        int ProcessedFiles,
        int TotalFiles,
        string Message,
        bool IsError = false);

    internal sealed class PdfImageConversionService
    {
        internal bool DirectoryExists(string folderPath) =>
            !string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath);

        internal void Convert(
            PdfImageConversionRequest request,
            CancellationToken cancellationToken,
            Action<PdfImageConversionProgress> reportProgress)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.Resolution);
            reportProgress ??= _ => { };
            var searchOption = request.TraverseSubfolders
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;
            var pdfFiles = Directory.EnumerateFiles(request.InputFolder, "*.pdf", searchOption)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (pdfFiles.Length == 0)
            {
                reportProgress(new PdfImageConversionProgress(0, 0, "未找到PDF文件"));
                return;
            }

            reportProgress(new PdfImageConversionProgress(
                0,
                pdfFiles.Length,
                $"找到 {pdfFiles.Length} 个PDF文件，开始并行处理..."));
            var processedFiles = 0;
            Parallel.ForEach(
                pdfFiles,
                new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                },
                pdfFile =>
                {
                    try
                    {
                        reportProgress(new PdfImageConversionProgress(
                            Volatile.Read(ref processedFiles),
                            pdfFiles.Length,
                            $"正在处理: {Path.GetFileName(pdfFile)}"));
                        ConvertSingleFile(pdfFile, request, cancellationToken, reportProgress);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        reportProgress(new PdfImageConversionProgress(
                            Volatile.Read(ref processedFiles),
                            pdfFiles.Length,
                            $"处理文件 {Path.GetFileName(pdfFile)} 时出错: {exception.Message}",
                            IsError: true));
                    }
                    finally
                    {
                        var current = Interlocked.Increment(ref processedFiles);
                        reportProgress(new PdfImageConversionProgress(
                            current,
                            pdfFiles.Length,
                            $"已处理 {current}/{pdfFiles.Length} 个文件"));
                    }
                });
        }

        private static void ConvertSingleFile(
            string pdfFile,
            PdfImageConversionRequest request,
            CancellationToken cancellationToken,
            Action<PdfImageConversionProgress> reportProgress)
        {
            var outputDirectory = ResolveOutputDirectory(pdfFile, request);
            Directory.CreateDirectory(outputDirectory);
            var options = new RenderOptions { Dpi = request.Resolution };
            var imageFormat = ResolveImageFormat(request.OutputFormat);
            var quality = imageFormat == SKEncodedImageFormat.Jpeg ? 90 : 100;
            var pageNumber = 1;
            using var pdfStream = File.OpenRead(pdfFile);
            foreach (var image in PDFtoImage.Conversion.ToImages(pdfStream, options: options))
            {
                using (image)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var extension = request.OutputFormat.ToLowerInvariant();
                    var outputFileName = $"{Path.GetFileNameWithoutExtension(pdfFile)}_page{pageNumber:D3}.{extension}";
                    var outputPath = Path.Combine(outputDirectory, outputFileName);
                    using var outputStream = File.Create(outputPath);
                    image.Encode(outputStream, imageFormat, quality);
                    reportProgress(new PdfImageConversionProgress(
                        0,
                        0,
                        $"  - 已保存第 {pageNumber} 页: {outputFileName}"));
                    pageNumber++;
                }
            }

            reportProgress(new PdfImageConversionProgress(
                0,
                0,
                $"完成处理: {Path.GetFileName(pdfFile)} (共 {pageNumber - 1} 页)"));
        }

        private static string ResolveOutputDirectory(string pdfFile, PdfImageConversionRequest request)
        {
            var sourceDirectory = Path.GetDirectoryName(pdfFile)
                ?? throw new InvalidOperationException("PDF 文件路径不包含目录。");
            var outputDirectory = request.SaveToSourcePath
                ? sourceDirectory
                : request.KeepOriginalStructure
                    ? Path.Combine(request.OutputFolder, Path.GetRelativePath(request.InputFolder, sourceDirectory))
                    : request.OutputFolder;
            if (request.CreateSeparateFolder)
            {
                outputDirectory = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(pdfFile));
            }

            return outputDirectory;
        }

        private static SKEncodedImageFormat ResolveImageFormat(string outputFormat)
        {
            return outputFormat.ToUpperInvariant() switch
            {
                "JPG" or "JPEG" => SKEncodedImageFormat.Jpeg,
                "PNG" => SKEncodedImageFormat.Png,
                "WEBP" => SKEncodedImageFormat.Webp,
                _ => SKEncodedImageFormat.Jpeg
            };
        }
    }
}
