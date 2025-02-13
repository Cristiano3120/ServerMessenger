using System.Collections;
using System.Globalization;

namespace Server_Messenger
{
    internal sealed class User : IEnumerable<(string name, string value)>
    {
        public byte[] ProfilePicture { get; set; } = [];
        public string Username { get; set; } = "";
        public string HashTag { get; set; } = "";
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public string Biography { get; set; } = "";
        public long Id { get; set; } = -1;
        public DateOnly? Birthday { get; set; }
        public bool FaEnabled { get; set; }
        public string Token { get; set; } = "";

        public IEnumerator<(string name, string value)> GetEnumerator()
        {
            yield return (nameof(Username), Username);
            yield return (nameof(HashTag), HashTag);
            yield return (nameof(Email), Email);
            yield return (nameof(Password), Password);
            yield return (nameof(Biography), Biography);
            yield return (nameof(Id), Id.ToString());
            yield return (nameof(Birthday), Birthday?.ToString(new CultureInfo("de-DE")) ?? "");
            yield return (nameof(ProfilePicture), Convert.ToBase64String(ProfilePicture));
            yield return (nameof(FaEnabled), FaEnabled.ToString());
            yield return (nameof(Token), Token);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}

