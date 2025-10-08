using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Text;

namespace AnimeStudio
{
    [Flags]
    public enum LoggerEvent
    {
        None = 0,
        Verbose = 1,
        Debug = 2,
        Info = 4,
        Warning = 8,
        Error = 16,
        All = Verbose | Debug | Info | Warning | Error,
    }

    public interface ILogger
    {
        void Log(LoggerEvent loggerEvent, string message);
    }

    public sealed class DummyLogger : ILogger
    {
        public void Log(LoggerEvent loggerEvent, string message) { }
    }

    public sealed class ConsoleLogger : ILogger
    {
        public void Log(LoggerEvent loggerEvent, string message)
        {
            ConsoleColor color = ConsoleColor.White;
            bool bold = false;
            bool underline = false;

            switch (loggerEvent)
            {
                case LoggerEvent.Verbose: color = ConsoleColor.Gray; break;
                case LoggerEvent.Debug: color = ConsoleColor.Green; break;
                case LoggerEvent.Info: color = ConsoleColor.Cyan; bold = true; break;
                case LoggerEvent.Warning: color = ConsoleColor.Yellow; underline = true; break;
                case LoggerEvent.Error: color = ConsoleColor.Red; bold = true; break;
            }

            string style = "";
            string reset = "\u001b[0m";
            if (bold) style += "\u001b[1m";
            if (underline) style += "\u001b[4m";

            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            var caller = new StackTrace().GetFrame(2)?.GetMethod()?.ReflectedType?.Name;
            if (!string.IsNullOrEmpty(caller)) message = $"[{caller}] {message}";

            Console.ForegroundColor = color;
            Console.WriteLine($"{style}[{timestamp}] {loggerEvent.ToString().ToUpper()}: {message}{reset}");
            Console.ResetColor();

            //Console.WriteLine("[{0}] {1}", loggerEvent, message);
        }
    }

    public sealed class FileLogger : ILogger
    {
        private const string LogFileName = "log.txt";
        private const string PrevLogFileName = "log_prev.txt";
        private readonly object LockWriter = new object();
        private StreamWriter Writer;
        public FileLogger()
        {
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LogFileName);
            var prevLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PrevLogFileName);

            if (File.Exists(logPath))
            {
                File.Move(logPath, prevLogPath, true);
            }
            Writer = new StreamWriter(logPath, true) { AutoFlush = true };
        }
        ~FileLogger()
        {
            Dispose();
        }
        public void Log(LoggerEvent loggerEvent, string message)
        {
            lock (LockWriter)
            {
                Writer.WriteLine($"[{DateTime.Now}][{loggerEvent}] {message}");
            }
        }

        public void Dispose()
        {
            Writer?.Dispose();
        }
    }
}
