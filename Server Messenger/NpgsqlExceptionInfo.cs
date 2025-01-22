namespace Server_Messenger
{
    internal sealed record NpgsqlExceptionInfos
    {
        public NpgsqlExceptions Exception { get; init; }
        public string ColumnName { get; init; }

        #region Constructors

        public NpgsqlExceptionInfos() : this(NpgsqlExceptions.None, "")
        {

        }

        public NpgsqlExceptionInfos(NpgsqlExceptions exception) : this (exception, "")
        {

        }

        public NpgsqlExceptionInfos(NpgsqlExceptions exception, string columnName)
        {
            Exception = exception;
            ColumnName = columnName;
        }

        #endregion

        public void Deconstruct(out NpgsqlExceptions exception, out string columnName)
        {
            exception = Exception;
            columnName = ColumnName;
        }
    }
}
