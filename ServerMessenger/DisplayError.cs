using System.Net.Sockets;

namespace ServerMessenger
{
    internal static class DisplayError
    {
        private static readonly string _loggingFilePath = @"C:\Users\Crist\Desktop\txts\ServerLogger.txt";
        private static readonly Queue<(string, string)> _loggingList = new();
        private static readonly object _lock = new();

        public static void Initialize()
        {
            Console.WriteLine("Initializing DisplayError");
            if (File.Exists(_loggingFilePath))
            {
                File.WriteAllText(_loggingFilePath, "");
            }
        }

        public static void DisplayBasicErrorInfos(Exception ex, string className, string methodName)
        {
            _ = LogAsync($"Error({className}.{methodName}): {ex.Message}");
        }

        public static void ObjectDisposedException(ObjectDisposedException ex, string className, string methodName)
        {
            DisplayBasicErrorInfos(ex, className, methodName);
            _ = LogAsync($"Error: The object {ex.ObjectName} was disposed");
        }

        public static void ArgumentNullException(ArgumentNullException ex, string className, string methodName)
        {
            DisplayBasicErrorInfos(ex, className, methodName);
            _ = LogAsync($"Error(Var that was null): {ex.ParamName}");
        }

        public static void SocketException(SocketException ex, string className, string methodName)
        {
            DisplayBasicErrorInfos(ex, className, methodName);
            _ = LogAsync($"Error(ErrorCode, SocketErrorCode): {ex.ErrorCode}, {ex.SocketErrorCode}");
        }

        public static async Task LogAsync(string log)
        {
            try
            {
                Console.WriteLine(log);
                lock (_lock)
                {
                    _loggingList.Enqueue((log, $"[{DateTime.UtcNow:HH:mm:ss}]"));
                }

                List<(string content, string timestamp)> logsToWrite;
                lock (_lock)
                {
                    logsToWrite = _loggingList.ToList();
                    _loggingList.Clear();
                }

                using var writer = new StreamWriter(_loggingFilePath, true);
                {
                    foreach (var (content, timestamp) in logsToWrite)
                    {
                        await writer.WriteLineAsync($"{timestamp} {content}");
                    }
                }
            }
            catch (Exception)
            {
                lock (_lock)
                {
                    _loggingList.Enqueue((log, $"[{DateTime.UtcNow:HH:mm:ss}]"));
                }
            }
        }
    }
}
