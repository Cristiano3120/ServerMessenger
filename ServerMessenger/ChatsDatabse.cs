namespace ServerMessenger
{
    internal static class ChatsDatabse
    {
        private static string? _connectionString = "";

        //MUSST HIERFÜR AUCH ENCRYPTION MACHEN GUCK ACCOUNTINFODATABASE
        public static void Initialize()
        {
            _ = DisplayError.LogAsync("Initializing ChatsDatabase");
            var pathToConnectionString = "";
            if (File.Exists(pathToConnectionString))
            {
                if (ReadConnectionStringFromFile(pathToConnectionString))
                {
                    Server.StopServer($"The File {pathToConnectionString} is corrupted!");
                }
            }
            else
            {
                _ = DisplayError.LogAsync($"The File {pathToConnectionString} is missing!");
            }
        }

        public static bool ReadConnectionStringFromFile(string pathToConnectionStringFile)
        {
            using var reader = new StreamReader(pathToConnectionStringFile);
            {
                _connectionString = reader.ReadLine()!;
            }
            return string.IsNullOrEmpty(_connectionString);
        }
    }
}
