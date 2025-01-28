using Npgsql;
using System.Data;
using System.Text.RegularExpressions;

namespace Server_Messenger
{
    internal static partial class PersonalDataDatabase
    {
        [GeneratedRegex(@"Schlüssel »\(([^)]+)\)")]
        public static partial Regex NpgsqlExceptionKeyRegex();

        private static readonly string _connectionString = ReadConnString();

        private static string ReadConnString()
        {
            return Server.Config.GetProperty("ConnectionStrings").GetProperty("PersonalDataDatabase").GetString()!;
        }

        #region CreateAccount

        public static async Task<NpgsqlExceptionInfos> CreateAccount(User user)
        {
            try
            {
                const string query = @"INSERT INTO users (username, hashtag, email, password, biography, birthday, profilpic, id, fa) 
                VALUES (@username, @hashTag, @email, @password, @biography, @birthday, @profilpic, @id, @fa);";

                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                if (user.Id == -1)
                    user.Id = GetHighestID();

                var cmd = new NpgsqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@username", Security.EncryptAesDatabase<string, string>(user.Username));
                cmd.Parameters.AddWithValue("@hashTag", Security.EncryptAesDatabase<string, string>(user.HashTag));
                cmd.Parameters.AddWithValue("@email", Security.EncryptAesDatabase<string, string>(user.Email));
                cmd.Parameters.AddWithValue("@password", Security.EncryptAesDatabase<string, string>(user.Password));
                cmd.Parameters.AddWithValue("@biography", Security.EncryptAesDatabase<string, string>(user.Biography));
                cmd.Parameters.AddWithValue("@profilpic", Security.EncryptAesDatabase<byte[], byte[]>(user.ProfilePicture));
                cmd.Parameters.AddWithValue("@birthday", user.Birthday!.Value);
                cmd.Parameters.AddWithValue("@id", user.Id);
                cmd.Parameters.AddWithValue("@fa", user.FaEnabled);
                await cmd.ExecuteNonQueryAsync();

                return (new NpgsqlExceptionInfos());
            }
            catch (NpgsqlException ex)
            {
                NpgsqlExceptionInfos exception = await HandleNpgsqlException(ex);
                if (exception.ColumnName == "id")
                    await CreateAccount(user);

                return exception;
            }
        }

        private static long GetHighestID()
        {
            try
            {
                const string query = @"SELECT MAX(id) FROM users;";
                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();
                using var cmd = new NpgsqlCommand(query, conn);
                var result = cmd.ExecuteScalar();
                return result == DBNull.Value
                    ? 1
                    : Convert.ToInt64(result) + 1;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                return -1;
            }
        }

        #endregion

        public static async Task<(User? user, NpgsqlExceptionInfos npgsqlExceptionInfos)> CheckLoginData(string email, string password)
        {
            try
            {
                const string query = @"SELECT * FROM users WHERE email = @email AND password = @password;";

                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new NpgsqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@email", Security.EncryptAesDatabase<string, string>(email));
                cmd.Parameters.AddWithValue("@password", Security.EncryptAesDatabase<string, string>(password));

                using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();

                if (!reader.HasRows)
                    return (null, new NpgsqlExceptionInfos(NpgsqlExceptions.WrongLoginData));

                await reader.ReadAsync();
                var user = new User()
                {
                    Username = Security.DecryptAesDatabase<string, string>(reader.GetString("username")),
                    HashTag = Security.DecryptAesDatabase<string, string>(reader.GetString("hashtag")),
                    Email = Security.DecryptAesDatabase<string, string>(reader.GetString("email")),
                    Password = "",
                    Biography = Security.DecryptAesDatabase<string, string>(reader.GetString("biography")),
                    Id = reader.GetInt64("id"),
                    Birthday = DateOnly.FromDateTime(reader.GetDateTime("birthday")),
                    ProfilePicture = Security.DecryptAesDatabase<byte[], byte[]>(await reader.GetFieldValueAsync<byte[]>("profilpic")),
                    FaEnabled = await reader.GetFieldValueAsync<bool>("fa"),
                };

                return (user, new NpgsqlExceptionInfos());
            }
            catch (NpgsqlException ex)
            {
                NpgsqlExceptionInfos exceptionInfos = await HandleNpgsqlException(ex);
                return (null, exceptionInfos);
            }
        }

        public static async Task RemoveUser(string email)
        {
            const string query = "DELETE FROM users WHERE email = @email";
            var npgsqlConnection = new NpgsqlConnection(_connectionString);
            await npgsqlConnection.OpenAsync();

            var cmd = new NpgsqlCommand(query, npgsqlConnection);
            cmd.Parameters.AddWithValue("@email", Security.EncryptAesDatabase<string, string>(email));
            await cmd.ExecuteNonQueryAsync(); 
        }

        private static async Task<NpgsqlExceptionInfos> HandleNpgsqlException(NpgsqlException ex)
        {
            Logger.LogError(ex);
            var errorCode = ex.SqlState;

            switch (errorCode)
            {
                case "08001" or "08006" or "08003":
                    await Server.Shutdown();
                    return new NpgsqlExceptionInfos(NpgsqlExceptions.ConnectionError);
                case "23505":
                    var message = ex.Message;
                    string columnName = "";

                    if (!string.IsNullOrEmpty(message))
                    {
                        Match match = NpgsqlExceptionKeyRegex().Match(message);

                        if (match.Success)
                        {
                            columnName = match.Groups[1].Value;
                        }
                    }

                    return new NpgsqlExceptionInfos(NpgsqlExceptions.AccCreationError, columnName);
                default:
                    return new NpgsqlExceptionInfos(NpgsqlExceptions.UnknownError);
            }
        }
    }
}
