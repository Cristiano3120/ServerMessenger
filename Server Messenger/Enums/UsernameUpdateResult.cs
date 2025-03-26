namespace Server_Messenger.Enums
{
    public enum UsernameUpdateResult : byte
    {
        Successful = 0,
        NameTaken = 1,
        OnCooldown = 2,
    }
}
