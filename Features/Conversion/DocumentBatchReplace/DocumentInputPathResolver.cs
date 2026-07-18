using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace XIAOFUTools.Features.Conversion.DocumentBatchReplace
{
    internal sealed class DocumentInputPathResolver : IDocumentInputPathResolver
    {
        internal static readonly IReadOnlySet<string> SupportedExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".docx", ".doc", ".docm"
            };

        public DocumentInputResolution Resolve(
            IEnumerable<string> paths,
            bool traverseSubfolders)
        {
            if (paths == null)
            {
                return new DocumentInputResolution([], 0);
            }

            var files = new List<string>();
            var skippedCount = 0;
            var searchOption = traverseSubfolders
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            foreach (var rawPath in paths)
            {
                if (!TryGetFullPath(rawPath, out var fullPath))
                {
                    skippedCount++;
                    continue;
                }

                if (File.Exists(fullPath))
                {
                    if (SupportedExtensions.Contains(Path.GetExtension(fullPath)))
                    {
                        files.Add(fullPath);
                    }
                    else
                    {
                        skippedCount++;
                    }

                    continue;
                }

                if (!Directory.Exists(fullPath))
                {
                    skippedCount++;
                    continue;
                }

                files.AddRange(EnumerateWordFiles(fullPath, searchOption));
            }

            return new DocumentInputResolution(files, skippedCount);
        }

        private static IEnumerable<string> EnumerateWordFiles(
            string folder,
            SearchOption searchOption)
        {
            var files = new List<string>();
            foreach (var extension in SupportedExtensions)
            {
                try
                {
                    files.AddRange(Directory.GetFiles(
                        folder,
                        $"*{extension}",
                        searchOption));
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            files.Sort(StringComparer.OrdinalIgnoreCase);
            return files;
        }

        private static bool TryGetFullPath(string? path, out string fullPath)
        {
            fullPath = string.Empty;
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                fullPath = Path.GetFullPath(path);
                return true;
            }
            catch (Exception exception) when (
                exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return false;
            }
        }
    }
}
