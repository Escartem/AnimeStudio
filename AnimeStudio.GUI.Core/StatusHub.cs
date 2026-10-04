using System;
using System.Collections.Concurrent;
using System.Threading;

namespace AnimeStudio.GUI.Core
{
    public readonly record struct LogEntry(DateTime Time, LoggerEvent Level, string Message);

    // Workers write the latest status/progress here without blocking. The UI polls on a timer,
    // so a million progress reports cost a million field writes, not a million dispatcher calls.
    public sealed class StatusHub : IProgress<int>
    {
        private const int MaxLogEntries = 20000;

        private string status = string.Empty;
        private int progress;
        private int version;
        private int logCount;

        public ConcurrentQueue<LogEntry> Log { get; } = new();
        public ConcurrentQueue<string> PendingErrors { get; } = new();
        public bool ShowErrorMessages { get; set; } = true;

        public string Status => Volatile.Read(ref status);
        public int ProgressValue => Volatile.Read(ref progress);
        public int Version => Volatile.Read(ref version);

        public void SetStatus(string text)
        {
            Volatile.Write(ref status, text ?? string.Empty);
            Interlocked.Increment(ref version);
        }

        public void Report(int value)
        {
            Volatile.Write(ref progress, Math.Clamp(value, 0, 100));
            Interlocked.Increment(ref version);
        }

        public void Append(LoggerEvent level, string message)
        {
            Log.Enqueue(new LogEntry(DateTime.Now, level, message));
            if (Interlocked.Increment(ref logCount) > MaxLogEntries && Log.TryDequeue(out _))
                Interlocked.Decrement(ref logCount);
        }

        public bool TryDequeueLog(out LogEntry entry)
        {
            if (Log.TryDequeue(out entry))
            {
                Interlocked.Decrement(ref logCount);
                return true;
            }
            return false;
        }
    }

    public sealed class HubLogger : ILogger
    {
        private readonly StatusHub hub;

        public HubLogger(StatusHub hub) => this.hub = hub;

        public void Log(LoggerEvent loggerEvent, string message)
        {
            hub.Append(loggerEvent, message);
            if (loggerEvent == LoggerEvent.Error)
            {
                if (hub.ShowErrorMessages)
                    hub.PendingErrors.Enqueue(message);
            }
            else
            {
                hub.SetStatus(message);
            }
        }
    }
}
