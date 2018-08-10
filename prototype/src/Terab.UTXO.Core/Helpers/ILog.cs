namespace Terab.UTXO.Core.Helpers
{
    // TODO: [vermorel] merely a placeholder logger at this point.

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
}
