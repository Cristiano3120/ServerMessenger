namespace Server_Messenger
{
    public readonly record struct UsernameUpdate(string Username, string Hashtag, long UserId, DateTime LastChanged) { }
}