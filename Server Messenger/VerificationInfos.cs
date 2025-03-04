namespace Server_Messenger
{
    internal struct VerificationInfos(byte verificationAttempts, long verificationCode, string email)
    {
        public byte VerificationAttempts { get; set; } = verificationAttempts;
        public long VerificationCode { get; init; } = verificationCode;
        public string Email { get; init; } = email;
    }
}
