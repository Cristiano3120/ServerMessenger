using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Npgsql;

namespace Server_Messenger
{
    public static partial class Logger
    {
        [GeneratedRegex("(\"(?:profilePicture|newProfilePicture)\": \")[^\"]*(\")")]
        private static partial Regex FilterProfilPicRegex();

        #region LogInformation

        public static void LogInformation(ConsoleColor color, bool makeLineAfter = true, params string[] logs)
        {
            Console.ForegroundColor = color;
            Log(color, makeLineAfter, logs);
        }

        public static void LogInformation(params string[] logs)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Log(ConsoleColor.White, true, logs);
        }

        #endregion

        /// <summary>
        /// Outputs the wanted logs as yellow text in the console
        /// </summary>
        public static void LogWarning(params string[] logs)
        {
            Log(ConsoleColor.Yellow, true, logs);
        }

        /// <summary>
        /// Logs the error in red in the console with the error message and the file, method, line and column where the error occured
        /// </summary>
        /// <typeparam name="T">Has to be of type <c>EXCEPTION</c>, <c>UnobservedTaskExceptionEventArgs</c>, <c>NpgsqlException</c> <c>string</c> </typeparam>
        /// <exception cref="ArgumentException"></exception>
        public static void LogError<T>(T exception)
        {
            if (exception is string str)
            {
                Log(ConsoleColor.Red, true, str);
                return;
            }

            if (exception is UnobservedTaskExceptionEventArgs unobservedEx)
            {
                foreach (Exception innerEx in unobservedEx.Exception.Flatten().InnerExceptions)
                {
                    LogError(innerEx);
                }
                return;
            }

            if (exception is NpgsqlException npgsqlException)
            {
                Log(ConsoleColor.Red, true, $"ERROR(SQLSTATE): {npgsqlException.SqlState}");
            }

            Exception ex = exception as Exception
                ?? throw new ArgumentException($"Type {typeof(T).Name} must be of type EXCEPTION, UnobservedTaskExceptionEventArgs, NpgsqlExceptin or string.");

            StackTrace stackTrace = new(ex, true);
            StackFrame? stackFrame = null;
            foreach (StackFrame item in stackTrace.GetFrames())
            {
                //Looking for the frame contains the infos about the error
                if (item.GetMethod()?.Name != null && item.GetFileName() != null)
                {
                    stackFrame = item;
                    break;
                }
            }

            if (stackFrame != null)
            {
                var methodName = stackFrame?.GetMethod()?.Name + "()";
                var filename = stackFrame?.GetFileName() ?? "missing filename";
                var lineNum = stackFrame?.GetFileLineNumber();
                var columnNum = stackFrame?.GetFileColumnNumber();

                var index = filename.LastIndexOf('\\') + 1;
                filename = filename[index..];

                var errorInfos = $"ERROR in file {filename}, in {methodName}, at line: {lineNum}, at column: {columnNum}";
                Log(ConsoleColor.Red, true, errorInfos);
            }

            Log(ConsoleColor.Red, true, $"ERROR: {ex.Message}");

            if (ex.InnerException != null)
                LogError(ex.InnerException);
        }

        public static void LogPayload(ConsoleColor color, string payload, string prefix)
        {
            Console.ForegroundColor = color;

            string message = FilterProfilPicRegex().Replace(payload, "$1[Image]$2");
            JsonNode jsonNode = JsonNode.Parse(message)!;

            string opCode = nameof(OpCode).ToCamelCase();
            jsonNode[opCode] = Enum.Parse<OpCode>(jsonNode[opCode]!.ToString()).ToString();
            
            string settingsUpdate = nameof(SettingsUpdate).ToCamelCase();
            if (message.Contains(settingsUpdate))
            {
                jsonNode[settingsUpdate] = Enum.Parse<SettingsUpdate>(jsonNode[settingsUpdate]!.ToString()).ToString();
            }

            string usernameUpdateResult = nameof(UsernameUpdateResult).ToCamelCase();
            if (message.Contains(usernameUpdateResult))
            {
                jsonNode[usernameUpdateResult] = Enum.Parse<UsernameUpdateResult>(jsonNode[usernameUpdateResult]!.ToString()).ToString();
            }

            Console.WriteLine($"[{DateTime.Now:HH: dd: ss}]: {prefix} {jsonNode}");
            Console.WriteLine("");

        }

        /// <summary>
        /// The method that filters the logs and writes them into the Console
        /// </summary>
        private static void Log(ConsoleColor color, bool makeLineAfter, params string[] logs)
        {
            Console.ForegroundColor = color;
            for (int i = 0; i < logs.Length; i++)
            {
                string message = FilterProfilPicRegex().Replace(logs[i], "$1[Image]$2");
                Console.WriteLine($"[{DateTime.Now:HH: mm: ss}]: {message}");
            }

            if (makeLineAfter)
            {
                Console.WriteLine("");
            }  
        }
    }
}
