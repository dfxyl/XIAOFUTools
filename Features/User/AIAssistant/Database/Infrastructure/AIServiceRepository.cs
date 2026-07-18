using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace XIAOFUTools.Features.User.AIAssistant.Database.Infrastructure
{
    internal sealed class AIServiceRepository
    {
        private const string SelectColumns =
            "id, name, api_endpoint, model_name, api_key, is_default, max_tokens, " +
            "context_window_tokens, temperature, supports_vision";

        private readonly AIAssistantConnectionFactory _connectionFactory;

        internal AIServiceRepository(AIAssistantConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        internal AIServiceConfig GetDefault()
        {
            try
            {
                using var connection = _connectionFactory.OpenConnection();
                using var command = connection.CreateCommand();
                command.CommandText = $"SELECT {SelectColumns} FROM ai_services WHERE is_default = 1 LIMIT 1";
                using var reader = command.ExecuteReader();
                return reader.Read() ? ReadService(reader) : null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取默认服务失败: {ex.Message}");
                return null;
            }
        }

        internal List<AIServiceConfig> GetAll()
        {
            var services = new List<AIServiceConfig>();
            using var connection = _connectionFactory.OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM ai_services ORDER BY is_default DESC, name";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                services.Add(ReadService(reader));
            }

            return services;
        }

        internal void InitializeDefaults()
        {
            try
            {
                using var connection = _connectionFactory.OpenConnection();
                RemoveLegacyBuiltInServices(connection);
                var hasDefault = HasDefaultService(connection);

                foreach (var service in AIAssistantDefaultServicePolicy.InitialServices)
                {
                    var existingId = FindServiceId(connection, service.ModelName, service.ApiEndpoint);
                    var makeDefault = service.IsDefault && !hasDefault;
                    if (existingId.HasValue)
                    {
                        UpdateBuiltInService(connection, existingId.Value, service, makeDefault);
                    }
                    else
                    {
                        InsertInternal(connection, Clone(service, makeDefault));
                    }

                    if (makeDefault)
                    {
                        hasDefault = true;
                    }
                }

                EnsureSingleDefaultService(connection);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"应用AI服务默认策略失败: {ex.Message}");
            }
        }

        internal void Insert(AIServiceConfig service)
        {
            using var connection = _connectionFactory.OpenConnection();
            InsertInternal(connection, service);
        }

        internal void Update(AIServiceConfig service)
        {
            AIAssistantDefaultServicePolicy.ApplyKnownModelDefaults(service);
            using var connection = _connectionFactory.OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE ai_services
                SET name = @name, api_endpoint = @endpoint, model_name = @model, api_key = @key,
                    is_default = @default, max_tokens = @maxTokens, temperature = @temp,
                    context_window_tokens = @contextWindow, supports_vision = @vision,
                    updated_at = CURRENT_TIMESTAMP
                WHERE id = @id";
            AddServiceParameters(command, service);
            command.Parameters.AddWithValue("@id", service.Id);
            command.ExecuteNonQuery();
        }

        internal void Delete(int serviceId)
        {
            using var connection = _connectionFactory.OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM ai_services WHERE id = @id";
            command.Parameters.AddWithValue("@id", serviceId);
            command.ExecuteNonQuery();
        }

        internal void ResetToDefaults()
        {
            using (var connection = _connectionFactory.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "DELETE FROM ai_services";
                command.ExecuteNonQuery();
            }

            InitializeDefaults();
        }

        internal void SetDefault(int serviceId)
        {
            using var connection = _connectionFactory.OpenConnection();
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                "UPDATE ai_services SET is_default = CASE WHEN id = @id THEN 1 ELSE 0 END";
            command.Parameters.AddWithValue("@id", serviceId);
            command.ExecuteNonQuery();
            transaction.Commit();
        }

        private static AIServiceConfig ReadService(SqliteDataReader reader)
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
                ContextWindowTokens = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                Temperature = reader.GetDouble(8),
                SupportsVision = !reader.IsDBNull(9) && reader.GetInt32(9) == 1
            };
        }

        private static int? FindServiceId(
            SqliteConnection connection,
            string modelName,
            string endpoint)
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT id FROM ai_services WHERE model_name = @model AND api_endpoint = @endpoint LIMIT 1";
            command.Parameters.AddWithValue("@model", modelName);
            command.Parameters.AddWithValue("@endpoint", endpoint);
            var value = command.ExecuteScalar();
            return value == null || value == DBNull.Value ? null : Convert.ToInt32(value);
        }

        private static bool HasDefaultService(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM ai_services WHERE is_default = 1";
            return Convert.ToInt32(command.ExecuteScalar()) > 0;
        }

        private static void InsertInternal(SqliteConnection connection, AIServiceConfig service)
        {
            AIAssistantDefaultServicePolicy.ApplyKnownModelDefaults(service);
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO ai_services
                    (name, api_endpoint, model_name, api_key, is_default, max_tokens,
                     context_window_tokens, temperature, supports_vision)
                VALUES
                    (@name, @endpoint, @model, @key, @default, @maxTokens,
                     @contextWindow, @temp, @vision)";
            AddServiceParameters(command, service);
            command.ExecuteNonQuery();
        }

        private static void AddServiceParameters(SqliteCommand command, AIServiceConfig service)
        {
            command.Parameters.AddWithValue("@name", service.Name);
            command.Parameters.AddWithValue("@endpoint", service.ApiEndpoint);
            command.Parameters.AddWithValue("@model", service.ModelName);
            command.Parameters.AddWithValue("@key", service.ApiKey);
            command.Parameters.AddWithValue("@default", service.IsDefault ? 1 : 0);
            command.Parameters.AddWithValue("@maxTokens", service.MaxTokens);
            command.Parameters.AddWithValue("@contextWindow", service.ContextWindowTokens);
            command.Parameters.AddWithValue("@temp", service.Temperature);
            command.Parameters.AddWithValue("@vision", service.SupportsVision ? 1 : 0);
        }

        private static void UpdateBuiltInService(
            SqliteConnection connection,
            int id,
            AIServiceConfig service,
            bool makeDefault)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE ai_services
                SET name = @name, api_endpoint = @endpoint, model_name = @model,
                    api_key = @key, max_tokens = @maxTokens,
                    context_window_tokens = @contextWindow, temperature = @temp,
                    supports_vision = @vision,
                    is_default = CASE WHEN @makeDefault = 1 THEN 1 ELSE is_default END,
                    updated_at = CURRENT_TIMESTAMP
                WHERE id = @id";
            AddServiceParameters(command, service);
            command.Parameters.AddWithValue("@makeDefault", makeDefault ? 1 : 0);
            command.Parameters.AddWithValue("@id", id);
            command.ExecuteNonQuery();
        }

        private static void RemoveLegacyBuiltInServices(SqliteConnection connection)
        {
            var legacyIds = new List<int>();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = $"SELECT {SelectColumns} FROM ai_services";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var service = ReadService(reader);
                    if (AIAssistantDefaultServicePolicy.IsLegacyBuiltInService(service))
                    {
                        legacyIds.Add(service.Id);
                    }
                }
            }

            foreach (var id in legacyIds)
            {
                using var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM ai_services WHERE id = @id";
                command.Parameters.AddWithValue("@id", id);
                command.ExecuteNonQuery();
            }
        }

        private static void EnsureSingleDefaultService(SqliteConnection connection)
        {
            var firstServiceId = 0;
            var defaultServiceId = 0;
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT id, is_default FROM ai_services ORDER BY is_default DESC, name";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var id = reader.GetInt32(0);
                    if (firstServiceId == 0)
                    {
                        firstServiceId = id;
                    }

                    if (defaultServiceId == 0 && reader.GetInt32(1) == 1)
                    {
                        defaultServiceId = id;
                    }
                }
            }

            var selectedId = defaultServiceId == 0 ? firstServiceId : defaultServiceId;
            if (selectedId == 0)
            {
                return;
            }

            using var resetCommand = connection.CreateCommand();
            resetCommand.CommandText =
                "UPDATE ai_services SET is_default = CASE WHEN id = @id THEN 1 ELSE 0 END";
            resetCommand.Parameters.AddWithValue("@id", selectedId);
            resetCommand.ExecuteNonQuery();
        }

        private static AIServiceConfig Clone(AIServiceConfig service, bool isDefault)
        {
            return new AIServiceConfig
            {
                Name = service.Name,
                ApiEndpoint = service.ApiEndpoint,
                ModelName = service.ModelName,
                ApiKey = service.ApiKey,
                IsDefault = isDefault,
                MaxTokens = service.MaxTokens,
                ContextWindowTokens = service.ContextWindowTokens,
                Temperature = service.Temperature,
                SupportsVision = service.SupportsVision
            };
        }
    }
}
