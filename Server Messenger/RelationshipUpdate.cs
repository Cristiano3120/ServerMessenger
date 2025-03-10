namespace Server_Messenger
{
    internal readonly record struct RelationshipUpdate()
    {
        public required User? User { get; init; }

        public required Relationship? Relationship { get; init; }

        public required RelationshipState RequestedRelationshipState { get; init; }

        public void Deconstruct(out User? user, out Relationship? relationship, out RelationshipState requestedRelationshipState)
        {
            user = User;
            relationship = Relationship;
            requestedRelationshipState = RequestedRelationshipState;
        }
    }
}
