namespace Server_Messenger.PersonalDataDb
{
    internal record Relationships
    {
        public required long SenderId { get; set; }
        public required long ReceiverId { get; set; }
        public required RelationshipState RelationshipState { get; set; }
    }
}
