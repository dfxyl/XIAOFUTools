using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Data.Sqlite;
using XIAOFUTools.Features.User.AIAssistant.Database.Infrastructure;

namespace XIAOFUTools.Features.User.AIAssistant.Database
{
    /// <summary>
    /// SQLite数据库管理器 - 管理AI服务配置和对话历史
    /// 支持多进程并发访问（WAL模式）
    /// </summary>
    public partial class DatabaseManager : IDisposable
    {
        private static DatabaseManager _instance;
        private static readonly object _lock = new object();
        private static int _sqlitePclInitialized;
        private static string _lastInitError;
        private static readonly byte[] SqliteHeader =
        {
            0x53, 0x51, 0x4C, 0x69, 0x74, 0x65, 0x20, 0x66,
            0x6F, 0x72, 0x6D, 0x61, 0x74, 0x20, 0x33, 0x00
        };

        private readonly string _dbPath;
        private readonly AIAssistantConnectionFactory _connectionFactory;
        private readonly AIServiceRepository _serviceRepository;
        private readonly AIAssistantSessionRepository _sessionRepository;
        private readonly AIAssistantSettingsRepository _settingsRepository;
        private readonly AIAssistantDatabaseMigrator _databaseMigrator;
        private bool _isInitialized;

        private DatabaseManager()
        {
            EnsureSqliteInitialized();

            // 数据库存储在用户文档目录
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "XIAOFUTools", "AIAssistant");
            
            try
            {
                Directory.CreateDirectory(appDataPath);
            }
            catch (Exception ex)
            {
                // 降级到临时目录
                System.Diagnostics.Debug.WriteLine($"无法创建AppData目录: {ex.Message}，降级到临时目录");
                appDataPath = Path.Combine(Path.GetTempPath(), "XIAOFUTools", "AIAssistant");
                Directory.CreateDirectory(appDataPath);
            }
            
            _dbPath = Path.Combine(appDataPath, "aiassistant.db");
            EnsureDatabaseFileIntegrity();
            
            _connectionFactory = new AIAssistantConnectionFactory(_dbPath);
            _serviceRepository = new AIServiceRepository(_connectionFactory);
            _sessionRepository = new AIAssistantSessionRepository(_connectionFactory);
            _settingsRepository = new AIAssistantSettingsRepository(_connectionFactory);
            _databaseMigrator = new AIAssistantDatabaseMigrator(
                _connectionFactory,
                _settingsRepository,
                _serviceRepository.InitializeDefaults);
            
            InitializeDatabase();
            _isInitialized = true;
        }

    }
}
