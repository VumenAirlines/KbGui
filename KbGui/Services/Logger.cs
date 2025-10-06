using System;
using System.Threading;
using KBSoftware.Services;

namespace KbGui.Services;


public class Logger(LogLevel minLevel = LogLevel.Info) : ILogger
{
    private readonly Lock _lock = new();

    public void Log(LogLevel level, string message, Exception? ex = null)
    {
        if (level < minLevel) return;

        lock (_lock)
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = level switch
            {
                LogLevel.Trace => ConsoleColor.Gray,
                LogLevel.Debug => ConsoleColor.Cyan,
                LogLevel.Info  => ConsoleColor.Green,
                LogLevel.Warn  => ConsoleColor.Yellow,
                LogLevel.Error => ConsoleColor.Red,
                LogLevel.Fatal => ConsoleColor.Magenta,
                _ => ConsoleColor.White
            };var color = level switch
            {
                LogLevel.Trace => "gray",
                LogLevel.Debug => "cyan",
                LogLevel.Info  => "green",
                LogLevel.Warn  => "yellow",
                LogLevel.Error => "red",
                LogLevel.Fatal => "magenta",
                _ => "white"
            };

            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss}] [{level}] {message} {ex?.Message}"
            );
            if (ex != null)
            {
                Console.WriteLine(ex.StackTrace);
            }

            Console.ForegroundColor = originalColor;
        }
    }
}



