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

                    root = root.TryGetProperty("user", out JsonElement property)
                        ? property
                        : doc.RootElement.GetProperty("User");

                    return new User()
                    {
                        ProfilePicture = root.GetProperty("ProfilePicture").GetBytesFromBase64(),
                        Username = root.GetProperty("Username").GetString()!,
                        HashTag = root.GetProperty("HashTag").GetString()!,
                        Email = root.GetProperty("Email").GetString()!,
                        Password = root.GetProperty("Password").GetString()!,
                        Biography = root.GetProperty("Biography").GetString()!,
                        Id = long.Parse(root.GetProperty("Id").GetString()!),
                        Birthday = DateOnly.Parse(root.GetProperty("Birthday").GetString()!, new CultureInfo("de-DE")),
                        FaEnabled = bool.Parse(root.GetProperty("FaEnabled").GetString()!),
                        Token = root.GetProperty("Token").GetString()!,
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

                    root = root.TryGetProperty("relationship", out JsonElement rootProperty)
                        ? rootProperty
                        : doc.RootElement.GetProperty("Relationship");

                    byte[] profilPic = [];

                    if (root.TryGetProperty("ProfilePicture", out JsonElement profilPicProperty))
                    {
                        profilPic = profilPicProperty.GetBytesFromBase64();
                    }

                    return root.ValueKind == JsonValueKind.Null
                        ? null
                        : new Relationship()
                        {
                            ProfilePicture = profilPic,
                            Username = root.GetProperty("Username").GetString()!,
                            HashTag = root.GetProperty("HashTag").GetString()!,
                            Biography = root.GetProperty("Biography").GetString()!,
                            Id = long.Parse(root.GetProperty("Id").GetString()!),
                        };
                }
            }
        }
    }
}
