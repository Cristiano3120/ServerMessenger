using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace Server_Messenger
{
    public readonly struct Message() 
    {
        [BsonRepresentation(BsonType.String)]
        public Guid Guid { get; init; } = Guid.NewGuid();
        public long SenderId { get; init; }
        public DateTime DateTime { get; init; }
        public string Content { get; init; } = "";
    }
}
