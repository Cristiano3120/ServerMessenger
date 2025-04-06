using System.Text.Json;

namespace Server_Messenger.Json
{
    public static class JsonSerializer
    {
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new();

        static JsonSerializer()
        {
            _jsonSerializerOptions.Converters.Add(new JsonConverters.RelationshipConverter());
            _jsonSerializerOptions.Converters.Add(new JsonConverters.UserConverter());
            _jsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            _jsonSerializerOptions.WriteIndented = true;
        }

        public static string Serialize(object payload)
            => System.Text.Json.JsonSerializer.Serialize(payload, _jsonSerializerOptions);

        /// <summary>
        /// Deserializes a <see cref="JsonElement"/> into the specified type <typeparamref name="T"/>.
        /// It uses the <see cref="JsonSerializerOptions"/> defined in the static constructor.
        /// If the property name is the same as the type name<c>(not case sensitivite)</c> you can just pass in the whole <see cref="JsonElement"/>
        /// </summary>
        /// <typeparam name="T">The type to deserialize into</typeparam>
        /// <param name="payload">The <see cref="JsonElement"/> to deserialize</param>
        /// <returns>The deserialized object of type <typeparamref name="T"/> or <c>null</c> if deserialization fails.</returns>
        public static T? Deserialize<T>(JsonElement payload)
        {
            if (payload.ValueKind == JsonValueKind.Array)
            {
                return payload.Deserialize<T>(_jsonSerializerOptions);
            }

            string propertyName = typeof(T).Name.ToCamelCase();
            return payload.TryGetProperty(propertyName, out JsonElement property)
                ? property.Deserialize<T>(_jsonSerializerOptions)
                : payload.Deserialize<T>(_jsonSerializerOptions);
        }
    }
}
