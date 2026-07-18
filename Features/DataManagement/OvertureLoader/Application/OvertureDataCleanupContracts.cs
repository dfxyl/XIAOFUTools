using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XIAOFUTools.Features.DataManagement.OvertureLoader.Application
{
    internal enum OvertureCleanupStage
    {
        RemovingMapMembers,
        DeletingFolder
    }

    internal sealed record OvertureCleanupProgress(
        OvertureCleanupStage Stage,
        string FolderPath,
        int Current,
        int Total);

    internal sealed record OvertureCleanupIssue(string FolderPath, string Message);

    internal sealed record OvertureCleanupResult(
        int ExistingFolderCount,
        int RemovedMapMemberCount,
        int DeletedFolderCount,
        IReadOnlyList<OvertureCleanupIssue> Issues);

    internal interface IOvertureDataFolderStore
    {
        Task<bool> ExistsAsync(string folderPath, CancellationToken cancellationToken);

        Task<bool> ContainsParquetFilesAsync(
            string folderPath,
            CancellationToken cancellationToken);

        Task DeleteAsync(string folderPath, CancellationToken cancellationToken);
    }

    internal interface IOvertureMapMemberCleanupService
    {
        Task<int> RemoveFromActiveMapUsingFolderAsync(
            string folderPath,
            CancellationToken cancellationToken);

        Task<int> RemoveFromProjectMapsUsingFileAsync(
            string filePath,
            CancellationToken cancellationToken);
    }

    internal interface IOvertureDataReplacementService
    {
        Task<bool> HasExistingDataAsync(
            string dataRoot,
            IEnumerable<string> actualTypes,
            CancellationToken cancellationToken);

        Task<OvertureCleanupResult> CleanupAsync(
            string dataRoot,
            IEnumerable<string> actualTypes,
            IProgress<OvertureCleanupProgress> progress,
            CancellationToken cancellationToken);
    }
}
