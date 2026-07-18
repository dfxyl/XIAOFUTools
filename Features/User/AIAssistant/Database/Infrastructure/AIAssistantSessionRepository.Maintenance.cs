using Microsoft.Data.Sqlite;

namespace XIAOFUTools.Features.User.AIAssistant.Database.Infrastructure
{
    internal sealed partial class AIAssistantSessionRepository
    {
        private static readonly string[] SessionDataTables =
        {
            "conversation_history",
            "tool_call_history",
            "python_executions",
            "context_summaries"
        };

        public void ClearConversationHistory(string sessionId)
        {
            using (var conn = _connectionFactory.OpenConnection())
            using (var transaction = conn.BeginTransaction())
            {
                DeleteSessionData(conn, transaction, sessionId);
                transaction.Commit();
            }
        }

        public void UpdateSessionActivity(string sessionId)
        {
            using (var conn = _connectionFactory.OpenConnection())
            {
                UpdateSessionActivityInternal(conn, sessionId);
            }
        }

        public void UpdateSessionTitle(string sessionId, string title)
        {
            using (var conn = _connectionFactory.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "UPDATE sessions SET title = @title WHERE session_id = @sid";
                cmd.Parameters.AddWithValue("@title", title);
                cmd.Parameters.AddWithValue("@sid", sessionId);
                cmd.ExecuteNonQuery();
            }
        }

        public void DeleteSession(string sessionId)
        {
            using (var conn = _connectionFactory.OpenConnection())
            using (var transaction = conn.BeginTransaction())
            {
                DeleteSessionData(conn, transaction, sessionId);
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = "DELETE FROM sessions WHERE session_id = @sid";
                    cmd.Parameters.AddWithValue("@sid", sessionId);
                    cmd.ExecuteNonQuery();
                }

                transaction.Commit();
            }
        }

        private static void DeleteSessionData(
            SqliteConnection conn,
            SqliteTransaction transaction,
            string sessionId)
        {
            foreach (var tableName in SessionDataTables)
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = $"DELETE FROM {tableName} WHERE session_id = @sid";
                    cmd.Parameters.AddWithValue("@sid", sessionId);
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}
