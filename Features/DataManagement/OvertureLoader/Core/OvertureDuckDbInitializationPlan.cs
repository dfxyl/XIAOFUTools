using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace XIAOFUTools.Features.DataManagement.OvertureLoader.Core
{
    internal static class OvertureDuckDbInitializationPlan
    {
        private static readonly string[] RequiredExtensions = { "spatial", "httpfs" };

        public const string DirectExtensionSql = @"
INSTALL spatial;
INSTALL httpfs;
LOAD spatial;
LOAD httpfs;";

        public const string ConnectionSettingsSql = @"
SET s3_region='us-west-2';
SET enable_http_metadata_cache=true;
SET enable_object_cache=true;
SET enable_progress_bar=true;
SET http_timeout=300000;
SET http_retries=5;
SET http_retry_wait_ms=1000;
SET s3_use_ssl=true;
SET threads=4;
SET memory_limit='2GB';
SET max_memory='4GB';";

        public static string ResolveExtensionDirectory(string assemblyLocation)
        {
            if (string.IsNullOrWhiteSpace(assemblyLocation))
            {
                throw new ArgumentException("程序集路径不能为空。", nameof(assemblyLocation));
            }

            var assemblyDirectory = Path.GetDirectoryName(Path.GetFullPath(assemblyLocation));
            if (string.IsNullOrWhiteSpace(assemblyDirectory))
            {
                throw new ArgumentException("无法解析程序集目录。", nameof(assemblyLocation));
            }

            return Path.Combine(assemblyDirectory, "Extensions");
        }

        public static bool HasRequiredExtensions(IEnumerable<string> extensionFiles)
        {
            ArgumentNullException.ThrowIfNull(extensionFiles);
            var names = extensionFiles
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return RequiredExtensions.All(required =>
                names.Contains($"{required}.duckdb_extension"));
        }

        public static string BuildBundledExtensionSql(string extensionDirectory)
        {
            if (string.IsNullOrWhiteSpace(extensionDirectory))
            {
                throw new ArgumentException("扩展目录不能为空。", nameof(extensionDirectory));
            }

            var normalizedDirectory = Path.GetFullPath(extensionDirectory)
                .Replace('\\', '/')
                .Replace("'", "''", StringComparison.Ordinal);
            return $@"
SET extension_directory='{normalizedDirectory}';
LOAD spatial;
LOAD httpfs;";
        }

        public static string BuildExtensionFailureMessage(string extensionDirectory)
        {
            return
                "加载 DuckDB 扩展失败。\n\n" +
                "处理要求：\n" +
                "1. Extensions 目录必须同时包含 spatial.duckdb_extension 和 httpfs.duckdb_extension；\n" +
                "2. 扩展版本和平台必须与当前 DuckDB 运行时一致；\n" +
                "3. 可由 DuckDB 在线安装，或从 DuckDB 官方扩展仓库下载匹配文件；\n" +
                $"4. 当前扩展搜索路径：{extensionDirectory}";
        }
    }
}
