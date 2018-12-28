// Copyright Lokad 2018 under MIT BCH.
using System;

namespace Terab.Lib
{
    /// <summary> Generic logger abstraction. </summary>
    public interface ILog
    {
        void Log(LogSeverity severity, string message);
    }

    public enum LogSeverity
    {
        Info = 0,
        Debug = 1,
        Warning = 2,
        Error = 3,
    }

    public class ConsoleLog : ILog
    {
        public void Log(LogSeverity severity, string message)
        {
            Console.WriteLine($"{DateTime.UtcNow:s}, {severity}: {message}");
        }
    }
}
