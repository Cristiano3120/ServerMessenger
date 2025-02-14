using System;
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

        public static explicit operator Relationship(User user)
            => new()
            {
                Username = user.Username,
                HashTag = user.HashTag,
                Id = user.Id,
                Biography = user.Biography,
                ProfilePicture = user.ProfilePicture,
            };

        public IEnumerator<(string name, string value)> GetEnumerator()
        {
            yield return (nameof(Username).ToCamelCase(), Username);
            yield return (nameof(HashTag).ToCamelCase(), HashTag);
            yield return (nameof(Email).ToCamelCase(), Email);
            yield return (nameof(Password).ToCamelCase(), Password);
            yield return (nameof(Biography).ToCamelCase(), Biography);
            yield return (nameof(Id).ToCamelCase(), Id.ToString());
            yield return (nameof(Birthday).ToCamelCase(), Birthday?.ToString(new CultureInfo("de-DE")) ?? "");
            yield return (nameof(ProfilePicture).ToCamelCase(), Convert.ToBase64String(ProfilePicture));
            yield return (nameof(FaEnabled).ToCamelCase(), FaEnabled.ToString());
            yield return (nameof(Token).ToCamelCase(), Token);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}

