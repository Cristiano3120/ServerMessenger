using MongoDB.Driver;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;

namespace ServerMessenger
{
#pragma warning disable CS8618
    internal static class ChatsDatabse
    {
        private static string? _connectionString;
        private static IMongoCollection<Chat> _database;

        public static void Initialize()
        {
            _ = DisplayError.LogAsync("Initializing ChatsDatabase");
            var connectionStringFilepath = @"C:\Users\Crist\Desktop\txts\ChatsDatabaseConnection.txt";

            if (File.Exists(connectionStringFilepath))
            {
                if (ReadConnectionStringFromFile(connectionStringFilepath))
                {
                    Server.StopServer($"The File {connectionStringFilepath} is corrupted!");
                }
            }
            else
            {
                Server.StopServer($"The File {connectionStringFilepath} is missing!");
            }
            var client = new MongoClient(_connectionString);
            _database = client.GetDatabase("chats").GetCollection<Chat>("messages");
        }

        public static async Task<uint> GetChatID(List<UserAfterLogin> users)
        {
            _ = DisplayError.LogAsync("Getting the requestet chatID");
            var filter = Builders<Chat>.Filter.All(x => x.Participants, users);
            var chat = await _database.FindAsync(filter);
            return chat.FirstOrDefault().ChatId;
        }

        public static async Task CreateChatAsync(List<UserAfterLogin> participants)
        {
            _ = DisplayError.LogAsync("Creating chat");
            var newChat = new Chat
            {
                ChatId = await GenerateChatIdAsync(),
                Participants = participants,
                Messages = new List<Message>()
            };

            _database.InsertOne(newChat);
        }

        public static void DeleteChatAsync(uint chatID)
        {
            _ = DisplayError.LogAsync($"Deleting the chat: {chatID}");
            var filter = Builders<Chat>.Filter.Eq(x => x.ChatId, chatID);
            _database.FindOneAndDelete(filter);
        }

        public static async Task AddMessageToChat(uint chatID, Message message)
        {
            _ = DisplayError.LogAsync($"Adding a message to the chat {chatID}");

            var contentBytes = Encoding.UTF8.GetBytes(message.Content);
            var encryptedContentBytes = Security.EncryptDataAESDatabase(contentBytes, AccountInfoDatabase.Key, AccountInfoDatabase.Iv);
            var encryptedContent = Convert.ToBase64String(encryptedContentBytes);
            message.Content = encryptedContent;

            var filter = Builders<Chat>.Filter.Eq(x => x.ChatId, chatID);
            var update = Builders<Chat>.Update.Push(x => x.Messages, message);
            var result = await _database.UpdateOneAsync(filter, update);
            if (result.ModifiedCount == 1)
            {
                _ = DisplayError.LogAsync("Message successfully added to the chat!");
            }
            else
            {
                _ = DisplayError.LogAsync("Chat not found or message couldn´t be added.");
            }
        }

        public static async Task<List<Message>> GetMessagesFromChat(uint chatID)
        {
            _ = DisplayError.LogAsync($"Getting messages from the chat {chatID}");
            var chatVerlauf = await _database.FindAsync(x => x.ChatId == chatID);
            var messages = chatVerlauf.FirstOrDefault().Messages;

            if (messages != null)
            {
                return messages.Select(messageElement =>
                {
                    var contentBytes = Convert.FromBase64String(messageElement.Content);
                    var decryptedContent = Security.DecryptDataAES(contentBytes, AccountInfoDatabase.Key, AccountInfoDatabase.Iv);
                    messageElement.Content = decryptedContent;
                    return messageElement;
                }).ToList();
            }
            else
            {
                return [];
            }
        }

        private static async Task<uint> GenerateChatIdAsync()
        {
            uint id;
            IAsyncCursor<Chat> find;
            do
            {
                byte[] bytes = new byte[4];
                RandomNumberGenerator.Fill(bytes);
                id = BitConverter.ToUInt32(bytes);
                find = await _database.FindAsync(x => x.ChatId == id);
            }
            while (!await find.MoveNextAsync());

            _ = DisplayError.LogAsync($"Random generated ChatID: {id}");
            return id;
        }

        private static bool ReadConnectionStringFromFile(string pathToConnectionStringFile)
        {
            using var reader = new StreamReader(pathToConnectionStringFile);
            {
                _connectionString = reader.ReadLine()!;
            }
            return string.IsNullOrEmpty(_connectionString);
        }
    }
}
