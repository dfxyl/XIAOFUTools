#nullable enable

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using XIAOFUTools.Features.Custom.GisToolPocket.Core;
namespace XIAOFUTools.Features.Custom.GisToolPocket.Infrastructure
{
    internal static class ToolboxInternalStorageService
    {
        private const string StorageDirectoryName = "Toolboxes";

        public static string StorageRoot
        {
            get
            {
                var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(root, "GIS Toolbox", StorageDirectoryName);
            }
        }

        public static string Import(string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                throw new ArgumentException("工具箱路径为空。", nameof(sourcePath));

            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("工具箱文件不存在。", sourcePath);

            Directory.CreateDirectory(StorageRoot);

            var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
            return extension switch
            {
                ".atbx" => ImportSingleFile(sourcePath),
                ".tbx" => ImportSingleFile(sourcePath),
                ".pyt" => ImportPythonToolboxDirectory(sourcePath),
                _ => throw new NotSupportedException("仅支持 atbx、tbx 和 pyt 工具箱。")
            };
        }

        public static bool IsInternalPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            var fullPath = Path.GetFullPath(path);
            var fullRoot = Path.GetFullPath(StorageRoot);
            return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
        }

        public static void ClearStorage()
        {
            if (Directory.Exists(StorageRoot))
            {
                ClearReadOnlyAttributes(StorageRoot);
                Directory.Delete(StorageRoot, true);
            }

            Directory.CreateDirectory(StorageRoot);
        }

        private static string ImportSingleFile(string sourcePath)
        {
            var fileName = Path.GetFileNameWithoutExtension(sourcePath);
            var extension = Path.GetExtension(sourcePath);
            var targetPath = Path.Combine(StorageRoot, $"{SanitizeFileName(fileName)}_{ShortHash(sourcePath)}{extension}");
            if (PathsEqual(sourcePath, targetPath))
                return targetPath;

            CopyFileReplacing(sourcePath, targetPath);
            return targetPath;
        }

        private static string ImportPythonToolboxDirectory(string sourcePath)
        {
            var sourceDirectory = Path.GetDirectoryName(sourcePath);
            if (string.IsNullOrWhiteSpace(sourceDirectory) || !Directory.Exists(sourceDirectory))
                return ImportSingleFile(sourcePath);

            var folderName = $"{SanitizeFileName(Path.GetFileNameWithoutExtension(sourcePath))}_{ShortHash(sourcePath)}";
            var targetDirectory = Path.Combine(StorageRoot, folderName);
            var targetPath = Path.Combine(targetDirectory, Path.GetFileName(sourcePath));
            if (PathsEqual(sourcePath, targetPath))
                return targetPath;

            ResetDirectory(targetDirectory);
            CopyPythonToolboxBundle(sourcePath, targetDirectory);
            return targetPath;
        }

        private static void CopyDirectory(string sourceDirectory, string targetDirectory)
        {
            Directory.CreateDirectory(targetDirectory);

            foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourceDirectory, directory);
                Directory.CreateDirectory(Path.Combine(targetDirectory, relativePath));
            }

            foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourceDirectory, file);
                var targetPath = Path.Combine(targetDirectory, relativePath);
                var parent = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrWhiteSpace(parent))
                    Directory.CreateDirectory(parent);

                CopyFileReplacing(file, targetPath);
            }
        }

        private static void CopyPythonToolboxBundle(string sourcePath, string targetDirectory)
        {
            var sourceDirectory = Path.GetDirectoryName(sourcePath)!;
            var mainToolboxPath = Path.GetFullPath(sourcePath);

            Directory.CreateDirectory(targetDirectory);

            foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                if (ShouldSkipPythonBundleDirectory(directory))
                    continue;

                var relativePath = Path.GetRelativePath(sourceDirectory, directory);
                Directory.CreateDirectory(Path.Combine(targetDirectory, relativePath));
            }

            foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                if (!ShouldCopyPythonBundleFile(file, mainToolboxPath))
                    continue;

                var relativePath = Path.GetRelativePath(sourceDirectory, file);
                var targetPath = Path.Combine(targetDirectory, relativePath);
                var parent = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrWhiteSpace(parent))
                    Directory.CreateDirectory(parent);

                CopyFileReplacing(file, targetPath);
            }
        }

        private static bool ShouldCopyPythonBundleFile(string filePath, string mainToolboxPath)
        {
            if (IsUnderSkippedPythonBundleDirectory(filePath))
                return false;

            var fullPath = Path.GetFullPath(filePath);
            if (fullPath.Equals(mainToolboxPath, StringComparison.OrdinalIgnoreCase))
                return true;

            var extension = Path.GetExtension(fullPath);
            return !extension.Equals(".pyt", StringComparison.OrdinalIgnoreCase) &&
                   !extension.Equals(".tbx", StringComparison.OrdinalIgnoreCase) &&
                   !extension.Equals(".atbx", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldSkipPythonBundleDirectory(string directoryPath)
        {
            var name = Path.GetFileName(directoryPath);
            return name.Equals("__pycache__", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals(".vs", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("obj", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsUnderSkippedPythonBundleDirectory(string filePath)
        {
            var current = Path.GetDirectoryName(filePath);
            while (!string.IsNullOrWhiteSpace(current))
            {
                if (ShouldSkipPythonBundleDirectory(current))
                    return true;

                current = Path.GetDirectoryName(current);
            }

            return false;
        }

        private static void ResetDirectory(string directory)
        {
            if (Directory.Exists(directory))
            {
                ClearReadOnlyAttributes(directory);
                Directory.Delete(directory, true);
            }

            Directory.CreateDirectory(directory);
        }

        private static void CopyFileReplacing(string sourcePath, string targetPath)
        {
            if (File.Exists(targetPath))
                File.SetAttributes(targetPath, FileAttributes.Normal);

            File.Copy(sourcePath, targetPath, true);
            File.SetAttributes(targetPath, File.GetAttributes(targetPath) & ~FileAttributes.ReadOnly);
        }

        private static void ClearReadOnlyAttributes(string directory)
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);

            foreach (var childDirectory in Directory.EnumerateDirectories(directory, "*", SearchOption.AllDirectories))
                File.SetAttributes(childDirectory, FileAttributes.Normal);

            File.SetAttributes(directory, FileAttributes.Normal);
        }

        private static string SanitizeFileName(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Length);
            foreach (var character in value)
                builder.Append(Array.IndexOf(invalid, character) >= 0 ? '_' : character);

            return builder.Length == 0 ? "toolbox" : builder.ToString();
        }

        private static string ShortHash(string value)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(value).ToLowerInvariant()));
            return Convert.ToHexString(bytes, 0, 6).ToLowerInvariant();
        }

        private static bool PathsEqual(string left, string right)
        {
            return Path.GetFullPath(left).Equals(Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }
    }
}

