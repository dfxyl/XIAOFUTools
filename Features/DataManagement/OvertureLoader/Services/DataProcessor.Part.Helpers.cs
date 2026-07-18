using System;
using System.Threading;
using System.Threading.Tasks;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Core;

namespace XIAOFUTools.Features.DataManagement.OvertureLoader.Services
{
    public partial class DataProcessor
    {

        /// <summary>
        /// 检查缓存是否有效
        /// </summary>
        private static bool IsCacheValid(string key)
        {
            return _schemaCache.ContainsKey(key) &&
                   DateTime.Now - _schemaCache[key] < CACHE_EXPIRY;
        }

        public async Task InitializeDuckDBAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _duckDbInitializer.InitializeAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to initialize DuckDB: {ex.Message}",
                    ex);
            }
        }

        internal async Task<bool> IngestFileAsync(
            string s3Path,
            OvertureExtent? extent = null,
            string actualS3Type = null,
            IProgress<string> progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                progress?.Report($"正在连接到 S3: {actualS3Type ?? "数据"}...");

                // Store the actualS3Type for context
                _currentActualS3Type = actualS3Type;

                // Clear previous parent theme context, it will be set by CreateFeatureLayerAsync if needed
                _currentParentS3Theme = null;

                progress?.Report("正在从 S3 读取架构...");
                if (extent != null)
                {
                    progress?.Report("正在应用当前地图范围的空间过滤器...");
                }
                progress?.Report("正在从 S3 加载数据 (可能需要一些时间)...");

                var result = await _duckDbDataSession.IngestAsync(
                    s3Path,
                    extent,
                    cancellationToken);
                progress?.Report($"成功从 S3 加载了 {result.RowCount:N0} 行数据");
                System.Diagnostics.Debug.WriteLine(
                    $"Loaded {result.RowCount} rows with {result.ColumnCount} columns");
                if (!result.HasRows)
                {
                    progress?.Report("数据集为空 - 没有要处理的要素");
                }
                return result.HasRows;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Query error: {ex.Message}");
                return false;
            }
        }

    }
}
