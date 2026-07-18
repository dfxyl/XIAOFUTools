using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Data.Sqlite;

namespace XIAOFUTools.Features.User.AIAssistant.Database.Infrastructure
{
    internal sealed class AIAssistantSettingsRepository
    {
        private const string PromptBeforeToolInChatKey = "prompt_before_tool_in_chat";
        private const string SensitiveModeKey = "sensitive_mode";
        private const string LegacyWebFetchKey = "enable_web_fetch";
        private const string ToolSettingsBaselineVersionKey = "tool_settings_baseline_version";
        private const string CurrentToolSettingsBaselineVersion = "2";

        private readonly AIAssistantConnectionFactory _connectionFactory;

        internal AIAssistantSettingsRepository(AIAssistantConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        internal SearchSettings GetSearchSettings()
        {
            var settings = new SearchSettings();
            try
            {
                using var connection = _connectionFactory.OpenConnection();
                EnsureSearchSettingsTable(connection);
                InitializeDefaultSearchSettings(connection);
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT key, value FROM search_settings";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    ApplySearchSetting(settings, reader.GetString(0), reader.GetString(1));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"获取搜索设置失败: {ex.Message}");
            }

            return settings;
        }

        internal void SaveSearchSettings(SearchSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            try
            {
                using var connection = _connectionFactory.OpenConnection();
                EnsureSearchSettingsTable(connection);
                var values = new Dictionary<string, string>
                {
                    ["tavily_api_key"] = settings.TavilyApiKey ?? string.Empty,
                    ["brave_api_key"] = settings.BraveApiKey ?? string.Empty,
                    ["enable_searxng"] = ToDatabaseBoolean(settings.EnableSearXNG),
                    ["searxng_instance_url"] = settings.SearXNGInstanceUrl ?? string.Empty,
                    ["enable_bing_scraper"] = ToDatabaseBoolean(settings.EnableBingScraper)
                };
                SaveValues(connection, "search_settings", values);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存搜索设置失败: {ex.Message}");
            }
        }

        internal ToolExecutionSettings GetToolExecutionSettings()
        {
            var settings = new ToolExecutionSettings();
            try
            {
                using var connection = _connectionFactory.OpenConnection();
                EnsureToolSettingsTable(connection);
                InitializeDefaultToolSettings(connection);
                var values = ReadValues(connection, "tool_settings");
                ApplyToolSettingsBaselineIfNeeded(connection, values);

                settings.PromptBeforeToolInChat = ReadBoolean(values, PromptBeforeToolInChatKey, true);
                settings.SensitiveMode = ReadBoolean(values, SensitiveModeKey, true);
                var legacyWebFetchEnabled = ReadBoolean(values, LegacyWebFetchKey, true);
                foreach (var tool in AIAssistantToolCatalog.Tools)
                {
                    var chatEnabled = ReadBoolean(
                        values,
                        BuildToolChatKey(tool.ToolName),
                        tool.DefaultChatEnabled);
                    var agentEnabled = ReadBoolean(
                        values,
                        BuildToolAgentKey(tool.ToolName),
                        tool.DefaultAgentEnabled);
                    if (string.Equals(tool.ToolName, "web_fetch", StringComparison.OrdinalIgnoreCase) &&
                        !legacyWebFetchEnabled)
                    {
                        chatEnabled = false;
                        agentEnabled = false;
                    }

                    settings.SetToolMode(tool.ToolName, chatEnabled, agentEnabled);
                }

                settings.EnsureDefaults();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"获取工具执行设置失败: {ex.Message}");
            }

            return settings;
        }

        internal void SaveToolExecutionSettings(ToolExecutionSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            try
            {
                settings.EnsureDefaults();
                using var connection = _connectionFactory.OpenConnection();
                EnsureToolSettingsTable(connection);
                var values = BuildToolSettingValues(settings);
                SaveValues(connection, "tool_settings", values);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存工具执行设置失败: {ex.Message}");
            }
        }

        internal void InitializeDefaultSearchSettings(SqliteConnection connection)
        {
            var defaults = new Dictionary<string, string>
            {
                ["tavily_api_key"] = string.Empty,
                ["brave_api_key"] = string.Empty,
                ["enable_searxng"] = "true",
                ["searxng_instance_url"] = string.Empty,
                ["enable_bing_scraper"] = "true"
            };
            InsertMissingValues(connection, "search_settings", defaults);
        }

        internal void InitializeDefaultToolSettings(SqliteConnection connection)
        {
            InsertMissingValues(connection, "tool_settings", BuildDefaultToolSettingValues());
        }

        private static Dictionary<string, string> BuildToolSettingValues(ToolExecutionSettings settings)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [PromptBeforeToolInChatKey] = ToDatabaseBoolean(settings.PromptBeforeToolInChat),
                [SensitiveModeKey] = ToDatabaseBoolean(settings.SensitiveMode),
                [ToolSettingsBaselineVersionKey] = CurrentToolSettingsBaselineVersion
            };
            foreach (var tool in AIAssistantToolCatalog.Tools)
            {
                var mode = settings.ToolModes.TryGetValue(tool.ToolName, out var configuredMode)
                    ? configuredMode
                    : new ToolModeSetting
                    {
                        ToolName = tool.ToolName,
                        ChatEnabled = tool.DefaultChatEnabled,
                        AgentEnabled = tool.DefaultAgentEnabled
                    };
                values[BuildToolChatKey(tool.ToolName)] = ToDatabaseBoolean(mode.ChatEnabled);
                values[BuildToolAgentKey(tool.ToolName)] = ToDatabaseBoolean(mode.AgentEnabled);
                if (string.Equals(tool.ToolName, "web_fetch", StringComparison.OrdinalIgnoreCase))
                {
                    values[LegacyWebFetchKey] = ToDatabaseBoolean(mode.ChatEnabled || mode.AgentEnabled);
                }
            }

            return values;
        }

        private static Dictionary<string, string> BuildDefaultToolSettingValues()
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [PromptBeforeToolInChatKey] = "true",
                [SensitiveModeKey] = "true",
                [ToolSettingsBaselineVersionKey] = CurrentToolSettingsBaselineVersion
            };
            foreach (var tool in AIAssistantToolCatalog.Tools)
            {
                values[BuildToolChatKey(tool.ToolName)] = ToDatabaseBoolean(tool.DefaultChatEnabled);
                values[BuildToolAgentKey(tool.ToolName)] = ToDatabaseBoolean(tool.DefaultAgentEnabled);
                if (string.Equals(tool.ToolName, "web_fetch", StringComparison.OrdinalIgnoreCase))
                {
                    values[LegacyWebFetchKey] = ToDatabaseBoolean(
                        tool.DefaultChatEnabled || tool.DefaultAgentEnabled);
                }
            }

            return values;
        }

        private static void ApplyToolSettingsBaselineIfNeeded(
            SqliteConnection connection,
            IDictionary<string, string> values)
        {
            if (values.TryGetValue(ToolSettingsBaselineVersionKey, out var version) &&
                string.Equals(version, CurrentToolSettingsBaselineVersion, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var baseline = BuildDefaultToolSettingValues();
            SaveValues(connection, "tool_settings", baseline);
            foreach (var item in baseline)
            {
                values[item.Key] = item.Value;
            }
        }

        private static void EnsureSearchSettingsTable(SqliteConnection connection)
        {
            EnsureSettingsTable(connection, "search_settings");
        }

        private static void EnsureToolSettingsTable(SqliteConnection connection)
        {
            EnsureSettingsTable(connection, "tool_settings");
        }

        private static void EnsureSettingsTable(SqliteConnection connection, string tableName)
        {
            using var command = connection.CreateCommand();
            command.CommandText = $@"
                CREATE TABLE IF NOT EXISTS {tableName} (
                    key TEXT PRIMARY KEY,
                    value TEXT NOT NULL DEFAULT ''
                )";
            command.ExecuteNonQuery();
        }

        private static Dictionary<string, string> ReadValues(SqliteConnection connection, string tableName)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT key, value FROM {tableName}";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                values[reader.GetString(0)] = reader.GetString(1);
            }

            return values;
        }

        private static void SaveValues(
            SqliteConnection connection,
            string tableName,
            IEnumerable<KeyValuePair<string, string>> values)
        {
            foreach (var item in values)
            {
                using var command = connection.CreateCommand();
                command.CommandText = $"INSERT OR REPLACE INTO {tableName} (key, value) VALUES (@key, @value)";
                command.Parameters.AddWithValue("@key", item.Key);
                command.Parameters.AddWithValue("@value", item.Value);
                command.ExecuteNonQuery();
            }
        }

        private static void InsertMissingValues(
            SqliteConnection connection,
            string tableName,
            IEnumerable<KeyValuePair<string, string>> values)
        {
            foreach (var item in values)
            {
                using var command = connection.CreateCommand();
                command.CommandText = $"INSERT OR IGNORE INTO {tableName} (key, value) VALUES (@key, @value)";
                command.Parameters.AddWithValue("@key", item.Key);
                command.Parameters.AddWithValue("@value", item.Value);
                command.ExecuteNonQuery();
            }
        }

        private static void ApplySearchSetting(SearchSettings settings, string key, string value)
        {
            switch (key)
            {
                case "tavily_api_key": settings.TavilyApiKey = value; break;
                case "brave_api_key": settings.BraveApiKey = value; break;
                case "enable_searxng": settings.EnableSearXNG = ParseBoolean(value); break;
                case "searxng_instance_url": settings.SearXNGInstanceUrl = value; break;
                case "enable_bing_scraper": settings.EnableBingScraper = ParseBoolean(value); break;
            }
        }

        private static bool ReadBoolean(
            IReadOnlyDictionary<string, string> values,
            string key,
            bool defaultValue)
        {
            return values.TryGetValue(key, out var value) ? ParseBoolean(value) : defaultValue;
        }

        private static bool ParseBoolean(string value) =>
            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

        private static string ToDatabaseBoolean(bool value) => value ? "true" : "false";
        private static string BuildToolChatKey(string toolName) => $"tool_{toolName}_chat_enabled";
        private static string BuildToolAgentKey(string toolName) => $"tool_{toolName}_agent_enabled";
    }
}
