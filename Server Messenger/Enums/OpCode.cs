namespace Server_Messenger.Enums
{
    internal enum OpCode : byte
    {
        SendRSA = 0,
        ReceiveAes = 1,
        ReadyToReceive = 2,
        RequestToCreateAccount = 3,
        AnswerToCreateAccount = 4,
        RequestToLogin = 5,
        AnswerToLogin = 6,
        RequestToVerifiy = 7,
        VerificationWentWrong = 8,
        AnswerToAutoLogin = 10,
        UpdateRelationship = 11,
        AnswerToRequestedRelationshipUpdate = 12,
        SendFriendships = 13,
    }
}
