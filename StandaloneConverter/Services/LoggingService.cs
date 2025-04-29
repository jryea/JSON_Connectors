using System;

namespace StandaloneConverter.Services
{
    public class LoggingService
    {
        public event EventHandler<string> LogMessageAdded;

        public void Log(string message)
        {
            string timestampedMessage = $"[{DateTime.Now:HH:mm:ss}] {message}";
            Console.WriteLine(timestampedMessage); // Also write to console for debugging
            LogMessageAdded?.Invoke(this, timestampedMessage);
        }
    }
}