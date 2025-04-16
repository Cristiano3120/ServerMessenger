using System.Text.Json.Serialization;

namespace Server_Messenger
{
    internal sealed class User
    {
        [JsonConverter(typeof(JsonConverters.Base64ByteArrayJsonConverter))]
        public byte[] ProfilePicture { get; set; } = [];
        [JsonIgnore]
        public DateTime? LastUsernameChange { get; set; }
        public string Username { get; set; } = "";
        public string Hashtag { get; set; } = "";
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public string Biography { get; set; } = "";
        public long Id { get; set; } = -1;
        public DateOnly? Birthday { get; set; }
        public bool FaEnabled { get; set; }
        public string Token { get; set; } = "";
        
        public static explicit operator Relationship(User? user)
        {
            ArgumentNullException.ThrowIfNull(user, nameof(user));
            return new()
            {
                Username = user.Username,
                Hashtag = user.Hashtag,
                Id = user.Id,
                Biography = user.Biography,
                ProfilePicture = user.ProfilePicture,
            };
        }
    }
}

