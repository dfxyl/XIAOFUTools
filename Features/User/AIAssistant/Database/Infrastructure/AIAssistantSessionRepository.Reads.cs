using System;
using System.Collections.Generic;

namespace XIAOFUTools.Features.User.AIAssistant.Database.Infrastructure
{
    internal sealed partial class AIAssistantSessionRepository
    {
        private readonly AIAssistantConnectionFactory _connectionFactory;

        internal AIAssistantSessionRepository(AIAssistantConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public List<PythonExecutionRecord> GetPythonExecutions(string sessionId)
        {
            var records = new List<PythonExecutionRecord>();
            try
            {
            using (var conn = _connectionFactory.OpenConnection())
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
            using (var conn = _connectionFactory.OpenConnection())
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

        /// <summary>
        /// 获取会话的最新上下文总结
        /// </summary>
        public ContextSummary GetLatestContextSummary(string sessionId)
        {
            try
            {
            using (var conn = _connectionFactory.OpenConnection())
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
            using (var conn = _connectionFactory.OpenConnection())
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
            using (var conn = _connectionFactory.OpenConnection())
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
            using (var conn = _connectionFactory.OpenConnection())
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

        public List<ConversationMessage> GetConversationHistory(string sessionId, int limit = 50)
        {
            var messages = new List<ConversationMessage>();
            using (var conn = _connectionFactory.OpenConnection())
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
                        messages.Add(ReadConversationMessage(reader));
                    }
                }
            }

            messages.Reverse();
            return messages;
        }

        public List<Session> GetRecentSessions(int limit = 20)
        {
            var sessions = new List<Session>();
            using (var conn = _connectionFactory.OpenConnection())
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

        public List<ToolCallRecord> GetToolCallHistory(string sessionId, int limit = 200)
        {
            var records = new List<ToolCallRecord>();
            using (var conn = _connectionFactory.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = limit > 0
                    ? @"
                        SELECT id, session_id, turn_id, call_id, tool_name, parameters, result, status, timestamp
                        FROM tool_call_history
                        WHERE session_id = @sid
                        ORDER BY id ASC
                        LIMIT @limit"
                    : @"
                        SELECT id, session_id, turn_id, call_id, tool_name, parameters, result, status, timestamp
                        FROM tool_call_history
                        WHERE session_id = @sid
                        ORDER BY id ASC";
                cmd.Parameters.AddWithValue("@sid", sessionId);
                if (limit > 0)
                {
                    cmd.Parameters.AddWithValue("@limit", limit);
                }

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

        private static ConversationMessage ReadConversationMessage(Microsoft.Data.Sqlite.SqliteDataReader reader)
        {
            return new ConversationMessage
            {
                Id = reader.GetInt32(0),
                Role = reader.GetString(1),
                Content = reader.GetString(2),
                Thinking = reader.IsDBNull(3) ? null : reader.GetString(3),
                Images = reader.IsDBNull(4) ? null : reader.GetString(4),
                Timestamp = DateTime.Parse(reader.GetString(5)),
                TokenCount = reader.GetInt32(6),
                TurnId = reader.IsDBNull(7) ? null : reader.GetString(7)
            };
        }
    }
}
