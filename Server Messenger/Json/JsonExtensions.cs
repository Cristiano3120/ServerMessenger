using System.Security.Cryptography;
using System.Text.Json;

namespace Server_Messenger.Json
{
    internal static class JsonExtensions
    {
        private static readonly string _pathToConfig = Server.GetDynamicPath(@"Settings\appsettings.json");

        #region GetExtensions

        /// <summary>
        /// Needs to be called on the <c>root</c> of the <see cref="JsonElement"/>.
        /// Converts the data that is sent as an byte to the <see cref="OpCode"/> equivalent.
        /// </summary>
        /// <param name="property">The <see cref="Enum"/> as an <see cref="JsonElement"/> property</param>
        /// <returns><c>Returns</c> the from the Client received <see cref="OpCode"/></returns>
        public static OpCode GetOpCode(this JsonElement property)
            => (OpCode)property.GetProperty("opCode").GetByte();

        /// <summary>
        /// Needs to be called on the <c>root</c> of the <see cref="JsonElement"/>.
        /// Converts the data that is sent as an byte to the <see cref="SettingsUpdate"/> equivalent.
        /// </summary>
        /// <param name="property">The <see cref="Enum"/> as an <see cref="JsonElement"/> property</param>
        /// <returns><c>Returns</c> the from the Client received <see cref="SettingsUpdate"/></returns>
        public static SettingsUpdate GetSettingsUpdate(this JsonElement property)
            => (SettingsUpdate)property.GetProperty("settingsUpdate").GetByte();
                

        /// <summary>
        /// Extrcats the key and the iv from the <see cref="JsonElement"/>
        /// </summary>
        /// <param name="property">The payload as an <see cref="JsonElement"/></param>
        /// <returns><c>Returns</c> the from the client sent Aes</returns>
        public static Aes GetAes(this JsonElement property)
        {
            AesKeyData aesKeyData = JsonSerializer.Deserialize<AesKeyData>(property);

            Aes aes = Aes.Create();
            aes.Key = Convert.FromBase64String(aesKeyData.Key);
            aes.IV = Convert.FromBase64String(aesKeyData.Iv);

            return aes;
        }

        #endregion

        #region ReadJson

        public static JsonElement ReadConfig()
        {
            string jsonFileContent = File.ReadAllText(_pathToConfig);
            return JsonDocument.Parse(jsonFileContent).RootElement;
        }

        #endregion
    }
}
