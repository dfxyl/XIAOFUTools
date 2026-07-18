using System;
using System.Diagnostics;

namespace XIAOFUTools.Shared.Diagnostics
{
    internal sealed class AppLogger : IAppLogger
    {
        public event EventHandler<AppLogEntry> EntryWritten;

        public void Write(AppLogLevel level, string message, Exception exception = null)
        {
            var entry = new AppLogEntry(DateTimeOffset.Now, level, message ?? string.Empty, exception);
            Debug.WriteLine($"[{entry.Timestamp:HH:mm:ss}] {level}: {entry.Message}{(exception == null ? string.Empty : $" - {exception}")}");
            EntryWritten?.Invoke(this, entry);
        }
    }
}
