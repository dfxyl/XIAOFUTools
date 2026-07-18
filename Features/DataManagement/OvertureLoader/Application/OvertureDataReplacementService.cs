using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Core;

namespace XIAOFUTools.Features.DataManagement.OvertureLoader.Application
{
    internal sealed class OvertureDataReplacementService : IOvertureDataReplacementService
    {
        private readonly IOvertureDataFolderStore _folderStore;
        private readonly IOvertureMapMemberCleanupService _mapCleanupService;

        public OvertureDataReplacementService(
            IOvertureDataFolderStore folderStore,
            IOvertureMapMemberCleanupService mapCleanupService)
        {
            _folderStore = folderStore ?? throw new ArgumentNullException(nameof(folderStore));
            _mapCleanupService = mapCleanupService ??
                throw new ArgumentNullException(nameof(mapCleanupService));
        }

        public async Task<bool> HasExistingDataAsync(
            string dataRoot,
            IEnumerable<string> actualTypes,
            CancellationToken cancellationToken)
        {
            var folders = OvertureDataFolderPlanner.BuildThemeFolders(dataRoot, actualTypes);
            foreach (var folder in folders)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await _folderStore
                    .ContainsParquetFilesAsync(folder, cancellationToken)
                    .ConfigureAwait(false))
                {
                    return true;
                }
            }

            return false;
        }

        public async Task<OvertureCleanupResult> CleanupAsync(
            string dataRoot,
            IEnumerable<string> actualTypes,
            IProgress<OvertureCleanupProgress> progress,
            CancellationToken cancellationToken)
        {
            var plannedFolders = OvertureDataFolderPlanner.BuildThemeFolders(dataRoot, actualTypes);
            var existingFolders = new List<string>();
            foreach (var folder in plannedFolders)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await _folderStore.ExistsAsync(folder, cancellationToken).ConfigureAwait(false))
                {
                    existingFolders.Add(folder);
                }
            }

            var issues = new List<OvertureCleanupIssue>();
            var removedMapMembers = 0;
            for (var index = 0; index < existingFolders.Count; index++)
            {
                var folder = existingFolders[index];
                progress?.Report(new OvertureCleanupProgress(
                    OvertureCleanupStage.RemovingMapMembers,
                    folder,
                    index + 1,
                    existingFolders.Count));
                try
                {
                    removedMapMembers += await _mapCleanupService
                        .RemoveFromActiveMapUsingFolderAsync(folder, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    issues.Add(new OvertureCleanupIssue(folder, ex.Message));
                }
            }

            var deletedFolders = 0;
            for (var index = 0; index < existingFolders.Count; index++)
            {
                var folder = existingFolders[index];
                progress?.Report(new OvertureCleanupProgress(
                    OvertureCleanupStage.DeletingFolder,
                    folder,
                    index + 1,
                    existingFolders.Count));
                try
                {
                    await _folderStore.DeleteAsync(folder, cancellationToken).ConfigureAwait(false);
                    deletedFolders++;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    issues.Add(new OvertureCleanupIssue(folder, ex.Message));
                }
            }

            return new OvertureCleanupResult(
                existingFolders.Count,
                removedMapMembers,
                deletedFolders,
                issues);
        }
    }
}
