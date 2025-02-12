namespace Server_Messenger
{
    internal sealed record RelationshipUpdate()
    {
        public required User User { get; init; }
        public required Relationship Relationship { get; init; }
        public required Relationshipstate RequestedRelationshipstate { get; init; }
    }
}
