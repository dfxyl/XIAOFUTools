using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace XIAOFUTools.Features.Conversion.ImagesToPdf.Infrastructure
{
    internal sealed record ImagePdfOutputPathRequest(
        string SourceFolderPath,
        string InputFolderPath,
        string OutputFolderPath,
        bool SaveToSourcePath,
        bool KeepOriginalStructure,
        string PdfName);

    internal sealed class ImagePdfFileStore
    {
        internal bool DirectoryExists(string folderPath) =>
            !string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath);

        internal IReadOnlyList<string> GetImageFiles(
            string folderPath,
            bool includeSubfolders,
            IEnumerable<string> supportedExtensions)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
            ArgumentNullException.ThrowIfNull(supportedExtensions);
            var searchOption = includeSubfolders
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;
            var imageFiles = new List<string>();
            foreach (var extension in supportedExtensions.Where(extension => !string.IsNullOrWhiteSpace(extension)))
            {
                try
                {
                    imageFiles.AddRange(Directory.EnumerateFiles(folderPath, $"*{extension}", searchOption));
                }
                catch
                {
                    // 保持旧行为：忽略访问受限的单个扩展名扫描。
                }
            }

            return imageFiles
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        internal IReadOnlyList<string> GetFolders(string inputFolderPath, bool traverseSubfolders)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(inputFolderPath);
            var folders = new List<string> { inputFolderPath };
            if (traverseSubfolders)
            {
                folders.AddRange(Directory.EnumerateDirectories(inputFolderPath, "*", SearchOption.AllDirectories)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
            }

            return folders;
        }

        internal string CreateUniquePdfOutputPath(ImagePdfOutputPathRequest request)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceFolderPath);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.PdfName);
            var outputDirectory = ResolveOutputDirectory(request);
            Directory.CreateDirectory(outputDirectory);

            var outputPath = Path.Combine(outputDirectory, $"{request.PdfName}.pdf");
            var counter = 1;
            while (File.Exists(outputPath))
            {
                outputPath = Path.Combine(outputDirectory, $"{request.PdfName}_{counter}.pdf");
                counter++;
            }

            return outputPath;
        }

        internal void EnsureParentDirectory(string outputFilePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(outputFilePath);
            var directory = Path.GetDirectoryName(outputFilePath)
                ?? throw new InvalidOperationException("输出文件路径不包含目录。");
            Directory.CreateDirectory(directory);
        }

        private static string ResolveOutputDirectory(ImagePdfOutputPathRequest request)
        {
            if (request.SaveToSourcePath)
            {
                return request.SourceFolderPath;
            }

            ArgumentException.ThrowIfNullOrWhiteSpace(request.OutputFolderPath);
            if (!request.KeepOriginalStructure)
            {
                return request.OutputFolderPath;
            }

            ArgumentException.ThrowIfNullOrWhiteSpace(request.InputFolderPath);
            var relativePath = Path.GetRelativePath(request.InputFolderPath, request.SourceFolderPath);
            return relativePath == "."
                ? request.OutputFolderPath
                : Path.Combine(request.OutputFolderPath, relativePath);
        }
    }
}
