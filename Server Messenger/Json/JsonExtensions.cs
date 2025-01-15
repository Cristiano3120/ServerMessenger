using System.Security.Cryptography;
using System.Text.Json;

namespace Server_Messenger.Json
{
    internal static class JsonExtensions
    {
        /// <summary>
        /// Needs to be called on the <c>code</c> property of the <see cref="JsonElement"/>.
        /// Converts the data that is sent as an byte to the <see cref="OpCode"/> equivalent.
        /// </summary>
        /// <param name="property">The <c>code</c> as an <see cref="JsonElement"/> property</param>
        /// <returns><returns><c>Returns</c> the from the Client received OpCode</returns>
        public static OpCode GetOpCode(this JsonElement property)
            => (OpCode)property.GetByte();

        /// <summary>
        /// Extrcats the key and the iv from the <see cref="JsonElement"/>
        /// </summary>
        /// <param name="property">The payload as an <see cref="JsonElement"/></param>
        /// <returns><c>Returns</c> the from the client sent Aes</returns>
        public static Aes GetAes(this JsonElement property)
        {
            var aes = Aes.Create();
            aes.Key = property.GetProperty("key").GetBytesFromBase64();
            aes.IV = property.GetProperty("iv").GetBytesFromBase64();
            return aes;
        }
    }
}
