using System.Text.Json.Serialization;

namespace Server_Messenger
{
    public readonly struct AesKeyData
    {
        [JsonPropertyName("key")]
        public string Key { get; init; }

        [JsonPropertyName("iv")]
        public string IV { get; init; }

        public AesKeyData(string key, string iv)
        {
            Key = key;
            IV = iv;
        }
    }
}
