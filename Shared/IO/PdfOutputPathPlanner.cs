using System;
using System.IO;

namespace XIAOFUTools.Shared.IO
{
    internal sealed record PdfOutputPathOptions(
        string InputFolder,
        string OutputFolder,
        bool SaveToSourcePath,
        bool KeepOriginalStructure);

    internal static class PdfOutputPathPlanner
    {
        public static string GetOutputPath(
            string sourceFilePath,
            string pdfName,
            PdfOutputPathOptions options)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(pdfName);
            ArgumentNullException.ThrowIfNull(options);

            var sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(sourceFilePath));
            if (string.IsNullOrWhiteSpace(sourceDirectory))
            {
                throw new ArgumentException("无法解析源文件目录。", nameof(sourceFilePath));
            }

            var outputDirectory = ResolveOutputDirectory(sourceDirectory, options);
            Directory.CreateDirectory(outputDirectory);
            return GetUniquePath(Path.Combine(outputDirectory, $"{pdfName}.pdf"));
        }

        private static string ResolveOutputDirectory(
            string sourceDirectory,
            PdfOutputPathOptions options)
        {
            if (options.SaveToSourcePath)
            {
                return sourceDirectory;
            }

            ArgumentException.ThrowIfNullOrWhiteSpace(options.OutputFolder);
            if (!options.KeepOriginalStructure)
            {
                return Path.GetFullPath(options.OutputFolder);
            }

            ArgumentException.ThrowIfNullOrWhiteSpace(options.InputFolder);
            var relativePath = Path.GetRelativePath(
                Path.GetFullPath(options.InputFolder),
                sourceDirectory);
            if (relativePath.StartsWith("..", StringComparison.Ordinal))
            {
                throw new ArgumentException("源文件不位于输入目录中。", nameof(sourceDirectory));
            }

            return Path.Combine(Path.GetFullPath(options.OutputFolder), relativePath);
        }

        private static string GetUniquePath(string candidatePath)
        {
            if (!File.Exists(candidatePath))
            {
                return candidatePath;
            }

            var directory = Path.GetDirectoryName(candidatePath) ?? string.Empty;
            var baseName = Path.GetFileNameWithoutExtension(candidatePath);
            var extension = Path.GetExtension(candidatePath);
            for (var index = 1; ; index++)
            {
                var suffixedPath = Path.Combine(directory, $"{baseName}_{index}{extension}");
                if (!File.Exists(suffixedPath))
                {
                    return suffixedPath;
                }
            }
        }
    }
}
