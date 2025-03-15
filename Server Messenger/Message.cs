namespace Server_Messenger
{
    public readonly record struct Message(string ChatId, long SenderId, DateTime DateTime, string Content) { }
}
