using System.Text.Json.Serialization;
using System.Text.Json;

namespace Server_Messenger.Json
{
    internal static class JsonConverters
    {
        public class Base64ByteArrayJsonConverter : JsonConverter<byte[]>
        {
            public override byte[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
                => Convert.FromBase64String(reader.GetString()!);

            public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
                => writer.WriteStringValue(Convert.ToBase64String(value));
        }
    }
}
