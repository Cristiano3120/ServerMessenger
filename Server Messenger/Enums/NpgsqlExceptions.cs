namespace Server_Messenger.Enums
{
    /// <summary>
    /// Enum for the different exceptions that can be thrown by Npgsql and are handled by the server
    /// </summary>
    public enum NpgsqlExceptions : byte
    {
        None = 0,
        UnknownError = 1,
        ConnectionError = 2,
        AccCreationError = 3,
        WrongLoginData = 4,
        UserNotFound = 5,
    }
}
