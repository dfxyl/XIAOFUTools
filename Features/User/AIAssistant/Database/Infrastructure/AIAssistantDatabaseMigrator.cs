using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Data.Sqlite;

namespace XIAOFUTools.Features.User.AIAssistant.Database.Infrastructure
{
    internal sealed class AIAssistantDatabaseMigrator
    {
        private readonly AIAssistantConnectionFactory _connectionFactory;
        private readonly AIAssistantSettingsRepository _settingsRepository;
        private readonly Action _initializeDefaultServices;

        internal AIAssistantDatabaseMigrator(
            AIAssistantConnectionFactory connectionFactory,
            AIAssistantSettingsRepository settingsRepository,
            Action initializeDefaultServices)
        {
            _connectionFactory = connectionFactory;
            _settingsRepository = settingsRepository;
            _initializeDefaultServices = initializeDefaultServices;
        }

        internal void Migrate()
        {
            try
            {
                using var connection = _connectionFactory.OpenConnection();
                foreach (var statement in CreateTableStatements)
                {
                    Execute(connection, statement);
                }

                EnsureColumnExists(connection, "ai_services", "context_window_tokens", "INTEGER DEFAULT 0");
                EnsureColumnExists(connection, "conversation_history", "turn_id", "TEXT");
                EnsureColumnExists(connection, "tool_call_history", "turn_id", "TEXT");
                EnsureColumnExists(connection, "tool_call_history", "call_id", "TEXT");

                Execute(connection, @"
                    CREATE INDEX IF NOT EXISTS idx_conversation_session ON conversation_history(session_id);
                    CREATE INDEX IF NOT EXISTS idx_conversation_turn ON conversation_history(turn_id);
                    CREATE INDEX IF NOT EXISTS idx_tool_call_session ON tool_call_history(session_id);
                    CREATE INDEX IF NOT EXISTS idx_tool_call_turn ON tool_call_history(turn_id);
                    CREATE INDEX IF NOT EXISTS idx_python_exec_session ON python_executions(session_id);
                    CREATE INDEX IF NOT EXISTS idx_summary_session ON context_summaries(session_id);");

                _settingsRepository.InitializeDefaultSearchSettings(connection);
                _settingsRepository.InitializeDefaultToolSettings(connection);
                _initializeDefaultServices();
                Debug.WriteLine("SQLite数据库迁移完成");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"数据库迁移失败: {ex.Message}");
                Debug.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                throw;
            }
        }

        private static readonly IReadOnlyList<string> CreateTableStatements = new[]
        {
            @"CREATE TABLE IF NOT EXISTS ai_services (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                api_endpoint TEXT NOT NULL,
                model_name TEXT NOT NULL,
                api_key TEXT NOT NULL,
                is_default INTEGER DEFAULT 0,
                max_tokens INTEGER DEFAULT 4096,
                context_window_tokens INTEGER DEFAULT 0,
                temperature REAL DEFAULT 0.7,
                supports_vision INTEGER DEFAULT 0,
                created_at TEXT DEFAULT CURRENT_TIMESTAMP,
                updated_at TEXT DEFAULT CURRENT_TIMESTAMP
            )",
            @"CREATE TABLE IF NOT EXISTS conversation_history (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id TEXT NOT NULL,
                role TEXT NOT NULL,
                content TEXT NOT NULL,
                thinking TEXT,
                images TEXT,
                turn_id TEXT,
                timestamp TEXT DEFAULT CURRENT_TIMESTAMP,
                token_count INTEGER DEFAULT 0
            )",
            @"CREATE TABLE IF NOT EXISTS tool_call_history (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id TEXT NOT NULL,
                turn_id TEXT,
                call_id TEXT,
                tool_name TEXT NOT NULL,
                parameters TEXT,
                result TEXT,
                status TEXT NOT NULL,
                timestamp TEXT DEFAULT CURRENT_TIMESTAMP
            )",
            @"CREATE TABLE IF NOT EXISTS sessions (
                session_id TEXT PRIMARY KEY,
                title TEXT,
                created_at TEXT DEFAULT CURRENT_TIMESTAMP,
                last_activity TEXT DEFAULT CURRENT_TIMESTAMP
            )",
            @"CREATE TABLE IF NOT EXISTS python_executions (
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
            )",
            @"CREATE TABLE IF NOT EXISTS context_summaries (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id TEXT NOT NULL,
                summary TEXT NOT NULL,
                summarized_up_to_id INTEGER NOT NULL,
                message_count INTEGER DEFAULT 0,
                estimated_tokens INTEGER DEFAULT 0,
                timestamp TEXT DEFAULT CURRENT_TIMESTAMP
            )",
            @"CREATE TABLE IF NOT EXISTS search_settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL DEFAULT ''
            )",
            @"CREATE TABLE IF NOT EXISTS tool_settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL DEFAULT ''
            )"
        };

        private static void EnsureColumnExists(
            SqliteConnection connection,
            string tableName,
            string columnName,
            string columnType)
        {
            if (ColumnExists(connection, tableName, columnName))
            {
                return;
            }

            Execute(connection, $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnType}");
        }

        private static bool ColumnExists(
            SqliteConnection connection,
            string tableName,
            string columnName)
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info({tableName})";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void Execute(SqliteConnection connection, string commandText)
        {
            using var command = connection.CreateCommand();
            command.CommandText = commandText;
            command.ExecuteNonQuery();
        }
    }
}
