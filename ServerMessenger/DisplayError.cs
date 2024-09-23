using System.Net.Sockets;

namespace ServerMessenger
{
    internal static class DisplayError
    {
        private static readonly string _loggingFilePath = @"C:\Users\Crist\Desktop\txts\ServerLogger.txt";
        private static readonly Queue<(string, string)> _loggingList = new();

        public static void Initialize()
        {
            if (File.Exists(_loggingFilePath))
            {
                File.WriteAllText(_loggingFilePath, "");
            }
        }

        public static void DisplayBasicErrorInfos(Exception ex, string className, string methodName)
        {
            Log($"Error({className}.{methodName}): {ex.Message}");
        }

        public static void ObjectDisposedException(ObjectDisposedException ex, string className, string methodName)
        {
            DisplayBasicErrorInfos(ex, className, methodName);
            Log($"Error: The object {ex.ObjectName} was disposed");
        }

        public static void ArgumentNullException(ArgumentNullException ex, string className, string methodName)
        {
            DisplayBasicErrorInfos(ex, className, methodName);
            Log($"Error(Var that was null): {ex.ParamName}");
        }

        public static void SocketException(SocketException ex, string className, string methodName)
        {
            DisplayBasicErrorInfos(ex, className, methodName);
            Log($"Error(ErrorCode, SocketErrorCode): {ex.ErrorCode}, {ex.SocketErrorCode}");
        }

        public static async Task Log(string log)
        {
            try
            {
                Console.WriteLine(log);
                _loggingList.Enqueue((log, $"[{DateTime.UtcNow.ToString("HH:mm:ss")}]"));
                foreach (var (content, timestamp) in _loggingList)
                {
                    using (var writer = new StreamWriter(_loggingFilePath, true))
                    {
                        await writer.WriteLineAsync($"{timestamp} {content}");
                    }
                }
            }
            catch (Exception)
            {
                _loggingList.Enqueue((log, $"[{DateTime.UtcNow.ToString("HH:mm:ss")}]"));
            }
            
        }
    }
}
