using System.Security.Cryptography;

namespace Server_Messenger
{
    public record UserData
    {
        public long Id {  get; init; }
        public required Aes Aes {  get; init; }

        public void Deconstruct(out long id, out Aes aes)
        {
            id = Id;
            aes = Aes;
        }
    }
}
