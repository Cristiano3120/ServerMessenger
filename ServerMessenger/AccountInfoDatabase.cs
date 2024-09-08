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

                Console.WriteLine("Files exist.");
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
                Console.WriteLine("Reading the connection string");
                _connectionString = reader.ReadLine();
            }
            catch (Exception ex)
            {
                DisplayError.DisplayBasicErrorInfos(ex, "AccountInfoBase", "ReadAesAndConnectionString()");
            }
        }

        public static async Task<bool?> CheckLoginDataAsync(string email, string password)
        {
            try
            {
                var command = "SELECT Password FROM \"Users\" WHERE Email = @e";
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    var encryptedData = Security.EncryptDataAESDatabase(Encoding.UTF8.GetBytes(email), _key, _IV);
                    var encryptedEmail = Convert.ToBase64String(encryptedData);
                    encryptedData = Security.EncryptDataAESDatabase(Encoding.UTF8.GetBytes(password), _key, _IV);
                    var encryptedPassword = Convert.ToBase64String(encryptedData);
                    using (var cmd = new NpgsqlCommand(command, conn))
                    {
                        cmd.Parameters.AddWithValue("@e", encryptedEmail);
                        var reader = await cmd.ExecuteReaderAsync();
                        if (reader.HasRows)
                        {
                            await reader.ReadAsync();
                            Console.WriteLine(encryptedPassword);
                            if (encryptedPassword == reader.GetString(0))
                            {
                                return true;
                            }
                        }
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                DisplayError.DisplayBasicErrorInfos(ex, "AccountInfoDatabase", "CheckLoginData");
                return null;
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

                    Console.WriteLine("Connection to the database was sucessfull");
                    string command = "INSERT INTO \"Users\" (email, username, password, firstName, lastName, day, month, year) VALUES (@e, @u, @p, @fN, @lN, @d, @m, @y)";

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
                        Console.WriteLine($"{test[i]}: {dataToPutInDb[i]}");
                        var dataAsBytes = Encoding.UTF8.GetBytes(dataToPutInDb[i]);
                        var encryptedData = Security.EncryptDataAESDatabase(dataAsBytes, _key, _IV);
                        dataToPutInDb[i] = Convert.ToBase64String(encryptedData);
                        Console.WriteLine($"{test[i]}: {dataToPutInDb[i]}");
                        Console.WriteLine("///////////////////");
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
                        cmd.ExecuteNonQuery();
                    }

                    Console.WriteLine("Put the user into the db");
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
                    Console.WriteLine("Checking if the Email/ Username is already in the Database");
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

                                Console.WriteLine($"Email from DB: {emailDb}");
                                Console.WriteLine($"User Email: {user.Email}");
                                Console.WriteLine($"Username from DB: {usernameDb}");
                                Console.WriteLine($"User Username: {user.Username}");

                                if (emailDb == email && usernameDb == username)
                                {
                                    Console.WriteLine("Both the email and the username are already in the database");
                                    return UserCheckResult.BothExists;
                                }
                                else if (emailDb == email)
                                {
                                    Console.WriteLine("Only the email is already in the database");
                                    return UserCheckResult.EmailExists;
                                }
                                else if (usernameDb == username)
                                {
                                    Console.WriteLine("Only the username is already in the database");
                                    return UserCheckResult.UsernameExists;
                                }
                            }
                            Console.WriteLine("Neither were found in the Database");
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
    }
}
