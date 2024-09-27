using Npgsql;
using System.Security.Cryptography;
using System.Text;

namespace ServerMessenger
{

    /// <summary>
    /// This class provides methods for the database that stores infos about users.
    /// </summary>
    internal static class AccountInfoDatabase
    {
        #pragma warning disable CS8618
        private const string _pathForDatabaseInfos = @"C:\Users\Crist\Desktop\MessengerServerAccountDatabaseConnectionString.txt";
        private static string? _connectionString;
        private static string? _password;
        private static byte[]? _salt;
        private static byte[] _key;
        private static byte[] _IV;

        /// <summary>
        /// Reads crutial vars for the Server from files
        /// </summary>
        public static void Initialize()
        {
            try
            {
                if (!File.Exists(_pathForDatabaseInfos) || !File.Exists(@"C:\Users\Crist\Desktop\AESData.txt"))
                {
                    Server.StopServer("Error(AccountInfoDatabse.Initialize()): Atleast one of the files didn´t exist.");
                }

                _ = DisplayError.Log("Files exist.");
                using (var streamReader = new StreamReader(@"C:\Users\Crist\Desktop\AESData.txt"))
                {
                    _password = streamReader.ReadLine()!;
                    _salt = Encoding.UTF8.GetBytes(streamReader.ReadLine()!);
                }

                if (string.IsNullOrEmpty(_password) || _salt is null)
                {
                    Server.StopServer("Error(AccountInfoDatabse.Initialize()): Salt or/and password was null");
                }

                ReadEncryptionAndConnectionString();
                DeriveKeyAndIV(_password!, _salt!, out _key, out _IV);
            }
            catch (Exception ex)
            {
                DisplayError.DisplayBasicErrorInfos(ex, "AccountInfosDatabase", "Initialize");
                Server.StopServer("Error(AccountInfoDatabse.Initialize()): Something happend while initialising");
            }
        }

        /// <summary>
        /// Gets the AES for the database.
        /// </summary>
        /// <param name="password">The password needed to get the AES key.</param>
        /// <param name="salt">he salt needed to get the AES key.</param>
        /// <param name="key">The corresponding AES key.</param>
        /// <param name="iv">The corresponding AES IV.</param>
        public static void DeriveKeyAndIV(string password, byte[] salt, out byte[] key, out byte[] iv)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256))
            {
                key = pbkdf2.GetBytes(32);
                iv = pbkdf2.GetBytes(16);
            }
        }

        private static void ReadEncryptionAndConnectionString()
        {
            try
            {
                using var reader = new StreamReader(_pathForDatabaseInfos);
                _ = DisplayError.Log("Reading the connection string");
                _connectionString = reader.ReadLine();
            }
            catch (Exception ex)
            {
                DisplayError.DisplayBasicErrorInfos(ex, "AccountInfoBase", "ReadAesAndConnectionString()");
            }
        }

        public static async Task UpdateRelationshipState(int userId, int friendId, RelationshipStateEnum state)
        {
            try
            {
                var acceptedCommand = "UPDATE friendships SET status = 'Accepted' WHERE userid = @f AND friendid = @u";
                var declineCommand = "DELETE FROM friendships WHERE userid = @f AND friendid = @u";
                var blockedCommand = "UPDATE friendships SET status = 'Blocked' WHERE (userid = @u AND friendid = @f) OR (userid = @f AND friendid = @u)";
                var unblockORdeleteCommand = "DELETE FROM friendships WHERE (userid = @u AND friendid = @f) OR (userid = @f AND friendid = @u)";

                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    using (var cmd = new NpgsqlCommand())
                    {
                        cmd.Connection = conn;
                        cmd.Parameters.AddWithValue("@u", userId);
                        cmd.Parameters.AddWithValue("@f", friendId);
                        Console.WriteLine("Updating a friend relation");
                        switch (state)
                        {
                            case RelationshipStateEnum.Accepted:
                                Console.WriteLine("Changing the state from pending to accepted");
                                cmd.CommandText = acceptedCommand;
                                break;
                            case RelationshipStateEnum.Blocked:
                                Console.WriteLine("Blocking a user");
                                cmd.CommandText = blockedCommand;
                                break;
                            case RelationshipStateEnum.Decline:
                                Console.WriteLine("Declining a friend request");
                                cmd.CommandText = declineCommand;
                                break;
                            case RelationshipStateEnum.Unblocked:
                                Console.WriteLine("Unblocking a user");
                                cmd.CommandText = unblockORdeleteCommand;
                                break;
                            case RelationshipStateEnum.Delete:
                                Console.WriteLine("Deleting a user as a friend");
                                cmd.CommandText = unblockORdeleteCommand;
                                break;
                        }
                        _ = cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                DisplayError.DisplayBasicErrorInfos(ex, "AccountInfoDatabase", "UpdateRelationshipState");
            }
        }

        public static async Task<List<(int friendId, string status)>> GetFriendshipsAsync(int userId)
        {
            var friendships = new List<(int friendId, string status)>();

            try
            {
                Console.WriteLine("Trying to get all pending friend requests and friends of the user");

                var command = @"SELECT CASE WHEN userid = @userId THEN friendid ELSE userid END AS friendid, 
                status FROM friendships WHERE (userid = @userId OR friendid = @userId) AND (status != 'Pending' 
                OR (status = 'Pending' AND friendid = @userId) OR  status = 'Blocked')";

                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    using (var cmd = new NpgsqlCommand(command, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        var reader = await cmd.ExecuteReaderAsync();

                        while (await reader.ReadAsync())
                        {
                            var friendId = reader.GetInt32(0);
                            var status = reader.GetString(1);

                            friendships.Add((friendId, status));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DisplayError.DisplayBasicErrorInfos(ex, "AccountInfoDatabase", "GetFriendshipsAsync");
            }
            return friendships;
        }

        public static async Task PutFriendRequestIntoDbAsync(int userId, int friendId)
        {
            try
            {
                Console.WriteLine("Trying to put the fa into the database");
                var insertCommand = @" INSERT INTO friendships (userid, friendid, status) VALUES (@u, @f, 'Pending') 
                ON CONFLICT (userid, friendid) DO NOTHING;";

                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    using (var cmd = new NpgsqlCommand(insertCommand, conn))
                    {
                        cmd.Parameters.AddWithValue("@u", userId);
                        cmd.Parameters.AddWithValue("@f", friendId);

                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                DisplayError.DisplayBasicErrorInfos(ex, "AccountInfoDatabase", "PutFriendRequestIntoDbAsync");
            }
        }

        public static async Task<string?> GetUsernameByIdAsync(int userId)
        {
            try
            {
                var command = "SELECT username FROM \"Users\" WHERE \"ID\"  = @userId";
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    using (var cmd = new NpgsqlCommand(command, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        var result = await cmd.ExecuteScalarAsync();

                        if (result != null)
                        {
                            var encryptedUsername = result.ToString();
                            return Security.DecryptDataAES(Convert.FromBase64String(encryptedUsername!), _key, _IV);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DisplayError.DisplayBasicErrorInfos(ex, "AccountInfoDatabse", "GetUsernameByIdAsync");
            }
            return null;
        }

        public static async Task<int?> GetUserIdByUsernameAsync(string username)
        {
            try
            {
                var query = @"SELECT ""ID"" FROM ""Users"" WHERE username = @u LIMIT 1";

                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    using (var cmd = new NpgsqlCommand(query, conn))
                    {
                        var encryptedUsernameAsBytes = Security.EncryptDataAESDatabase(Encoding.UTF8.GetBytes(username), _key, _IV);
                        var encryptedUsername = Convert.ToBase64String(encryptedUsernameAsBytes);
                        cmd.Parameters.AddWithValue("@u", encryptedUsername);
                        var result = await cmd.ExecuteScalarAsync();

                        if (result != null && int.TryParse(result.ToString(), out int userId))
                        {
                            return userId;
                        }
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                DisplayError.DisplayBasicErrorInfos(ex, "AccountInfoDatabse", "GetUserIdByUsernameAsync");
                return null;
            }
        }

        public static async Task<string?> GetProfilPicAsync(int? userId, string? username)
        {
            try
            {
                string command;
                if (userId != null)
                {
                    command = @"SELECT ""profilpicture"" FROM ""Users"" WHERE ""ID"" = @id";
                }
                else
                {
                    command = @"SELECT ""profilpicture"" FROM ""Users"" WHERE ""Username"" = @username";
                }
                using var conn = new NpgsqlConnection(_connectionString);
                {
                    await conn.OpenAsync();

                    using var cmd = new NpgsqlCommand(command, conn);
                    {
                        if (userId != null)
                        {
                            cmd.Parameters.AddWithValue("id", userId);
                        }
                        else
                        {
                            cmd.Parameters.AddWithValue("username", username!);
                        }
                        var result = await cmd.ExecuteScalarAsync();

                        if (result is byte[] imageBytes && imageBytes.Length > 0)
                        {
                            return Convert.ToBase64String(imageBytes);
                        }
                        else
                        {
                            return null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DisplayError.DisplayBasicErrorInfos(ex, "AccountInfoDatabase", "GetProfilPicAsync");
                return null;
            }
        }

        public static async Task<(bool? isSuccess, string? username, int? id)> CheckLoginDataAsync(string email, string password)
        {
            try
            {
                var command = "SELECT Password, Username, \"ID\" FROM \"Users\" WHERE Email = @e";
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    var encryptedEmailData = Security.EncryptDataAESDatabase(Encoding.UTF8.GetBytes(email), _key, _IV);
                    var encryptedEmail = Convert.ToBase64String(encryptedEmailData);
                    var encryptedPasswordData = Security.EncryptDataAESDatabase(Encoding.UTF8.GetBytes(password), _key, _IV);
                    var encryptedPassword = Convert.ToBase64String(encryptedPasswordData);

                    using (var cmd = new NpgsqlCommand(command, conn))
                    {
                        cmd.Parameters.AddWithValue("@e", encryptedEmail);
                        var reader = await cmd.ExecuteReaderAsync();

                        if (reader.HasRows)
                        {
                            await reader.ReadAsync();

                            var storedPassword = reader.GetString(0);
                            var username = reader.GetString(1);
                            var id = reader.GetInt32(2);

                            if (encryptedPassword == storedPassword)
                            {
                                var encryptedData = Convert.FromBase64String(username);
                                username = Security.DecryptDataAES(encryptedData, _key, _IV);
                                return (true, username, id);
                            }
                        }
                    }
                }

                return (false, null, null);
            }
            catch (Exception ex)
            {
                DisplayError.DisplayBasicErrorInfos(ex, "AccountInfoDatabase", "CheckLoginData");
                return (null, null, null);
            }
        }

        public static async Task<bool> PutAccountIntoTheDb(User user)
        {
            var result = await CheckIfEmailOrUsernameExists(user);
            if (result != UserCheckResult.None)
            {
                return false;
            }
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                try
                {
                    await conn.OpenAsync();

                    _ = DisplayError.Log("Connection to the database was sucessfull");
                    string command = "INSERT INTO \"Users\" (email, username, password, firstName, lastName, day, month, year, profilpicture) VALUES (@e, @u, @p, @fN, @lN, @d, @m, @y, @pic)";

                    var imageBytes = GetBytesFromImage(filepath: @"C:\Users\Crist\source\repos\ServerMessenger\ServerMessenger\defaultPic.png");

                    var dataToPutInDb = new List<string>
                    {
                        user.Email,
                        user.Username,
                        user.Password,
                        user.FirstName,
                        user.LastName,
                        user.Day.ToString(),
                        user.Month.ToString(),
                        user.Year.ToString()
                    };

                    var test = new List<string>()
                    {
                        "email",
                        "username",
                        "password",
                        "firstName",
                        "lastName",
                        "day",
                        "month",
                        "year"
                    };

                    for (int i = 0; i < dataToPutInDb.Count; i++)
                    {
                        _ = DisplayError.Log($"{test[i]}: {dataToPutInDb[i]}");
                        var dataAsBytes = Encoding.UTF8.GetBytes(dataToPutInDb[i]);
                        var encryptedData = Security.EncryptDataAESDatabase(dataAsBytes, _key, _IV);
                        dataToPutInDb[i] = Convert.ToBase64String(encryptedData);
                        _ = DisplayError.Log($"{test[i]}: {dataToPutInDb[i]}");
                        _ = DisplayError.Log("///////////////////");
                    }

                    using (var cmd = new NpgsqlCommand(command, conn))
                    {
                        cmd.Parameters.AddWithValue("e", dataToPutInDb[0]);
                        cmd.Parameters.AddWithValue("u", dataToPutInDb[1]);
                        cmd.Parameters.AddWithValue("p", dataToPutInDb[2]);
                        cmd.Parameters.AddWithValue("fn", dataToPutInDb[3]);
                        cmd.Parameters.AddWithValue("ln", dataToPutInDb[4]);
                        cmd.Parameters.AddWithValue("d", dataToPutInDb[5]);
                        cmd.Parameters.AddWithValue("m", dataToPutInDb[6]);
                        cmd.Parameters.AddWithValue("y", dataToPutInDb[7]);
                        cmd.Parameters.AddWithValue("pic", imageBytes);
                        cmd.ExecuteNonQuery();
                    }

                    _ = DisplayError.Log("Put the user into the db");
                    return true;
                }
                catch (Exception ex)
                {
                    DisplayError.DisplayBasicErrorInfos(ex, "AccountInfosDatabase", "PutAccountIntoTheDb");
                    return false;
                }
            }
        }

        public static async Task<UserCheckResult?> CheckIfEmailOrUsernameExists(User user)
        {
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                var command = @" SELECT * FROM ""Users"" WHERE (email = @e AND username = @u)
                OR (email = @e AND username <> @u)  OR (email <> @e AND username = @u);";
                try
                {
                    await conn.OpenAsync();
                    _ = DisplayError.Log("Checking if the Email/ Username is already in the Database");
                    //Encrypting email
                    var dataAsBytes = Encoding.UTF8.GetBytes(user.Email);
                    var encrypted = Security.EncryptDataAESDatabase(dataAsBytes, _key, _IV);
                    var email = Convert.ToBase64String(encrypted);
                    //Encrypting username
                    dataAsBytes = Encoding.UTF8.GetBytes(user.Username);
                    encrypted = Security.EncryptDataAESDatabase(dataAsBytes, _key, _IV);
                    var username = Convert.ToBase64String(encrypted);
                    using (var cmd = new NpgsqlCommand(command, conn))
                    {
                        cmd.Parameters.AddWithValue("e", email);
                        cmd.Parameters.AddWithValue("u", username);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (!reader.HasRows)
                            {
                                return UserCheckResult.None;
                            }

                            while (await reader.ReadAsync())
                            {
                                var emailDb = reader["email"]?.ToString();
                                var usernameDb = reader["username"]?.ToString();

                                _ = DisplayError.Log($"Email from DB: {emailDb}");
                                _ = DisplayError.Log($"User Email: {user.Email}");
                                _ = DisplayError.Log($"Username from DB: {usernameDb}");
                                _ = DisplayError.Log($"User Username: {user.Username}");

                                if (emailDb == email && usernameDb == username)
                                {
                                    _ = DisplayError.Log("Both the email and the username are already in the database");
                                    return UserCheckResult.BothExists;
                                }
                                else if (emailDb == email)
                                {
                                    _ = DisplayError.Log("Only the email is already in the database");
                                    return UserCheckResult.EmailExists;
                                }
                                else if (usernameDb == username)
                                {
                                    _ = DisplayError.Log("Only the username is already in the database");
                                    return UserCheckResult.UsernameExists;
                                }
                            }
                            _ = DisplayError.Log("Neither were found in the Database");
                            return UserCheckResult.None;
                        }
                    }
                }
                catch (Exception ex)
                {
                    DisplayError.DisplayBasicErrorInfos(ex, "AccountInfoDatabase", "CheckIfEmailOrUsernameExists");
                    return null;
                }
            }
        }

        public static async Task<bool?> CheckIfUserExists(string username)
        {
            try
            {
                var command = @"SELECT username FROM ""Users"" WHERE username = @u";

                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    _ = DisplayError.Log("Checking if user exists");
                    var usernameBytes = Encoding.UTF8.GetBytes(username);
                    var encryptedBytes = Security.EncryptDataAESDatabase(usernameBytes, _key, _IV);
                    var encryptedUsername = Convert.ToBase64String(encryptedBytes);
                    using (var cmd = new NpgsqlCommand(command, conn))
                    {
                        cmd.Parameters.AddWithValue("@u", encryptedUsername);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                return true;
                            }
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DisplayError.DisplayBasicErrorInfos(ex, "AccountInfoDatabase", "CheckIfUserExists");
                return null;
            }
        }

        private static byte[] GetBytesFromImage(string filepath)
        {
            return File.ReadAllBytes(filepath);
        }
    }
}
