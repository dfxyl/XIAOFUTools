using XIAOFUTools.Features.DataManagement.OvertureLoader.Application;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Core;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Infrastructure;
using XIAOFUTools.Shared.Diagnostics;

namespace XIAOFUTools.Tests.OvertureLoader;

public sealed class OvertureDuckDbInitializerTests
{
    private const string AssemblyLocation = @"C:\Program Files\XIAOFUTools\XIAOFUTools.dll";

    [Fact]
    public void InitializationPlan_RequiresBothExtensionsAndEscapesDirectory()
    {
        Assert.True(OvertureDuckDbInitializationPlan.HasRequiredExtensions(new[]
        {
            @"C:\extensions\SPATIAL.duckdb_extension",
            @"C:\extensions\httpfs.duckdb_extension"
        }));
        Assert.False(OvertureDuckDbInitializationPlan.HasRequiredExtensions(new[]
        {
            @"C:\extensions\spatial.duckdb_extension"
        }));

        var sql = OvertureDuckDbInitializationPlan.BuildBundledExtensionSql(
            @"C:\O'Brien\Extensions");

        Assert.Contains("C:/O''Brien/Extensions", sql, StringComparison.Ordinal);
        Assert.Contains("LOAD spatial", sql, StringComparison.Ordinal);
        Assert.Contains("LOAD httpfs", sql, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InitializeAsync_UsesDirectInstallThenAppliesSettings()
    {
        var executor = new RecordingCommandExecutor();
        var initializer = CreateInitializer(executor, Array.Empty<string>());

        await initializer.InitializeAsync(CancellationToken.None);

        Assert.True(executor.WasOpened);
        Assert.Equal(2, executor.Commands.Count);
        Assert.Equal(
            OvertureDuckDbInitializationPlan.DirectExtensionSql,
            executor.Commands[0]);
        Assert.Equal(
            OvertureDuckDbInitializationPlan.ConnectionSettingsSql,
            executor.Commands[1]);
    }

    [Fact]
    public async Task InitializeAsync_FallsBackToBundledExtensions()
    {
        var executor = new RecordingCommandExecutor
        {
            FailFirstCommand = true
        };
        var initializer = CreateInitializer(executor, new[]
        {
            @"C:\extensions\spatial.duckdb_extension",
            @"C:\extensions\httpfs.duckdb_extension"
        });

        await initializer.InitializeAsync(CancellationToken.None);

        Assert.Equal(3, executor.Commands.Count);
        Assert.Contains("SET extension_directory=", executor.Commands[1], StringComparison.Ordinal);
        Assert.Equal(
            OvertureDuckDbInitializationPlan.ConnectionSettingsSql,
            executor.Commands[2]);
    }

    [Fact]
    public async Task InitializeAsync_ReportsMissingBundledExtensions()
    {
        var executor = new RecordingCommandExecutor
        {
            FailFirstCommand = true
        };
        var initializer = CreateInitializer(executor, new[]
        {
            @"C:\extensions\spatial.duckdb_extension"
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            initializer.InitializeAsync(CancellationToken.None));

        Assert.Contains("spatial.duckdb_extension", exception.Message, StringComparison.Ordinal);
        Assert.Contains("httpfs.duckdb_extension", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("1.2.0", exception.Message, StringComparison.Ordinal);
    }

    private static OvertureDuckDbInitializer CreateInitializer(
        RecordingCommandExecutor executor,
        IReadOnlyList<string> extensionFiles)
    {
        return new OvertureDuckDbInitializer(
            executor,
            new StaticExtensionCatalog(extensionFiles),
            new AppLogger(),
            AssemblyLocation);
    }

    private sealed class RecordingCommandExecutor : IOvertureDuckDbCommandExecutor
    {
        public bool FailFirstCommand { get; init; }
        public bool WasOpened { get; private set; }
        public List<string> Commands { get; } = new();

        public Task OpenAsync(CancellationToken cancellationToken)
        {
            WasOpened = true;
            return Task.CompletedTask;
        }

        public Task ExecuteNonQueryAsync(
            string commandText,
            CancellationToken cancellationToken)
        {
            Commands.Add(commandText);
            if (FailFirstCommand && Commands.Count == 1)
            {
                throw new InvalidOperationException("network unavailable");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class StaticExtensionCatalog(IReadOnlyList<string> files)
        : IOvertureDuckDbExtensionCatalog
    {
        public IReadOnlyList<string> GetExtensionFiles(string extensionDirectory)
        {
            return files;
        }
    }
}
