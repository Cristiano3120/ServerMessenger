namespace Server_Messenger
{
    internal record struct VerificationInfos(byte VerificationAttempts, long VerificationCode, long UserId)
    {
        public byte VerificationAttempts { get; set; } = VerificationAttempts;
        public long VerificationCode { get; init; } = VerificationCode;
        public long UserId { get; init; } = UserId;
    }
}
