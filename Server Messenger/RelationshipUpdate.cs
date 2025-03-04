using System.Text.Json.Serialization;

namespace Server_Messenger
{
    internal readonly record struct RelationshipUpdate()
    {
        [JsonPropertyName("userId")]
        public required long UserId { get; init; }

        [JsonPropertyName("relationship")]
        public required Relationship Relationship { get; init; }

        [JsonPropertyName("requestedRelationshipState")]
        public required RelationshipState RequestedRelationshipState { get; init; }

        public void Deconstruct(out long userId, out Relationship relationship, out RelationshipState requestedRelationshipState)
        {
            userId = UserId;
            relationship = Relationship;
            requestedRelationshipState = RequestedRelationshipState;
        }
    }
}
