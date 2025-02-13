using System.Text.Json.Serialization;
using System.Text.Json;
using System.Globalization;
using System;

namespace Server_Messenger.Json
{
    internal static class JsonConverters
    {
        public class UserConverter : JsonConverter<User>
        {
            public override User Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                using (JsonDocument doc = JsonDocument.ParseValue(ref reader))
                {
                    JsonElement root = doc.RootElement;

                    root = root.GetProperty("user");

                    return new User()
                    {
                        ProfilePicture = root.GetProperty("profilePicture").GetBytesFromBase64(),
                        Username = root.GetProperty("username").GetString()!,
                        HashTag = root.GetProperty("hashTag").GetString()!,
                        Email = root.GetProperty("email").GetString()!,
                        Password = root.GetProperty("password").GetString()!,
                        Biography = root.GetProperty("biography").GetString()!,
                        Id = long.Parse(root.GetProperty("id").GetString()!),
                        Birthday = DateOnly.Parse(root.GetProperty("birthday").GetString()!, new CultureInfo("de-DE")),
                        FaEnabled = bool.Parse(root.GetProperty("faEnabled").GetString()!),
                        Token = root.GetProperty("token").GetString()!,
                    };
                }
            }

            public override void Write(Utf8JsonWriter writer, User value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();

                foreach ((string name, string value) item in value)
                {
                    writer.WritePropertyName(item.name);
                    writer.WriteStringValue(item.value);
                }

                writer.WriteEndObject();
            }
        }

        public class RelationshipConverter : JsonConverter<Relationship>
        {
            public override void Write(Utf8JsonWriter writer, Relationship value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();

                foreach ((string name, string value) item in value)
                {
                    writer.WritePropertyName(item.name);
                    writer.WriteStringValue(item.value);
                }

                writer.WriteEndObject();
            }

            public override Relationship? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                using (var doc = JsonDocument.ParseValue(ref reader))
                {
                    JsonElement root = doc.RootElement;

                    root = root.GetProperty("relationship");

                    byte[] profilPic = [];

                    return root.ValueKind == JsonValueKind.Null
                        ? null
                        : new Relationship()
                        {
                            ProfilePicture = root.GetProperty("profilePicture").GetBytesFromBase64(),
                            Username = root.GetProperty("username").GetString()!,
                            HashTag = root.GetProperty("hashTag").GetString()!,
                            Biography = root.GetProperty("biography").GetString()!,
                            Id = long.Parse(root.GetProperty("id").GetString()!),
                            Relationshipstate = Enum.Parse<Relationshipstate>(root.GetProperty("relationshipstate").GetString()!),
                        };
                }
            }
        }
    }
}
