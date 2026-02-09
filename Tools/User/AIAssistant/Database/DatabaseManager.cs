using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Data.Sqlite;

namespace XIAOFUTools.Tools.User.AIAssistant.Database
{
    /// <summary>
    /// SQLite数据库管理器 - 管理AI服务配置和对话历史
    /// 支持多进程并发访问（WAL模式）
    /// </summary>
    public class DatabaseManager : IDisposable
    {
        private static DatabaseManager _instance;
        private static readonly object _lock = new object();
        private static int _sqlitePclInitialized;
        private static string _lastInitError;
        private readonly string _dbPath;
        private readonly string _connectionString;
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
            
            // SQLite连接字符串，启用WAL模式支持多进程并发
            _connectionString = $"Data Source={_dbPath};Mode=ReadWriteCreate;Cache=Shared";
            
            InitializeDatabase();
            _isInitialized = true;
        }

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

        public static DatabaseManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            try
                            {
                                _instance = new DatabaseManager();
                                _lastInitError = null;
                            }
                            catch (Exception ex)
                            {
                                _lastInitError = ex.Message;
                                System.Diagnostics.Debug.WriteLine($"DatabaseManager初始化失败: {ex.Message}");
                                throw;
                            }
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 获取上次初始化错误信息（用于诊断）
        /// </summary>
        public static string LastInitError => _lastInitError;

        /// <summary>
        /// 检查数据库是否可用
        /// </summary>
        public static bool IsAvailable
        {
            get
            {
                try
                {
                    return _instance != null && _instance._isInitialized;
                }
                catch
                {
                    return false;
                }
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

        /// <summary>
        /// 获取新的数据库连接（每次操作使用独立连接，避免并发问题）
        /// </summary>
        private SqliteConnection GetConnection()
        {
            var conn = new SqliteConnection(_connectionString);
            conn.Open();
            
            // 启用WAL模式，支持多进程并发读写
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
                cmd.ExecuteNonQuery();
            }
            
            return conn;
        }

        private void InitializeDatabase()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"开始初始化SQLite数据库，路径: {_dbPath}");
                
                using (var conn = GetConnection())
                {
                    // 创建AI服务配置表
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS ai_services (
                                id INTEGER PRIMARY KEY AUTOINCREMENT,
                                name TEXT NOT NULL,
                                api_endpoint TEXT NOT NULL,
                                model_name TEXT NOT NULL,
                                api_key TEXT NOT NULL,
                                is_default INTEGER DEFAULT 0,
                                max_tokens INTEGER DEFAULT 4096,
                                temperature REAL DEFAULT 0.7,
                                supports_vision INTEGER DEFAULT 0,
                                created_at TEXT DEFAULT CURRENT_TIMESTAMP,
                                updated_at TEXT DEFAULT CURRENT_TIMESTAMP
                            )";
                        cmd.ExecuteNonQuery();
                    }

                    // 创建对话历史表
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS conversation_history (
                                id INTEGER PRIMARY KEY AUTOINCREMENT,
                                session_id TEXT NOT NULL,
                                role TEXT NOT NULL,
                                content TEXT NOT NULL,
                                thinking TEXT,
                                images TEXT,
                                turn_id TEXT,
                                timestamp TEXT DEFAULT CURRENT_TIMESTAMP,
                                token_count INTEGER DEFAULT 0
                            )";
                        cmd.ExecuteNonQuery();
                    }

                    // 创建GIS工具调用记录表
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS tool_call_history (
                                id INTEGER PRIMARY KEY AUTOINCREMENT,
                                session_id TEXT NOT NULL,
                                turn_id TEXT,
                                call_id TEXT,
                                tool_name TEXT NOT NULL,
                                parameters TEXT,
                                result TEXT,
                                status TEXT NOT NULL,
                                timestamp TEXT DEFAULT CURRENT_TIMESTAMP
                            )";
                        cmd.ExecuteNonQuery();
                    }

                    // 创建会话表
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS sessions (
                                session_id TEXT PRIMARY KEY,
                                title TEXT,
                                created_at TEXT DEFAULT CURRENT_TIMESTAMP,
                                last_activity TEXT DEFAULT CURRENT_TIMESTAMP
                            )";
                        cmd.ExecuteNonQuery();
                    }

                    // 创建Python执行记录表
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS python_executions (
                                id INTEGER PRIMARY KEY AUTOINCREMENT,
                                session_id TEXT NOT NULL,
                                message_id INTEGER,
                                code_id TEXT NOT NULL,
                                code TEXT NOT NULL,
                                output TEXT,
                                error TEXT,
                                success INTEGER DEFAULT 0,
                                execution_time_ms INTEGER DEFAULT 0,
                                timestamp TEXT DEFAULT CURRENT_TIMESTAMP
                            )";
                        cmd.ExecuteNonQuery();
                    }

                    // 创建索引提升查询性能
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            CREATE INDEX IF NOT EXISTS idx_conversation_session ON conversation_history(session_id);
                            CREATE INDEX IF NOT EXISTS idx_tool_call_session ON tool_call_history(session_id);
                            CREATE INDEX IF NOT EXISTS idx_python_exec_session ON python_executions(session_id);
                        ";
                        cmd.ExecuteNonQuery();
                    }

                    // 兼容旧版本数据库：补齐新增列
                    EnsureColumnExists(conn, "conversation_history", "turn_id", "TEXT");
                    EnsureColumnExists(conn, "tool_call_history", "turn_id", "TEXT");
                    EnsureColumnExists(conn, "tool_call_history", "call_id", "TEXT");

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            CREATE INDEX IF NOT EXISTS idx_conversation_turn ON conversation_history(turn_id);
                            CREATE INDEX IF NOT EXISTS idx_tool_call_turn ON tool_call_history(turn_id);
                        ";
                        cmd.ExecuteNonQuery();
                    }

                    // 创建上下文总结表（用于长对话压缩）
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS context_summaries (
                                id INTEGER PRIMARY KEY AUTOINCREMENT,
                                session_id TEXT NOT NULL,
                                summary TEXT NOT NULL,
                                summarized_up_to_id INTEGER NOT NULL,
                                message_count INTEGER DEFAULT 0,
                                estimated_tokens INTEGER DEFAULT 0,
                                timestamp TEXT DEFAULT CURRENT_TIMESTAMP
                            );
                            CREATE INDEX IF NOT EXISTS idx_summary_session ON context_summaries(session_id);
                        ";
                        cmd.ExecuteNonQuery();
                    }

                    // 创建联网搜索设置表
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS search_settings (
                                key TEXT PRIMARY KEY,
                                value TEXT NOT NULL DEFAULT ''
                            )";
                        cmd.ExecuteNonQuery();
                    }

                    // 初始化默认搜索设置
                    InitializeDefaultSearchSettings(conn);

                    // 创建工具执行设置表
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS tool_settings (
                                key TEXT PRIMARY KEY,
                                value TEXT NOT NULL DEFAULT ''
                            )";
                        cmd.ExecuteNonQuery();
                    }

                    // 初始化默认工具设置
                    InitializeDefaultToolSettings(conn);
                }

                // 初始化默认服务配置
                InitializeDefaultServices();
                
                System.Diagnostics.Debug.WriteLine("SQLite数据库初始化完成");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"数据库初始化失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                throw;
            }
        }

        private static void EnsureColumnExists(SqliteConnection conn, string tableName, string columnName, string columnType)
        {
            if (ColumnExists(conn, tableName, columnName))
            {
                return;
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnType}";
                cmd.ExecuteNonQuery();
            }
        }

        private static bool ColumnExists(SqliteConnection conn, string tableName, string columnName)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"PRAGMA table_info({tableName})";
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (!reader.IsDBNull(1) && string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private void InitializeDefaultServices()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("开始初始化默认服务配置...");
                
                var defaultServices = new[]
                {
                    new AIServiceConfig
                    {
                        Name = "DeepSeek Chat",
                        ApiEndpoint = "https://api.deepseek.com/v1",
                        ModelName = "deepseek-chat",
                        ApiKey = "sk-3df1480042c041a8b034da2296b04fa2",
                        IsDefault = true,
                        MaxTokens = 8192,
                        Temperature = 0.7,
                        SupportsVision = false
                    },
                    new AIServiceConfig
                    {
                        Name = "DeepSeek Reasoner",
                        ApiEndpoint = "https://api.deepseek.com/v1",
                        ModelName = "deepseek-reasoner",
                        ApiKey = "sk-3df1480042c041a8b034da2296b04fa2",
                        IsDefault = false,
                        MaxTokens = 8192,
                        Temperature = 1.0,
                        SupportsVision = false
                    },
                    new AIServiceConfig
                    {
                        Name = "SiliconFlow GLM-4.6V",
                        ApiEndpoint = "https://api.siliconflow.cn/v1/",
                        ModelName = "zai-org/GLM-4.6V",
                        ApiKey = "sk-vxzwulcezfneinkzkefolpbgikpdhxnrdpasnoygjdpcamyi",
                        IsDefault = false,
                        MaxTokens = 128000,
                        Temperature = 0.7,
                        SupportsVision = true
                    },
                    new AIServiceConfig
                    {
                        Name = "Moonshot Kimi K2",
                        ApiEndpoint = "https://api.moonshot.cn/v1",
                        ModelName = "kimi-k2-0905-preview",
                        ApiKey = "sk-Khak6GZI5AWDbhZAFdJd6C9T6dgguhcX1WnbRATiZyzk7fCm",
                        IsDefault = false,
                        MaxTokens = 256000,
                        Temperature = 0.7,
                        SupportsVision = false
                    }
                };
                
                using (var conn = GetConnection())
                {
                    foreach (var service in defaultServices)
                    {
                        // 检查模型是否已存在
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT COUNT(*) FROM ai_services WHERE model_name = @model";
                            cmd.Parameters.AddWithValue("@model", service.ModelName);
                            var exists = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                            
                            if (!exists)
                            {
                                InsertServiceInternal(conn, service);
                                System.Diagnostics.Debug.WriteLine($"已添加服务: {service.Name}");
                            }
                            else if (service.ModelName.Contains("deepseek"))
                            {
                                // 更新已存在的DeepSeek配置
                                using (var updateCmd = conn.CreateCommand())
                                {
                                    updateCmd.CommandText = @"
                                        UPDATE ai_services 
                                        SET max_tokens = @maxTokens, temperature = @temp
                                        WHERE model_name = @model";
                                    updateCmd.Parameters.AddWithValue("@maxTokens", service.MaxTokens);
                                    updateCmd.Parameters.AddWithValue("@temp", service.Temperature);
                                    updateCmd.Parameters.AddWithValue("@model", service.ModelName);
                                    updateCmd.ExecuteNonQuery();
                                }
                            }
                        }
                    }

                    // 兼容迁移：将旧的GLM-4.5V升级为GLM-4.6V（128K）
                    using (var oldCmd = conn.CreateCommand())
                    {
                        oldCmd.CommandText = "SELECT COUNT(*) FROM ai_services WHERE model_name = @oldModel";
                        oldCmd.Parameters.AddWithValue("@oldModel", "zai-org/GLM-4.5V");
                        var oldExists = Convert.ToInt32(oldCmd.ExecuteScalar()) > 0;

                        if (oldExists)
                        {
                            using (var newCmd = conn.CreateCommand())
                            {
                                newCmd.CommandText = "SELECT COUNT(*) FROM ai_services WHERE model_name = @newModel";
                                newCmd.Parameters.AddWithValue("@newModel", "zai-org/GLM-4.6V");
                                var newExists = Convert.ToInt32(newCmd.ExecuteScalar()) > 0;

                                if (!newExists)
                                {
                                    using (var updateCmd = conn.CreateCommand())
                                    {
                                        updateCmd.CommandText = @"
                                            UPDATE ai_services
                                            SET name = @name,
                                                model_name = @newModel,
                                                max_tokens = @maxTokens,
                                                supports_vision = 1
                                            WHERE model_name = @oldModel";
                                        updateCmd.Parameters.AddWithValue("@name", "SiliconFlow GLM-4.6V");
                                        updateCmd.Parameters.AddWithValue("@newModel", "zai-org/GLM-4.6V");
                                        updateCmd.Parameters.AddWithValue("@maxTokens", 128000);
                                        updateCmd.Parameters.AddWithValue("@oldModel", "zai-org/GLM-4.5V");
                                        updateCmd.ExecuteNonQuery();
                                    }
                                }
                                else
                                {
                                    using (var deleteCmd = conn.CreateCommand())
                                    {
                                        deleteCmd.CommandText = "DELETE FROM ai_services WHERE model_name = @oldModel";
                                        deleteCmd.Parameters.AddWithValue("@oldModel", "zai-org/GLM-4.5V");
                                        deleteCmd.ExecuteNonQuery();
                                    }
                                }
                            }
                        }
                    }
                }
                
                System.Diagnostics.Debug.WriteLine("服务配置检查完成");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"初始化默认服务失败: {ex.Message}");
            }
        }

        private void InsertServiceInternal(SqliteConnection conn, AIServiceConfig service)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT INTO ai_services (name, api_endpoint, model_name, api_key, is_default, max_tokens, temperature, supports_vision)
                    VALUES (@name, @endpoint, @model, @key, @default, @maxTokens, @temp, @vision)";
                
                cmd.Parameters.AddWithValue("@name", service.Name);
                cmd.Parameters.AddWithValue("@endpoint", service.ApiEndpoint);
                cmd.Parameters.AddWithValue("@model", service.ModelName);
                cmd.Parameters.AddWithValue("@key", service.ApiKey);
                cmd.Parameters.AddWithValue("@default", service.IsDefault ? 1 : 0);
                cmd.Parameters.AddWithValue("@maxTokens", service.MaxTokens);
                cmd.Parameters.AddWithValue("@temp", service.Temperature);
                cmd.Parameters.AddWithValue("@vision", service.SupportsVision ? 1 : 0);
                
                cmd.ExecuteNonQuery();
            }
        }

        #region AI服务配置管理

        public void InsertService(AIServiceConfig service)
        {
            using (var conn = GetConnection())
            {
                InsertServiceInternal(conn, service);
            }
        }

        public AIServiceConfig GetDefaultService()
        {
            try
            {
                using (var conn = GetConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT id, name, api_endpoint, model_name, api_key, is_default, max_tokens, temperature, supports_vision FROM ai_services WHERE is_default = 1 LIMIT 1";
                    
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new AIServiceConfig
                            {
                                Id = reader.GetInt32(0),
                                Name = reader.GetString(1),
                                ApiEndpoint = reader.GetString(2),
                                ModelName = reader.GetString(3),
                                ApiKey = reader.GetString(4),
                                IsDefault = reader.GetInt32(5) == 1,
                                MaxTokens = reader.GetInt32(6),
                                Temperature = reader.GetDouble(7),
                                SupportsVision = !reader.IsDBNull(8) && reader.GetInt32(8) == 1
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取默认服务失败: {ex.Message}");
            }
            return null;
        }

        public List<AIServiceConfig> GetAllServices()
        {
            var services = new List<AIServiceConfig>();
            using (var conn = GetConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT id, name, api_endpoint, model_name, api_key, is_default, max_tokens, temperature, supports_vision FROM ai_services ORDER BY is_default DESC, name";
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        services.Add(new AIServiceConfig
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.GetString(1),
                            ApiEndpoint = reader.GetString(2),
                            ModelName = reader.GetString(3),
                            ApiKey = reader.GetString(4),
                            IsDefault = reader.GetInt32(5) == 1,
                            MaxTokens = reader.GetInt32(6),
                            Temperature = reader.GetDouble(7),
                            SupportsVision = !reader.IsDBNull(8) && reader.GetInt32(8) == 1
                        });
                    }
                }
            }
            return services;
        }

        public void UpdateService(AIServiceConfig service)
        {
            using (var conn = GetConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    UPDATE ai_services 
                    SET name = @name, api_endpoint = @endpoint, model_name = @model, api_key = @key, 
                        is_default = @default, max_tokens = @maxTokens, temperature = @temp, 
                        supports_vision = @vision, updated_at = CURRENT_TIMESTAMP
                    WHERE id = @id";
                
                cmd.Parameters.AddWithValue("@name", service.Name);
                cmd.Parameters.AddWithValue("@endpoint", service.ApiEndpoint);
                cmd.Parameters.AddWithValue("@model", service.ModelName);
                cmd.Parameters.AddWithValue("@key", service.ApiKey);
                cmd.Parameters.AddWithValue("@default", service.IsDefault ? 1 : 0);
                cmd.Parameters.AddWithValue("@maxTokens", service.MaxTokens);
                cmd.Parameters.AddWithValue("@temp", service.Temperature);
                cmd.Parameters.AddWithValue("@vision", service.SupportsVision ? 1 : 0);
                cmd.Parameters.AddWithValue("@id", service.Id);
                
                cmd.ExecuteNonQuery();
            }
        }

        public void DeleteService(int serviceId)
        {
            using (var conn = GetConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM ai_services WHERE id = @id";
                cmd.Parameters.AddWithValue("@id", serviceId);
                cmd.ExecuteNonQuery();
            }
        }
        
        public void ResetToDefaultServices()
        {
            using (var conn = GetConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM ai_services";
                cmd.ExecuteNonQuery();
            }
            InitializeDefaultServices();
        }
        
        public void SetDefaultService(int serviceId)
        {
            using (var conn = GetConnection())
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "UPDATE ai_services SET is_default = 0";
                    cmd.ExecuteNonQuery();
                }
                
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "UPDATE ai_services SET is_default = 1 WHERE id = @id";
                    cmd.Parameters.AddWithValue("@id", serviceId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        #endregion


        #region 联网搜索设置管理

        private void InitializeDefaultSearchSettings(SqliteConnection conn)
        {
            var defaults = new Dictionary<string, string>
            {
                { "tavily_api_key", "" },
                { "brave_api_key", "" },
                { "enable_searxng", "true" },
                { "searxng_instance_url", "" },
                { "enable_bing_scraper", "true" }
            };

            foreach (var kv in defaults)
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "INSERT OR IGNORE INTO search_settings (key, value) VALUES (@key, @value)";
                    cmd.Parameters.AddWithValue("@key", kv.Key);
                    cmd.Parameters.AddWithValue("@value", kv.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// 获取联网搜索设置
        /// </summary>
        public SearchSettings GetSearchSettings()
        {
            var settings = new SearchSettings();
            try
            {
                using (var conn = GetConnection())
                {
                    // 确保表存在
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS search_settings (
                                key TEXT PRIMARY KEY,
                                value TEXT NOT NULL DEFAULT ''
                            )";
                        cmd.ExecuteNonQuery();
                    }

                    InitializeDefaultSearchSettings(conn);

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT key, value FROM search_settings";
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var key = reader.GetString(0);
                                var value = reader.GetString(1);
                                switch (key)
                                {
                                    case "tavily_api_key":
                                        settings.TavilyApiKey = value;
                                        break;
                                    case "brave_api_key":
                                        settings.BraveApiKey = value;
                                        break;
                                    case "enable_searxng":
                                        settings.EnableSearXNG = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                                        break;
                                    case "searxng_instance_url":
                                        settings.SearXNGInstanceUrl = value;
                                        break;
                                    case "enable_bing_scraper":
                                        settings.EnableBingScraper = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                                        break;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取搜索设置失败: {ex.Message}");
            }
            return settings;
        }

        /// <summary>
        /// 保存联网搜索设置
        /// </summary>
        public void SaveSearchSettings(SearchSettings settings)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    // 确保表存在
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS search_settings (
                                key TEXT PRIMARY KEY,
                                value TEXT NOT NULL DEFAULT ''
                            )";
                        cmd.ExecuteNonQuery();
                    }

                    var values = new Dictionary<string, string>
                    {
                        { "tavily_api_key", settings.TavilyApiKey ?? "" },
                        { "brave_api_key", settings.BraveApiKey ?? "" },
                        { "enable_searxng", settings.EnableSearXNG ? "true" : "false" },
                        { "searxng_instance_url", settings.SearXNGInstanceUrl ?? "" },
                        { "enable_bing_scraper", settings.EnableBingScraper ? "true" : "false" }
                    };

                    foreach (var kv in values)
                    {
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "INSERT OR REPLACE INTO search_settings (key, value) VALUES (@key, @value)";
                            cmd.Parameters.AddWithValue("@key", kv.Key);
                            cmd.Parameters.AddWithValue("@value", kv.Value);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存搜索设置失败: {ex.Message}");
            }
        }

        #endregion


        #region 工具执行设置管理

        private const string PromptBeforeToolInChatKey = "prompt_before_tool_in_chat";
        private const string SensitiveModeKey = "sensitive_mode";
        private const string LegacyWebFetchKey = "enable_web_fetch";
        private const string ToolSettingsBaselineVersionKey = "tool_settings_baseline_version";
        private const string CurrentToolSettingsBaselineVersion = "2";

        private void InitializeDefaultToolSettings(SqliteConnection conn)
        {
            var defaults = new Dictionary<string, string>
            {
                { PromptBeforeToolInChatKey, "true" },
                { SensitiveModeKey, "true" },
                { ToolSettingsBaselineVersionKey, CurrentToolSettingsBaselineVersion }
            };

            foreach (var tool in AIAssistantToolCatalog.Tools)
            {
                defaults[BuildToolChatKey(tool.ToolName)] = tool.DefaultChatEnabled ? "true" : "false";
                defaults[BuildToolAgentKey(tool.ToolName)] = tool.DefaultAgentEnabled ? "true" : "false";
            }

            foreach (var kv in defaults)
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "INSERT OR IGNORE INTO tool_settings (key, value) VALUES (@key, @value)";
                    cmd.Parameters.AddWithValue("@key", kv.Key);
                    cmd.Parameters.AddWithValue("@value", kv.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public ToolExecutionSettings GetToolExecutionSettings()
        {
            var settings = new ToolExecutionSettings();
            try
            {
                using (var conn = GetConnection())
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS tool_settings (
                                key TEXT PRIMARY KEY,
                                value TEXT NOT NULL DEFAULT ''
                            )";
                        cmd.ExecuteNonQuery();
                    }

                    InitializeDefaultToolSettings(conn);

                    var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT key, value FROM tool_settings";
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var key = reader.GetString(0);
                                var value = reader.GetString(1);
                                values[key] = value;
                            }
                        }
                    }

                    ApplyToolSettingsBaselineIfNeeded(conn, values);

                    if (values.TryGetValue(PromptBeforeToolInChatKey, out var promptValue))
                    {
                        settings.PromptBeforeToolInChat = promptValue.Equals("true", StringComparison.OrdinalIgnoreCase);
                    }

                    if (values.TryGetValue(SensitiveModeKey, out var sensitiveModeValue))
                    {
                        settings.SensitiveMode = sensitiveModeValue.Equals("true", StringComparison.OrdinalIgnoreCase);
                    }
                    else
                    {
                        settings.SensitiveMode = true;
                        using (var saveCmd = conn.CreateCommand())
                        {
                            saveCmd.CommandText = "INSERT OR REPLACE INTO tool_settings (key, value) VALUES (@key, @value)";
                            saveCmd.Parameters.AddWithValue("@key", SensitiveModeKey);
                            saveCmd.Parameters.AddWithValue("@value", "true");
                            saveCmd.ExecuteNonQuery();
                        }

                        values[SensitiveModeKey] = "true";
                    }

                    // 兼容旧版本开关
                    var legacyWebFetchEnabled = true;
                    if (values.TryGetValue(LegacyWebFetchKey, out var legacyWebFetchValue))
                    {
                        legacyWebFetchEnabled = legacyWebFetchValue.Equals("true", StringComparison.OrdinalIgnoreCase);
                    }

                    foreach (var tool in AIAssistantToolCatalog.Tools)
                    {
                        var chatEnabled = tool.DefaultChatEnabled;
                        var agentEnabled = tool.DefaultAgentEnabled;

                        if (values.TryGetValue(BuildToolChatKey(tool.ToolName), out var chatValue))
                        {
                            chatEnabled = chatValue.Equals("true", StringComparison.OrdinalIgnoreCase);
                        }

                        if (values.TryGetValue(BuildToolAgentKey(tool.ToolName), out var agentValue))
                        {
                            agentEnabled = agentValue.Equals("true", StringComparison.OrdinalIgnoreCase);
                        }

                        if (string.Equals(tool.ToolName, "web_fetch", StringComparison.OrdinalIgnoreCase) && !legacyWebFetchEnabled)
                        {
                            chatEnabled = false;
                            agentEnabled = false;
                        }

                        settings.SetToolMode(tool.ToolName, chatEnabled, agentEnabled);
                    }

                    settings.EnsureDefaults();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取工具执行设置失败: {ex.Message}");
            }

            return settings;
        }

        public void SaveToolExecutionSettings(ToolExecutionSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            try
            {
                settings.EnsureDefaults();

                using (var conn = GetConnection())
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS tool_settings (
                                key TEXT PRIMARY KEY,
                                value TEXT NOT NULL DEFAULT ''
                            )";
                        cmd.ExecuteNonQuery();
                    }

                    var values = new Dictionary<string, string>
                    {
                        { PromptBeforeToolInChatKey, settings.PromptBeforeToolInChat ? "true" : "false" },
                        { SensitiveModeKey, settings.SensitiveMode ? "true" : "false" },
                        { ToolSettingsBaselineVersionKey, CurrentToolSettingsBaselineVersion }
                    };

                    foreach (var tool in AIAssistantToolCatalog.Tools)
                    {
                        var toolMode = settings.ToolModes.TryGetValue(tool.ToolName, out var mode)
                            ? mode
                            : new ToolModeSetting
                            {
                                ToolName = tool.ToolName,
                                ChatEnabled = tool.DefaultChatEnabled,
                                AgentEnabled = tool.DefaultAgentEnabled
                            };

                        values[BuildToolChatKey(tool.ToolName)] = toolMode.ChatEnabled ? "true" : "false";
                        values[BuildToolAgentKey(tool.ToolName)] = toolMode.AgentEnabled ? "true" : "false";

                        // 兼容旧版本配置
                        if (string.Equals(tool.ToolName, "web_fetch", StringComparison.OrdinalIgnoreCase))
                        {
                            values[LegacyWebFetchKey] = (toolMode.ChatEnabled || toolMode.AgentEnabled) ? "true" : "false";
                        }
                    }

                    foreach (var kv in values)
                    {
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "INSERT OR REPLACE INTO tool_settings (key, value) VALUES (@key, @value)";
                            cmd.Parameters.AddWithValue("@key", kv.Key);
                            cmd.Parameters.AddWithValue("@value", kv.Value);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存工具执行设置失败: {ex.Message}");
            }
        }

        private void ApplyToolSettingsBaselineIfNeeded(SqliteConnection conn, Dictionary<string, string> currentValues)
        {
            if (currentValues.TryGetValue(ToolSettingsBaselineVersionKey, out var baselineVersion)
                && string.Equals(baselineVersion, CurrentToolSettingsBaselineVersion, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var baselineValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { PromptBeforeToolInChatKey, "true" },
                { SensitiveModeKey, "true" },
                { ToolSettingsBaselineVersionKey, CurrentToolSettingsBaselineVersion }
            };

            foreach (var tool in AIAssistantToolCatalog.Tools)
            {
                baselineValues[BuildToolChatKey(tool.ToolName)] = tool.DefaultChatEnabled ? "true" : "false";
                baselineValues[BuildToolAgentKey(tool.ToolName)] = tool.DefaultAgentEnabled ? "true" : "false";

                if (string.Equals(tool.ToolName, "web_fetch", StringComparison.OrdinalIgnoreCase))
                {
                    baselineValues[LegacyWebFetchKey] = (tool.DefaultChatEnabled || tool.DefaultAgentEnabled) ? "true" : "false";
                }
            }

            foreach (var kv in baselineValues)
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "INSERT OR REPLACE INTO tool_settings (key, value) VALUES (@key, @value)";
                    cmd.Parameters.AddWithValue("@key", kv.Key);
                    cmd.Parameters.AddWithValue("@value", kv.Value);
                    cmd.ExecuteNonQuery();
                }

                currentValues[kv.Key] = kv.Value;
            }
        }

        private static string BuildToolChatKey(string toolName)
        {
            return $"tool_{toolName}_chat_enabled";
        }

        private static string BuildToolAgentKey(string toolName)
        {
            return $"tool_{toolName}_agent_enabled";
        }

        #endregion


        #region 对话历史管理

        public void SaveMessage(string sessionId, string role, string content, int tokenCount = 0, string thinking = null, string images = null, string turnId = null)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    // 检查会话是否存在
                    bool sessionExists = false;
                    using (var checkCmd = conn.CreateCommand())
                    {
                        checkCmd.CommandText = "SELECT COUNT(*) FROM sessions WHERE session_id = @sid";
                        checkCmd.Parameters.AddWithValue("@sid", sessionId);
                        sessionExists = Convert.ToInt32(checkCmd.ExecuteScalar()) > 0;
                    }
                    
                    if (!sessionExists && role == "user")
                    {
                        var title = content.Length > 30 ? content.Substring(0, 30) + "..." : content;
                        CreateSessionInternal(conn, sessionId, title);
                    }
                    else if (!sessionExists)
                    {
                        CreateSessionInternal(conn, sessionId, null);
                    }
                    else
                    {
                        UpdateSessionActivityInternal(conn, sessionId);
                    }
                    
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            INSERT INTO conversation_history (session_id, role, content, thinking, images, turn_id, token_count)
                            VALUES (@sid, @role, @content, @thinking, @images, @turnId, @tokens)";
                        
                        cmd.Parameters.AddWithValue("@sid", sessionId);
                        cmd.Parameters.AddWithValue("@role", role);
                        cmd.Parameters.AddWithValue("@content", content);
                        cmd.Parameters.AddWithValue("@thinking", (object)thinking ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@images", (object)images ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@turnId", (object)turnId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@tokens", tokenCount);
                        
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveMessage失败: {ex.Message}");
                throw;
            }
        }

        public List<ConversationMessage> GetConversationHistory(string sessionId, int limit = 50)
        {
            var messages = new List<ConversationMessage>();
            using (var conn = GetConnection())
            using (var cmd = conn.CreateCommand())
            {
                if (limit > 0)
                {
                    cmd.CommandText = @"
                    SELECT id, role, content, thinking, images, timestamp, token_count, turn_id
                    FROM conversation_history 
                    WHERE session_id = @sid 
                    ORDER BY id DESC 
                    LIMIT @limit";
                    cmd.Parameters.AddWithValue("@limit", limit);
                }
                else
                {
                    cmd.CommandText = @"
                    SELECT id, role, content, thinking, images, timestamp, token_count, turn_id
                    FROM conversation_history 
                    WHERE session_id = @sid 
                    ORDER BY id DESC";
                }
                
                cmd.Parameters.AddWithValue("@sid", sessionId);
                
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        messages.Add(new ConversationMessage
                        {
                            Id = reader.GetInt32(0),
                            Role = reader.GetString(1),
                            Content = reader.GetString(2),
                            Thinking = reader.IsDBNull(3) ? null : reader.GetString(3),
                            Images = reader.IsDBNull(4) ? null : reader.GetString(4),
                            Timestamp = DateTime.Parse(reader.GetString(5)),
                            TokenCount = reader.GetInt32(6),
                            TurnId = reader.IsDBNull(7) ? null : reader.GetString(7)
                        });
                    }
                }
            }
            messages.Reverse();
            return messages;
        }

        public void ClearConversationHistory(string sessionId)
        {
            using (var conn = GetConnection())
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM conversation_history WHERE session_id = @sid";
                    cmd.Parameters.AddWithValue("@sid", sessionId);
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM tool_call_history WHERE session_id = @sid";
                    cmd.Parameters.AddWithValue("@sid", sessionId);
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM python_executions WHERE session_id = @sid";
                    cmd.Parameters.AddWithValue("@sid", sessionId);
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM context_summaries WHERE session_id = @sid";
                    cmd.Parameters.AddWithValue("@sid", sessionId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        #endregion

        #region 会话管理

        private void CreateSessionInternal(SqliteConnection conn, string sessionId, string title)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT OR REPLACE INTO sessions (session_id, title, created_at, last_activity)
                    VALUES (@sid, @title, @now, @now)";
                
                var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                cmd.Parameters.AddWithValue("@sid", sessionId);
                cmd.Parameters.AddWithValue("@title", title ?? $"会话 {DateTime.Now:yyyy-MM-dd HH:mm}");
                cmd.Parameters.AddWithValue("@now", now);
                
                cmd.ExecuteNonQuery();
            }
        }

        private void UpdateSessionActivityInternal(SqliteConnection conn, string sessionId)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "UPDATE sessions SET last_activity = @now WHERE session_id = @sid";
                cmd.Parameters.AddWithValue("@now", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("@sid", sessionId);
                cmd.ExecuteNonQuery();
            }
        }

        public void CreateSession(string sessionId, string title = null)
        {
            using (var conn = GetConnection())
            {
                CreateSessionInternal(conn, sessionId, title);
            }
        }

        public void UpdateSessionActivity(string sessionId)
        {
            using (var conn = GetConnection())
            {
                UpdateSessionActivityInternal(conn, sessionId);
            }
        }

        public void UpdateSessionTitle(string sessionId, string title)
        {
            using (var conn = GetConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "UPDATE sessions SET title = @title WHERE session_id = @sid";
                cmd.Parameters.AddWithValue("@title", title);
                cmd.Parameters.AddWithValue("@sid", sessionId);
                cmd.ExecuteNonQuery();
            }
        }

        public List<Session> GetRecentSessions(int limit = 20)
        {
            var sessions = new List<Session>();
            using (var conn = GetConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT s.session_id, s.title, s.created_at, s.last_activity 
                    FROM sessions s
                    WHERE EXISTS (
                        SELECT 1 FROM conversation_history ch 
                        WHERE ch.session_id = s.session_id
                    )
                    ORDER BY s.last_activity DESC 
                    LIMIT @limit";
                
                cmd.Parameters.AddWithValue("@limit", limit);
                
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        sessions.Add(new Session
                        {
                            SessionId = reader.GetString(0),
                            Title = reader.GetString(1),
                            CreatedAt = DateTime.Parse(reader.GetString(2)),
                            LastActivity = DateTime.Parse(reader.GetString(3))
                        });
                    }
                }
            }
            return sessions;
        }

        public void DeleteSession(string sessionId)
        {
            using (var conn = GetConnection())
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM conversation_history WHERE session_id = @sid";
                    cmd.Parameters.AddWithValue("@sid", sessionId);
                    cmd.ExecuteNonQuery();
                }
                
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM tool_call_history WHERE session_id = @sid";
                    cmd.Parameters.AddWithValue("@sid", sessionId);
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM python_executions WHERE session_id = @sid";
                    cmd.Parameters.AddWithValue("@sid", sessionId);
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM context_summaries WHERE session_id = @sid";
                    cmd.Parameters.AddWithValue("@sid", sessionId);
                    cmd.ExecuteNonQuery();
                }
                 
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM sessions WHERE session_id = @sid";
                    cmd.Parameters.AddWithValue("@sid", sessionId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        #endregion

        #region 工具调用记录

        public void SaveToolCall(string sessionId, string toolName, string parameters, string result, string status, string turnId = null, string callId = null)
        {
            using (var conn = GetConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT INTO tool_call_history (session_id, turn_id, call_id, tool_name, parameters, result, status)
                    VALUES (@sid, @turnId, @callId, @tool, @params, @result, @status)";
                
                cmd.Parameters.AddWithValue("@sid", sessionId);
                cmd.Parameters.AddWithValue("@turnId", (object)turnId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@callId", (object)callId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@tool", toolName);
                cmd.Parameters.AddWithValue("@params", parameters ?? "");
                cmd.Parameters.AddWithValue("@result", result ?? "");
                cmd.Parameters.AddWithValue("@status", status);
                
                cmd.ExecuteNonQuery();
            }
        }

        public List<ToolCallRecord> GetToolCallHistory(string sessionId, int limit = 200)
        {
            var records = new List<ToolCallRecord>();
            using (var conn = GetConnection())
            using (var cmd = conn.CreateCommand())
            {
                if (limit > 0)
                {
                    cmd.CommandText = @"
                        SELECT id, session_id, turn_id, call_id, tool_name, parameters, result, status, timestamp
                        FROM tool_call_history
                        WHERE session_id = @sid
                        ORDER BY id ASC
                        LIMIT @limit";
                    cmd.Parameters.AddWithValue("@limit", limit);
                }
                else
                {
                    cmd.CommandText = @"
                        SELECT id, session_id, turn_id, call_id, tool_name, parameters, result, status, timestamp
                        FROM tool_call_history
                        WHERE session_id = @sid
                        ORDER BY id ASC";
                }

                cmd.Parameters.AddWithValue("@sid", sessionId);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        records.Add(new ToolCallRecord
                        {
                            Id = reader.GetInt32(0),
                            SessionId = reader.GetString(1),
                            TurnId = reader.IsDBNull(2) ? null : reader.GetString(2),
                            CallId = reader.IsDBNull(3) ? null : reader.GetString(3),
                            ToolName = reader.GetString(4),
                            Parameters = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                            Result = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                            Status = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                            Timestamp = DateTime.Parse(reader.GetString(8))
                        });
                    }
                }
            }

            return records;
        }

        #endregion

        #region Python执行记录

        public int SavePythonExecution(string sessionId, string codeId, string code, string output, string error, bool success, long executionTimeMs)
        {
            try
            {
                using (var conn = GetConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT INTO python_executions (session_id, code_id, code, output, error, success, execution_time_ms)
                        VALUES (@sid, @codeId, @code, @output, @error, @success, @time);
                        SELECT last_insert_rowid();";
                    
                    cmd.Parameters.AddWithValue("@sid", sessionId);
                    cmd.Parameters.AddWithValue("@codeId", codeId);
                    cmd.Parameters.AddWithValue("@code", code);
                    cmd.Parameters.AddWithValue("@output", output ?? "");
                    cmd.Parameters.AddWithValue("@error", error ?? "");
                    cmd.Parameters.AddWithValue("@success", success ? 1 : 0);
                    cmd.Parameters.AddWithValue("@time", (int)executionTimeMs);
                    
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SavePythonExecution失败: {ex.Message}");
                return -1;
            }
        }

        public List<PythonExecutionRecord> GetPythonExecutions(string sessionId)
        {
            var records = new List<PythonExecutionRecord>();
            try
            {
                using (var conn = GetConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT id, code_id, code, output, error, success, execution_time_ms, timestamp
                        FROM python_executions
                        WHERE session_id = @sid
                        ORDER BY timestamp ASC";
                    
                    cmd.Parameters.AddWithValue("@sid", sessionId);
                    
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            records.Add(new PythonExecutionRecord
                            {
                                Id = reader.GetInt32(0),
                                CodeId = reader.GetString(1),
                                Code = reader.GetString(2),
                                Output = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                Error = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                Success = reader.GetInt32(5) == 1,
                                ExecutionTimeMs = reader.GetInt32(6),
                                Timestamp = DateTime.Parse(reader.GetString(7))
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetPythonExecutions失败: {ex.Message}");
            }
            return records;
        }

        public PythonExecutionRecord GetLatestPythonExecution(string sessionId, string codeId)
        {
            try
            {
                using (var conn = GetConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT id, code_id, code, output, error, success, execution_time_ms, timestamp
                        FROM python_executions
                        WHERE session_id = @sid AND code_id = @codeId
                        ORDER BY id DESC
                        LIMIT 1";
                    
                    cmd.Parameters.AddWithValue("@sid", sessionId);
                    cmd.Parameters.AddWithValue("@codeId", codeId);
                    
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new PythonExecutionRecord
                            {
                                Id = reader.GetInt32(0),
                                CodeId = reader.GetString(1),
                                Code = reader.GetString(2),
                                Output = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                Error = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                Success = reader.GetInt32(5) == 1,
                                ExecutionTimeMs = reader.GetInt32(6),
                                Timestamp = DateTime.Parse(reader.GetString(7))
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetLatestPythonExecution失败: {ex.Message}");
            }
            return null;
        }

        #endregion

        #region 上下文总结管理

        /// <summary>
        /// 保存上下文总结
        /// </summary>
        public void SaveContextSummary(string sessionId, string summary, int summarizedUpToId, int messageCount, int estimatedTokens)
        {
            try
            {
                using (var conn = GetConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT INTO context_summaries (session_id, summary, summarized_up_to_id, message_count, estimated_tokens)
                        VALUES (@sid, @summary, @upToId, @msgCount, @tokens)";
                    
                    cmd.Parameters.AddWithValue("@sid", sessionId);
                    cmd.Parameters.AddWithValue("@summary", summary);
                    cmd.Parameters.AddWithValue("@upToId", summarizedUpToId);
                    cmd.Parameters.AddWithValue("@msgCount", messageCount);
                    cmd.Parameters.AddWithValue("@tokens", estimatedTokens);
                    
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveContextSummary失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取会话的最新上下文总结
        /// </summary>
        public ContextSummary GetLatestContextSummary(string sessionId)
        {
            try
            {
                using (var conn = GetConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT id, summary, summarized_up_to_id, message_count, estimated_tokens, timestamp
                        FROM context_summaries
                        WHERE session_id = @sid
                        ORDER BY id DESC
                        LIMIT 1";
                    
                    cmd.Parameters.AddWithValue("@sid", sessionId);
                    
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new ContextSummary
                            {
                                Id = reader.GetInt32(0),
                                SessionId = sessionId,
                                Summary = reader.GetString(1),
                                SummarizedUpToId = reader.GetInt32(2),
                                MessageCount = reader.GetInt32(3),
                                EstimatedTokens = reader.GetInt32(4),
                                Timestamp = DateTime.Parse(reader.GetString(5))
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetLatestContextSummary失败: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// 获取指定ID之后的对话历史
        /// </summary>
        public List<ConversationMessage> GetMessagesAfterId(string sessionId, int afterId, int limit = 50)
        {
            var messages = new List<ConversationMessage>();
            try
            {
                using (var conn = GetConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT id, role, content, thinking, images, timestamp, token_count, turn_id
                        FROM conversation_history 
                        WHERE session_id = @sid AND id > @afterId
                        ORDER BY id ASC 
                        LIMIT @limit";
                    
                    cmd.Parameters.AddWithValue("@sid", sessionId);
                    cmd.Parameters.AddWithValue("@afterId", afterId);
                    cmd.Parameters.AddWithValue("@limit", limit);
                    
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            messages.Add(new ConversationMessage
                            {
                                Id = reader.GetInt32(0),
                                Role = reader.GetString(1),
                                Content = reader.GetString(2),
                                Thinking = reader.IsDBNull(3) ? null : reader.GetString(3),
                                Images = reader.IsDBNull(4) ? null : reader.GetString(4),
                                Timestamp = DateTime.Parse(reader.GetString(5)),
                                TokenCount = reader.GetInt32(6),
                                TurnId = reader.IsDBNull(7) ? null : reader.GetString(7)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetMessagesAfterId失败: {ex.Message}");
            }
            return messages;
        }

        /// <summary>
        /// 获取会话的总消息数
        /// </summary>
        public int GetMessageCount(string sessionId)
        {
            try
            {
                using (var conn = GetConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM conversation_history WHERE session_id = @sid";
                    cmd.Parameters.AddWithValue("@sid", sessionId);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 获取会话中最新消息的ID
        /// </summary>
        public int GetLatestMessageId(string sessionId)
        {
            try
            {
                using (var conn = GetConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT MAX(id) FROM conversation_history WHERE session_id = @sid";
                    cmd.Parameters.AddWithValue("@sid", sessionId);
                    var result = cmd.ExecuteScalar();
                    return result == DBNull.Value ? 0 : Convert.ToInt32(result);
                }
            }
            catch
            {
                return 0;
            }
        }

        #endregion

        public void Dispose()
        {
            // SQLite使用独立连接，无需在此处关闭
            System.Diagnostics.Debug.WriteLine("DatabaseManager已释放");
        }
    }
}
