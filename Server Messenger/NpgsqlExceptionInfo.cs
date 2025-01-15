namespace Server_Messenger
{
    internal sealed record NpgsqlExceptionInfos() 
    {
        public NpgsqlExceptions Exception { get; init; }
        public string ColumnName { get; init; } = "";

        public NpgsqlExceptionInfos(NpgsqlExceptions exception, string columnName = "") : this()
        {
            Exception = exception;
            ColumnName = columnName;
        }

        public void Deconstruct(out NpgsqlExceptions exception, out string columnName)
        {
            exception = Exception;
            columnName = ColumnName;
        }
    }
}
