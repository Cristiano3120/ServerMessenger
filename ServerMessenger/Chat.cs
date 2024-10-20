using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServerMessenger
{

    public sealed class Chat
    {
        [BsonId]
        public ObjectId Id { get; set; }
        public required uint ChatId { get; set; } 
        public required List<UserAfterLogin> Participants { get; set; }
        public required List<Message> Messages { get; set; }
    }
}
