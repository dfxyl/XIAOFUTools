using XIAOFUTools.Features.DataManagement.OvertureLoader.Application;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Core;

namespace XIAOFUTools.Tests.OvertureLoader;

public sealed class OvertureDataCleanupTests
{
    [Fact]
    public void FolderPlanner_ReturnsStableUniqueThemeFolders()
    {
        var root = Path.Combine(Path.GetTempPath(), "overture-data");

        var folders = OvertureDataFolderPlanner.BuildThemeFolders(
            root,
            new[] { "land", "water", "LAND", " " });

        Assert.Equal(
            new[] { Path.Combine(Path.GetFullPath(root), "land"), Path.Combine(Path.GetFullPath(root), "water") },
            folders);
    }

    [Theory]
    [InlineData("..")]
    [InlineData("base/land")]
    [InlineData("base\\land")]
    public void FolderPlanner_RejectsPathsOutsideThemeRoot(string actualType)
    {
        Assert.Throws<ArgumentException>(() =>
            OvertureDataFolderPlanner.BuildThemeFolders("C:\\data", new[] { actualType }));
    }

    [Fact]
    public void PathScope_DoesNotMatchSiblingFolderWithCommonPrefix()
    {
        var root = Path.Combine(Path.GetTempPath(), "overture-scope");
        var land = Path.Combine(root, "land");

        Assert.True(OverturePathScope.IsWithinFolder(Path.Combine(land, "part.parquet"), land));
        Assert.False(OverturePathScope.IsWithinFolder(
            Path.Combine(root, "land_use", "part.parquet"),
            land));
    }

    [Fact]
    public void PathScope_ResolvesParquetDatasetSuffix()
    {
        var parquetFile = Path.Combine(Path.GetTempPath(), "places.parquet");

        Assert.True(OverturePathScope.RefersToFile(
            parquetFile + Path.DirectorySeparatorChar + "dataset_name",
            parquetFile));
    }

    [Fact]
    public async Task ReplacementService_DetectsExistingParquetData()
    {
        var store = new RecordingFolderStore
        {
            FoldersWithParquet = { "land" }
        };
        var service = new OvertureDataReplacementService(store, new RecordingMapCleanupService());

        var found = await service.HasExistingDataAsync(
            "C:\\data",
            new[] { "water", "land" },
            CancellationToken.None);

        Assert.True(found);
        Assert.Equal(new[] { "water", "land" }, store.ParquetChecks);
    }

    [Fact]
    public async Task ReplacementService_RemovesMapMembersBeforeDeletingFolders()
    {
        var store = new RecordingFolderStore
        {
            ExistingFolders = { "land", "water" }
        };
        var mapCleanup = new RecordingMapCleanupService();
        var service = new OvertureDataReplacementService(store, mapCleanup);
        var progress = new RecordingProgress();

        var result = await service.CleanupAsync(
            "C:\\data",
            new[] { "land", "water" },
            progress,
            CancellationToken.None);

        Assert.Equal(2, result.ExistingFolderCount);
        Assert.Equal(2, result.RemovedMapMemberCount);
        Assert.Equal(2, result.DeletedFolderCount);
        Assert.Empty(result.Issues);
        Assert.Equal(new[] { "land", "water" }, mapCleanup.RemovedFolders);
        Assert.Equal(new[] { "land", "water" }, store.DeletedFolders);
        Assert.Equal(
            new[]
            {
                OvertureCleanupStage.RemovingMapMembers,
                OvertureCleanupStage.RemovingMapMembers,
                OvertureCleanupStage.DeletingFolder,
                OvertureCleanupStage.DeletingFolder
            },
            progress.Items.Select(item => item.Stage));
    }

    private sealed class RecordingFolderStore : IOvertureDataFolderStore
    {
        public HashSet<string> ExistingFolders { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> FoldersWithParquet { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> ParquetChecks { get; } = new();
        public List<string> DeletedFolders { get; } = new();

        public Task<bool> ExistsAsync(string folderPath, CancellationToken cancellationToken)
        {
            return Task.FromResult(ExistingFolders.Contains(Path.GetFileName(folderPath)));
        }

        public Task<bool> ContainsParquetFilesAsync(
            string folderPath,
            CancellationToken cancellationToken)
        {
            var folderName = Path.GetFileName(folderPath);
            ParquetChecks.Add(folderName);
            return Task.FromResult(FoldersWithParquet.Contains(folderName));
        }

        public Task DeleteAsync(string folderPath, CancellationToken cancellationToken)
        {
            DeletedFolders.Add(Path.GetFileName(folderPath));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingMapCleanupService : IOvertureMapMemberCleanupService
    {
        public List<string> RemovedFolders { get; } = new();

        public Task<int> RemoveFromActiveMapUsingFolderAsync(
            string folderPath,
            CancellationToken cancellationToken)
        {
            RemovedFolders.Add(Path.GetFileName(folderPath));
            return Task.FromResult(1);
        }

        public Task<int> RemoveFromProjectMapsUsingFileAsync(
            string filePath,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(0);
        }
    }

    private sealed class RecordingProgress : IProgress<OvertureCleanupProgress>
    {
        public List<OvertureCleanupProgress> Items { get; } = new();

        public void Report(OvertureCleanupProgress value)
        {
            Items.Add(value);
        }
    }
}
