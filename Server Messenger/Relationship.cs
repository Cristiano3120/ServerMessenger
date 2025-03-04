using System.Collections;

namespace Server_Messenger
{
    public sealed class Relationship: IEnumerable<(string name, string value)>
    {
        public long Id { get; init; } = -1;
        public byte[] ProfilePicture { get; set; } = [];
        public string Username { get; set; } = "";
        public string HashTag { get; set; } = "";
        public string Biography { get; set; } = "";
        public RelationshipState RelationshipState { get; set; }

        public IEnumerator<(string name, string value)> GetEnumerator()
        {
            yield return (nameof(Username).ToCamelCase(), Username);
            yield return (nameof(HashTag).ToCamelCase(), HashTag);
            yield return (nameof(Biography).ToCamelCase(), Biography);
            yield return (nameof(Id).ToCamelCase(), Id.ToString());
            yield return (nameof(ProfilePicture).ToCamelCase(), Convert.ToBase64String(ProfilePicture));
            yield return (nameof(RelationshipState).ToCamelCase(), RelationshipState.ToString());
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
