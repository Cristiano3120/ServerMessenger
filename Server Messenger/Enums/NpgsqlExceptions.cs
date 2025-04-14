namespace Server_Messenger.Enums
{
    public enum NpgsqlExceptions : byte
    {
        None = 0,
        UnknownError = 1,
        ConnectionError = 2,
        AccCreationError = 3,
        WrongLoginData = 4,
        UserNotFound = 5,
        TokenInvalid = 6,
        NoDataEntrys = 7,
        UnexpectedEx = 8,
        RequestedUserIsBlocked = 9,
        PayloadDataMissing = 10,
    }
}
