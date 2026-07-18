using System;
using System.Threading;
using System.Threading.Tasks;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Application;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Core;
using XIAOFUTools.Shared.Diagnostics;

namespace XIAOFUTools.Features.DataManagement.OvertureLoader.Infrastructure
{
    internal sealed class OvertureDuckDbInitializer : IOvertureDuckDbInitializer
    {
        private readonly IOvertureDuckDbCommandExecutor _commandExecutor;
        private readonly IOvertureDuckDbExtensionCatalog _extensionCatalog;
        private readonly IAppLogger _logger;
        private readonly string _extensionDirectory;

        public OvertureDuckDbInitializer(
            IOvertureDuckDbCommandExecutor commandExecutor,
            IOvertureDuckDbExtensionCatalog extensionCatalog,
            IAppLogger logger,
            string assemblyLocation)
        {
            _commandExecutor = commandExecutor ??
                throw new ArgumentNullException(nameof(commandExecutor));
            _extensionCatalog = extensionCatalog ??
                throw new ArgumentNullException(nameof(extensionCatalog));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _extensionDirectory = OvertureDuckDbInitializationPlan
                .ResolveExtensionDirectory(assemblyLocation);
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            await _commandExecutor.OpenAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                await _commandExecutor.ExecuteNonQueryAsync(
                    OvertureDuckDbInitializationPlan.DirectExtensionSql,
                    cancellationToken).ConfigureAwait(false);
                _logger.Write(
                    AppLogLevel.Information,
                    "DuckDB spatial/httpfs 扩展在线安装并加载成功。");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception onlineException)
            {
                _logger.Write(
                    AppLogLevel.Warning,
                    "DuckDB 扩展在线安装失败，转用插件本地扩展。",
                    onlineException);
                var extensionFiles = _extensionCatalog.GetExtensionFiles(_extensionDirectory);
                if (!OvertureDuckDbInitializationPlan.HasRequiredExtensions(extensionFiles))
                {
                    throw new InvalidOperationException(
                        OvertureDuckDbInitializationPlan.BuildExtensionFailureMessage(
                            _extensionDirectory),
                        onlineException);
                }

                try
                {
                    await _commandExecutor.ExecuteNonQueryAsync(
                        OvertureDuckDbInitializationPlan.BuildBundledExtensionSql(
                            _extensionDirectory),
                        cancellationToken).ConfigureAwait(false);
                    _logger.Write(
                        AppLogLevel.Information,
                        $"已从本地目录加载 DuckDB 扩展：{_extensionDirectory}");
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception bundledException)
                {
                    throw new InvalidOperationException(
                        OvertureDuckDbInitializationPlan.BuildExtensionFailureMessage(
                            _extensionDirectory),
                        new AggregateException(onlineException, bundledException));
                }
            }

            await _commandExecutor.ExecuteNonQueryAsync(
                OvertureDuckDbInitializationPlan.ConnectionSettingsSql,
                cancellationToken).ConfigureAwait(false);
            _logger.Write(
                AppLogLevel.Information,
                "DuckDB Overture 连接参数初始化完成。");
        }
    }
}
