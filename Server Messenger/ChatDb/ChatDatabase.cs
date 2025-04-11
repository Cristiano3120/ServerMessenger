using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;

namespace Server_Messenger.ChatDb
{
    public class ChatDatabase
    {
        private readonly IMongoCollection<Chat> _chats;

        public ChatDatabase()
        {
            MongoClient client = new(ReadConnString());
            IMongoDatabase database = client.GetDatabase("Chats");
            _chats = database.GetCollection<Chat>("Chats");
        }

        private static string ReadConnString()
            => Server.Config.GetProperty("ConnectionStrings").GetProperty("ChatDatabase").GetString()!;


        public async Task<Chat[]> GetChatsAsync(long id)
        {
            FilterDefinition<Chat> filter = Builders<Chat>.Filter.AnyEq(x => x.Members, id);
            Chat[] chats = [.. await _chats.Find(filter).ToListAsync()];
            for (int i = 0; i < chats.Length; i++)
            {
                const byte maxMessagesOnLogOn = 30;
                chats[i].Messages = [.. chats[i].Messages.TakeLast(maxMessagesOnLogOn)];
                chats[i].Messages = await Security.DecryptAesDatabaseAsync(chats[i].Messages);
            }

            return chats;
        }

        public async Task AddMessageAsync(Message message, long receiverId)
        {
            message = await Security.EncryptAesDatabaseAsync(message);
            string chatID = CombineIds([message.SenderId, receiverId]);
            Chat chat = await _chats.Find(x => x.ChatID == chatID).FirstOrDefaultAsync();

            if (chat is not null)
            {
                chat.Messages.Add(message);
                FilterDefinition<Chat> filter = Builders<Chat>.Filter.Eq(x => x.ChatID, chatID);
                await _chats.ReplaceOneAsync(filter, chat);
            }
            else
            {
                chat = new Chat()
                {
                    Members = [message.SenderId, receiverId],
                    Messages = [message],
                    ChatID = chatID
                };

                await _chats.InsertOneAsync(chat);
            }
        }

        private static string CombineIds(long[] ids)
        {
            Array.Sort(ids);
            return string.Join("-", ids);
        }
    }
}
