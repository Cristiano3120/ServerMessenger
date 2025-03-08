using System.Text.Json.Serialization;

namespace Server_Messenger
{
    internal readonly record struct RelationshipUpdate()
    {
        [JsonPropertyName("user")]
        public required User? User { get; init; }

        [JsonPropertyName("relationship")]
        public required Relationship? Relationship { get; init; }

        [JsonPropertyName("requestedRelationshipState")]
        public required RelationshipState RequestedRelationshipState { get; init; }

        public void Deconstruct(out User user, out Relationship? relationship, out RelationshipState requestedRelationshipState)
        {
            user = User;
            relationship = Relationship;
            requestedRelationshipState = RequestedRelationshipState;
        }
    }
}
