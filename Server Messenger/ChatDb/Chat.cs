using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace Server_Messenger.ChatDb
{
    public class Chat
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; private set; } = ObjectId.GenerateNewId().ToString();
        public string ChatID { get; set; } = "";
        public required List<long> Members { get; set; }
        public List<Message> Messages { get; set; } = [];
    }
}
