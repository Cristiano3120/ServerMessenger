namespace ServerMessenger
{
    public sealed class Friend
    {
        public required string Username { get; set; }
        public required RelationshipStateEnum Status { get; set; }
        public required string ProfilPic { get; set; }

        public void Deconstruct(out string username, out RelationshipStateEnum status, out string profilPic)
        {
            username = Username;
            status = Status;
            profilPic = ProfilPic;
        }
    }
}
