using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace XIAOFUTools.Features.General.DownloadOnlineImagery.Infrastructure
{
    internal sealed record OnlineImageryFileDeletion(
        string FileName,
        string Category,
        string ErrorMessage);

    internal sealed record OnlineImageryFileCleanupResult(
        IReadOnlyList<OnlineImageryFileDeletion> DeletedFiles,
        IReadOnlyList<OnlineImageryFileDeletion> FailedFiles)
    {
        internal int DeletedCount => DeletedFiles.Count;
    }

    internal sealed class OnlineImageryFileLifecycleService
    {
        private static readonly string[] AuxiliaryExtensions =
        {
            ".tfw", ".tifw", ".tif.aux.xml", ".tiff.aux.xml", ".aux.xml",
            ".ovr", ".rrd", ".xml", ".prj", ".clr", ".tif.xml", ".tiff.xml"
        };

        private static readonly string[] TemporaryFilePatterns =
        {
            "temp_*.*", "*_temp.*", "*.tmp", "*.temp", "*_grid.*", "grid_*.*"
        };

        public Task<bool> EnsureDirectoryAsync(string folderPath, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var existed = Directory.Exists(folderPath);
                Directory.CreateDirectory(folderPath);
                return !existed;
            }, cancellationToken);
        }

        public bool FileExists(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            return File.Exists(path);
        }

        public Task<OnlineImageryFileCleanupResult> DeleteRelatedFilesAsync(
            string mainFilePath,
            bool includeMainFile,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(mainFilePath);
            return Task.Run(() => DeleteRelatedFiles(mainFilePath, includeMainFile, cancellationToken), cancellationToken);
        }

        public Task<OnlineImageryFileCleanupResult> DeleteTemporaryFilesAsync(
            string outputFolder,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(outputFolder);
            return Task.Run(() =>
            {
                if (!Directory.Exists(outputFolder))
                {
                    return EmptyResult();
                }

                var candidates = new List<(string Path, string Category)>();
                foreach (var pattern in TemporaryFilePatterns)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        candidates.AddRange(Directory.EnumerateFiles(outputFolder, pattern)
                            .Select(path => (path, "临时文件")));
                    }
                    catch (Exception exception)
                    {
                        candidates.Add(($"{pattern}\0{exception.Message}", "查询失败"));
                    }
                }

                return DeleteCandidates(candidates, cancellationToken);
            }, cancellationToken);
        }

        private static OnlineImageryFileCleanupResult DeleteRelatedFiles(
            string mainFilePath,
            bool includeMainFile,
            CancellationToken cancellationToken)
        {
            var directory = Path.GetDirectoryName(mainFilePath);
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return EmptyResult();
            }

            var fileName = Path.GetFileName(mainFilePath);
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(mainFilePath);
            var candidates = new List<(string Path, string Category)>();
            if (includeMainFile)
            {
                candidates.Add((mainFilePath, "主文件"));
            }

            foreach (var extension in AuxiliaryExtensions)
            {
                candidates.Add((Path.Combine(directory, fileNameWithoutExtension + extension), "辅助文件"));
            }

            try
            {
                candidates.AddRange(Directory.EnumerateFiles(directory, fileNameWithoutExtension + ".*")
                    .Where(path => includeMainFile || !path.Equals(mainFilePath, StringComparison.OrdinalIgnoreCase))
                    .Select(path => (path, "相关文件")));
            }
            catch (Exception exception)
            {
                candidates.Add(($"{fileName}\0{exception.Message}", "查询失败"));
            }

            return DeleteCandidates(candidates, cancellationToken);
        }

        private static OnlineImageryFileCleanupResult DeleteCandidates(
            IEnumerable<(string Path, string Category)> candidates,
            CancellationToken cancellationToken)
        {
            var deleted = new List<OnlineImageryFileDeletion>();
            var failed = new List<OnlineImageryFileDeletion>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var separatorIndex = candidate.Path.IndexOf('\0');
                if (separatorIndex >= 0)
                {
                    failed.Add(new OnlineImageryFileDeletion(
                        candidate.Path[..separatorIndex],
                        candidate.Category,
                        candidate.Path[(separatorIndex + 1)..]));
                    continue;
                }

                if (!seenPaths.Add(candidate.Path) || !File.Exists(candidate.Path))
                {
                    continue;
                }

                try
                {
                    File.Delete(candidate.Path);
                    deleted.Add(new OnlineImageryFileDeletion(
                        Path.GetFileName(candidate.Path),
                        candidate.Category,
                        string.Empty));
                }
                catch (Exception exception)
                {
                    failed.Add(new OnlineImageryFileDeletion(
                        Path.GetFileName(candidate.Path),
                        candidate.Category,
                        exception.Message));
                }
            }

            return new OnlineImageryFileCleanupResult(deleted, failed);
        }

        private static OnlineImageryFileCleanupResult EmptyResult() => new(
            Array.Empty<OnlineImageryFileDeletion>(),
            Array.Empty<OnlineImageryFileDeletion>());
    }
}
