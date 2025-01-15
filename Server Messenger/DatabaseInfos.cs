namespace Server_Messenger
{
    public sealed record DatabaseInfos()
    {
        public string DbPassword { get; init; } = "";
        public bool? DbUpdated { get; init; } = null;

        public DatabaseInfos(string password, bool? updated) : this() 
        {
            DbPassword = password;
            DbUpdated = updated;
        }
    }
}
