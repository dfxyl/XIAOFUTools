using System;

namespace XIAOFUTools.Shared.Diagnostics
{
    internal enum AppLogLevel
    {
        Information,
        Warning,
        Error
    }

    internal interface IAppLogger
    {
        event EventHandler<AppLogEntry> EntryWritten;

        void Write(AppLogLevel level, string message, Exception exception = null);
    }

    internal sealed record AppLogEntry(DateTimeOffset Timestamp, AppLogLevel Level, string Message, Exception Exception);
}
