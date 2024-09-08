using System.Net.Sockets;

namespace ServerMessenger
{
    internal static class DisplayError
    {
        public static void DisplayBasicErrorInfos(Exception ex, string className, string methodName)
        {
            Console.WriteLine($"Error({className}.{methodName}): {ex.Message}");
        }

        public static void ObjectDisposedException(ObjectDisposedException ex, string className, string methodName)
        {
            DisplayBasicErrorInfos(ex, className, methodName);
            Console.WriteLine($"Error: The object {ex.ObjectName} was disposed");
        }

        public static void SocketException(SocketException ex, string className, string methodName)
        {
            DisplayBasicErrorInfos(ex, className, methodName);
            Console.WriteLine($"Error(ErrorCode, SocketErrorCode): {ex.ErrorCode}, {ex.SocketErrorCode}");
        }
    }
}
