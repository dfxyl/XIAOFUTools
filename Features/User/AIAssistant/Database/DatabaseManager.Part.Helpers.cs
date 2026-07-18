using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Data.Sqlite;

namespace XIAOFUTools.Features.User.AIAssistant.Database
{
    public partial class DatabaseManager
    {

        private static void EnsureSqliteInitialized()
        {
            if (Interlocked.Exchange(ref _sqlitePclInitialized, 1) == 1)
            {
                return;
            }

            try
            {
                SQLitePCL.Batteries_V2.Init();
                System.Diagnostics.Debug.WriteLine("SQLitePCL初始化成功");
            }
            catch (Exception ex)
            {
                // 重置标志，允许下次重试
                Interlocked.Exchange(ref _sqlitePclInitialized, 0);
                System.Diagnostics.Debug.WriteLine($"SQLitePCL初始化失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"堆栈: {ex.StackTrace}");
                throw new InvalidOperationException(
                    "SQLite本地库初始化失败，可能缺少运行时组件。" +
                    "请确保已安装 Visual C++ Redistributable。" +
                    $"\n详细信息: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 尝试初始化，不抛出异常
        /// </summary>
        public static bool TryInitialize(out string error)
        {
            error = null;
            try
            {
                var _ = Instance;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private void EnsureDatabaseFileIntegrity()
        {
            try
            {
                if (Directory.Exists(_dbPath))
                {
                    BackupInvalidDatabase("数据库路径被目录占用");
                    return;
                }

                if (!File.Exists(_dbPath))
                {
                    return;
                }

                var fileInfo = new FileInfo(_dbPath);
                if (fileInfo.Length <= 0)
                {
                    BackupInvalidDatabase("数据库文件为空");
                    return;
                }

                if (!LooksLikeSqliteDatabase(_dbPath))
                {
                    BackupInvalidDatabase("数据库文件头不是SQLite格式");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"数据库完整性预检查失败: {ex.Message}");
            }
        }

        private static bool LooksLikeSqliteDatabase(string path)
        {
            try
            {
                var header = new byte[SqliteHeader.Length];
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (stream.Length < SqliteHeader.Length)
                    {
                        return false;
                    }

                    var read = stream.Read(header, 0, header.Length);
                    if (read < SqliteHeader.Length)
                    {
                        return false;
                    }
                }

                for (var i = 0; i < SqliteHeader.Length; i++)
                {
                    if (header[i] != SqliteHeader[i])
                    {
                        return false;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private void BackupInvalidDatabase(string reason, Exception ex = null)
        {
            var backupTag = $"invalid_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}";
            var backupPath = $"{_dbPath}.{backupTag}.bak";

            try
            {
                if (Directory.Exists(_dbPath))
                {
                    Directory.Move(_dbPath, backupPath);
                }
                else if (File.Exists(_dbPath))
                {
                    File.Move(_dbPath, backupPath);
                }

                MoveSidecarFileIfExists($"{_dbPath}-wal", $"{backupPath}-wal");
                MoveSidecarFileIfExists($"{_dbPath}-shm", $"{backupPath}-shm");

                System.Diagnostics.Debug.WriteLine($"检测到无效SQLite数据库，已备份: {backupPath}，原因: {reason}");
                if (ex != null)
                {
                    System.Diagnostics.Debug.WriteLine($"触发异常: {ex.Message}");
                }
            }
            catch (Exception backupEx)
            {
                System.Diagnostics.Debug.WriteLine($"备份无效数据库失败: {backupEx.Message}，将尝试直接删除");

                SafeDeleteFile(_dbPath);
                SafeDeleteFile($"{_dbPath}-wal");
                SafeDeleteFile($"{_dbPath}-shm");
            }
        }

        private static void MoveSidecarFileIfExists(string sourcePath, string targetPath)
        {
            if (!File.Exists(sourcePath))
            {
                return;
            }

            try
            {
                File.Move(sourcePath, targetPath);
            }
            catch
            {
                SafeDeleteFile(sourcePath);
            }
        }

        private static void SafeDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // 忽略清理失败，后续初始化会继续尝试。
            }
        }

        private static bool IsNotDatabaseError(SqliteException ex)
        {
            if (ex == null)
            {
                return false;
            }

            return ex.SqliteErrorCode == 26 ||
                   ex.SqliteExtendedErrorCode == 26 ||
                   ex.Message.IndexOf("file is not a database", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void InitializeDatabase()
        {
            try
            {
                InitializeDatabaseCore();
            }
            catch (SqliteException ex) when (IsNotDatabaseError(ex))
            {
                // 某些机器上残留了非SQLite文件，自动备份并重建可避免AI助手启动失败。
                BackupInvalidDatabase("初始化数据库时检测到非SQLite文件", ex);
                InitializeDatabaseCore();
            }
        }

        private void InitializeDatabaseCore()
        {
            _databaseMigrator.Migrate();
        }
    }
}
