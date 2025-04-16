using System.Text.Json.Serialization;

namespace Server_Messenger
{
    public sealed class Relationship
    {
        [JsonConverter(typeof(JsonConverters.Base64ByteArrayJsonConverter))]
        public byte[] ProfilePicture { get; set; } = [];
        public RelationshipState RelationshipState { get; set; }
        public string Biography { get; set; } = "";
        public string Username { get; set; } = "";
        public string Hashtag { get; set; } = "";
        public long Id { get; init; } = -1;
    }
}
