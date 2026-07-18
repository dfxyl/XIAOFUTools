using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace XIAOFUTools.Features.Conversion.TxtToFeature.Infrastructure
{
    internal sealed class TxtToFeatureFileStore
    {
        public Task<bool> DirectoryExistsAsync(string folderPath, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return !string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath);
            }, cancellationToken);
        }

        public Task<IReadOnlyList<string>> FindTextFilesAsync(
            string folderPath,
            bool includeSubfolders,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

            return Task.Run<IReadOnlyList<string>>(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!Directory.Exists(folderPath))
                {
                    return Array.Empty<string>();
                }

                var searchOption = includeSubfolders
                    ? SearchOption.AllDirectories
                    : SearchOption.TopDirectoryOnly;
                var files = new List<string>();
                foreach (var filePath in Directory.EnumerateFiles(folderPath, "*.txt", searchOption))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    files.Add(filePath);
                }

                return files
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }, cancellationToken);
        }

        public Task EnsureDirectoryAsync(string folderPath, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                Directory.CreateDirectory(folderPath);
            }, cancellationToken);
        }

        public Task<TxtTemporaryDirectoryCleanupResult> CleanupTemporaryDirectoryAsync(
            string folderPath,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!Directory.Exists(folderPath))
                {
                    return new TxtTemporaryDirectoryCleanupResult(
                        Existed: false,
                        Deleted: true,
                        Failures: Array.Empty<string>());
                }

                var failures = new List<string>();
                Thread.Sleep(1000);
                for (var attempt = 0; attempt < 5; attempt++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        NormalizeAttributes(folderPath);
                        Directory.Delete(folderPath, recursive: true);
                        return new TxtTemporaryDirectoryCleanupResult(
                            Existed: true,
                            Deleted: true,
                            Failures: failures);
                    }
                    catch (Exception exception)
                    {
                        failures.Add(exception.Message);
                        if (attempt < 4)
                        {
                            Thread.Sleep(2000);
                        }
                    }
                }

                return new TxtTemporaryDirectoryCleanupResult(
                    Existed: true,
                    Deleted: false,
                    Failures: failures);
            }, cancellationToken);
        }

        private static void NormalizeAttributes(string folderPath)
        {
            var directory = new DirectoryInfo(folderPath);
            directory.Attributes = FileAttributes.Normal;

            foreach (var filePath in Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories))
            {
                try
                {
                    File.SetAttributes(filePath, FileAttributes.Normal);
                }
                catch
                {
                    // 无法复位的单个文件会由删除流程继续处理。
                }
            }

            foreach (var directoryPath in Directory.EnumerateDirectories(folderPath, "*", SearchOption.AllDirectories))
            {
                try
                {
                    new DirectoryInfo(directoryPath).Attributes = FileAttributes.Normal;
                }
                catch
                {
                    // 无法复位的子目录会由删除流程继续处理。
                }
            }
        }
    }

    internal sealed record TxtTemporaryDirectoryCleanupResult(
        bool Existed,
        bool Deleted,
        IReadOnlyList<string> Failures);
}
