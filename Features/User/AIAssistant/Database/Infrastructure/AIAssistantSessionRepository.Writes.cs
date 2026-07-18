using System;
using Microsoft.Data.Sqlite;

namespace XIAOFUTools.Features.User.AIAssistant.Database.Infrastructure
{
    internal sealed partial class AIAssistantSessionRepository
    {
        public void SaveMessage(string sessionId, string role, string content, int tokenCount = 0, string thinking = null, string images = null, string turnId = null)
        {
            try
            {
                using (var conn = _connectionFactory.OpenConnection())
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

        private static void UpdateSessionActivityInternal(SqliteConnection conn, string sessionId)
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
            using (var conn = _connectionFactory.OpenConnection())
            {
                CreateSessionInternal(conn, sessionId, title);
            }
        }

        public void SaveToolCall(string sessionId, string toolName, string parameters, string result, string status, string turnId = null, string callId = null)
        {
            using (var conn = _connectionFactory.OpenConnection())
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

        public int SavePythonExecution(string sessionId, string codeId, string code, string output, string error, bool success, long executionTimeMs)
        {
            try
            {
                using (var conn = _connectionFactory.OpenConnection())
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

        /// <summary>
        /// 保存上下文总结
        /// </summary>
        public void SaveContextSummary(string sessionId, string summary, int summarizedUpToId, int messageCount, int estimatedTokens)
        {
            try
            {
                using (var conn = _connectionFactory.OpenConnection())
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
    }
}
