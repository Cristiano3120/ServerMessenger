namespace Server_Messenger.PersonalDataDb
{
    internal class Relationships
    {
        public required long SenderId { get; set; }
        public required long ReceiverId { get; set; }
        public required Relationshipstate Relationshipstate { get; set; }
    }
}
