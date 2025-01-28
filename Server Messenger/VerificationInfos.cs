namespace Server_Messenger
{
    internal class VerificationInfos
    {
        public byte VerificationAttempts { get; set; }
        public long VerificationCode { get; init; }
        public string Email { get; init; } = "";
    }
}
