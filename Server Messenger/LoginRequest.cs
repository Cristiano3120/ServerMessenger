using System.Text.Json.Serialization;

namespace Server_Messenger
{
    public record LoginRequest
    {
        [JsonPropertyName("email")]
        public string Email { get; init; } = "";

        [JsonPropertyName("password")]
        public string Password { get; init; } = "";

        [JsonPropertyName("token")]
        public string Token { get; init; } = "";

        [JsonPropertyName("stayLoggedIn")]
        public bool StayLoggedIn { get; init; }

        public void Deconstruct(out string email, out string password, out bool stayLoggedIn)
        {
            email = Email;
            password = Password;
            stayLoggedIn = StayLoggedIn;
        }
    }
}
