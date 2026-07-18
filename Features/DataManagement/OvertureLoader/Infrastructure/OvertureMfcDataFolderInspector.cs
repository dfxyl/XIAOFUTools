using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Application;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Core;

namespace XIAOFUTools.Features.DataManagement.OvertureLoader.Infrastructure
{
    internal sealed class OvertureMfcDataFolderInspector : IOvertureMfcDataFolderInspector
    {
        public Task<bool> EnsureOutputFolderAsync(string folderPath, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Directory.Exists(folderPath))
                {
                    return false;
                }

                Directory.CreateDirectory(folderPath);
                return true;
            }, cancellationToken);
        }

        public Task<OvertureMfcDataFolderInspection> InspectAsync(
            string folderPath,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!Directory.Exists(folderPath))
                {
                    return OvertureMfcDataFolderInspection.Missing;
                }

                var parquetFileCount = CountFiles(folderPath, SearchOption.AllDirectories, cancellationToken);
                if (parquetFileCount > 0)
                {
                    return new OvertureMfcDataFolderInspection(
                        Exists: true,
                        ParquetFileCount: parquetFileCount,
                        ThemeFolders: Array.Empty<OvertureMfcThemeFolderInspection>());
                }

                var themes = new List<OvertureMfcThemeFolderInspection>();
                foreach (var themeFolder in Directory.GetDirectories(folderPath)
                             .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var types = new List<OvertureMfcTypeFolderInspection>();
                    foreach (var typeFolder in Directory.GetDirectories(themeFolder)
                                 .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        types.Add(new OvertureMfcTypeFolderInspection(
                            Path.GetFileName(typeFolder),
                            CountFiles(typeFolder, SearchOption.TopDirectoryOnly, cancellationToken)));
                    }

                    themes.Add(new OvertureMfcThemeFolderInspection(
                        Path.GetFileName(themeFolder),
                        types));
                }

                return new OvertureMfcDataFolderInspection(
                    Exists: true,
                    ParquetFileCount: 0,
                    ThemeFolders: themes);
            }, cancellationToken);
        }

        private static int CountFiles(
            string folderPath,
            SearchOption searchOption,
            CancellationToken cancellationToken)
        {
            var count = 0;
            foreach (var _ in Directory.EnumerateFiles(folderPath, "*.parquet", searchOption))
            {
                cancellationToken.ThrowIfCancellationRequested();
                count++;
            }

            return count;
        }
    }
}
