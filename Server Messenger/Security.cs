using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using ZstdNet;

namespace Server_Messenger
{
    internal static class Security
    {
        public static ConcurrentDictionary<WebSocket, Aes> ClientAes { get; private set; } = new();
        private static readonly Aes _databaseAes = Aes.Create();
        private static readonly RSA _rsa = RSA.Create();

        static Security()
        {
            var password = Server.Config.GetProperty("DatabaseAes").GetProperty("password").GetString()!;
            var salt = Encoding.UTF8.GetBytes(Server.Config.GetProperty("DatabaseAes").GetProperty("salt").GetString()!);

            using (Rfc2898DeriveBytes pbkdf2 = new(password, salt, 500000, HashAlgorithmName.SHA256))
            {
                _databaseAes.Key = pbkdf2.GetBytes(32);
                _databaseAes.IV = pbkdf2.GetBytes(16);
            }
        }

        public static async Task SendClientRSAAsync(WebSocket client)
        {
            RSAParameters publicKey = _rsa.ExportParameters(false);
            var payload = new
            {
                opCode = OpCode.SendRSA,
                modulus = Convert.ToBase64String(publicKey.Modulus!),
                exponent = Convert.ToBase64String(publicKey.Exponent!),
            };

            await Server.SendPayloadAsync(client, payload, EncryptionMode.None);
        }

        #region Encryption

        public static async Task<TReturn> EncryptAesDatabaseAsync<TParam, TReturn>(TParam dataToEncrypt) where TParam : class where TReturn : class
        {
            byte[] dataBytes = dataToEncrypt switch
            {
                byte[] bytes => bytes,
                string str => Encoding.UTF8.GetBytes(str),
                _ => throw new InvalidOperationException("TParam has an invalid type. Needs to be of type byte[] or string."),
            };

            using (MemoryStream ms = new())
            {
                using (ICryptoTransform encryptor = _databaseAes!.CreateEncryptor())
                {
                    using (CryptoStream cryptoStream = new(ms, encryptor, CryptoStreamMode.Write, true))
                    {
                        await cryptoStream.WriteAsync(dataBytes);
                        await cryptoStream.FlushFinalBlockAsync();
                    }
                }

                return typeof(TReturn) == typeof(byte[])
                    ? (TReturn)(object)ms.ToArray()
                    : (TReturn)(object)Convert.ToBase64String(ms.ToArray());
            }
        }

        public static async Task<User> EncryptAesDatabaseAsync(User user)
            => new()
            {
                Username = await EncryptAesDatabaseAsync<string, string>(user.Username),
                Hashtag = await EncryptAesDatabaseAsync<string, string>(user.Hashtag),
                Email = await EncryptAesDatabaseAsync<string, string>(user.Email),
                Password = await EncryptAesDatabaseAsync<string, string>(user.Password),
                Biography = await EncryptAesDatabaseAsync<string, string>(user.Biography),
                ProfilePicture = await EncryptAesDatabaseAsync<byte[], byte[]>(user.ProfilePicture),
                Token = await EncryptAesDatabaseAsync<string, string>(user.Token),
                Birthday = user.Birthday!.Value,
                FaEnabled = user.FaEnabled,
                Id = user.Id,
            };

        public static async Task<Message> EncryptAesDatabaseAsync(Message message)
        {
            return message with
            {
                Content = await EncryptAesDatabaseAsync<string, string>(message.Content),
            };
        }

        public static async Task<byte[]> EncryptAesAsync(WebSocket client, byte[] dataToEncrypt)
        {
            if (dataToEncrypt.Length == 0)
                throw new InvalidOperationException("Data to encrypt is empty.");

            if (ClientAes.TryGetValue(client, out Aes? aes))
            {
                using (MemoryStream ms = new())
                {
                    using (ICryptoTransform encryptor = aes.CreateEncryptor())
                    {
                        using (CryptoStream cryptoStream = new(ms, encryptor, CryptoStreamMode.Write, true))
                        {
                            await cryptoStream.WriteAsync(dataToEncrypt);
                            await cryptoStream.FlushFinalBlockAsync();
                        }
                    }

                    return ms.ToArray();
                }
            }

            throw new InvalidOperationException("Client data not found.");
        }

        #endregion

        #region Decryption

        public static async Task<byte[]> DecryptMessageAsync(WebSocket client, byte[] encryptedData)
        {
            try
            {
                return await DecryptAesAsync(client, encryptedData);
            }
            catch
            {
                return DecryptRSA(encryptedData);
            }
        }

        public static async Task<TReturn> DecryptAesDatabaseAsync<TParam, TReturn>(TParam encryptedData) where TParam : class where TReturn : class
        {
            byte[] encryptedBytes = encryptedData switch
            {
                byte[] bytes => bytes,
                string str => Convert.FromBase64String(str),
                _ => throw new InvalidOperationException("TParam has an invalid type. Needs to be of type byte[] or string."),
            };

            using ICryptoTransform decryptor = _databaseAes!.CreateDecryptor();
            using (MemoryStream ms = new(encryptedBytes))
            {
                using (CryptoStream cs = new(ms, decryptor, CryptoStreamMode.Read))
                {
                    using MemoryStream resultStream = new();

                    await cs.CopyToAsync(resultStream);
                    var decryptedBytes = resultStream.ToArray();

                    TReturn? @return = default;
                    return @return switch
                    {
                        _ when typeof(TReturn) == typeof(byte[]) => (TReturn)(object)decryptedBytes,
                        _ when typeof(TReturn) == typeof(string) => (TReturn)(object)Encoding.UTF8.GetString(decryptedBytes),
                        _ => throw new InvalidOperationException("TReturn has an invalid type. Needs to be of type byte[] or string."),
                    };
                }
            }
        }

        /// <summary>
        /// Decrypts the user obj and removes sensible data from it
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public static async Task<User?> DecryptAesDatabaseAsync(User? user)
        {
            return user is null
                ? null
                : new()
                {
                    Username = await DecryptAesDatabaseAsync<string, string>(user.Username),
                    Hashtag = await DecryptAesDatabaseAsync<string, string>(user.Hashtag),
                    Email = await DecryptAesDatabaseAsync<string, string>(user.Email),
                    Password = "",
                    Biography = await DecryptAesDatabaseAsync<string, string>(user.Biography),
                    ProfilePicture = await DecryptAesDatabaseAsync<byte[], byte[]>(user.ProfilePicture),
                    Token = await DecryptAesDatabaseAsync<string, string>(user.Token),
                    Birthday = user.Birthday!.Value,
                    FaEnabled = user.FaEnabled,
                    Id = user.Id,
                };
        }

        public static async Task<List<Message>> DecryptAesDatabaseAsync(List<Message> messages)
        {
            List<Message> decryptedMessages = new();
            foreach (Message message in messages)
            {
                Message decryptedMessage = message with
                {
                    Content = await DecryptAesDatabaseAsync<string, string>(message.Content)
                };
                decryptedMessages.Add(decryptedMessage);
            }

            return decryptedMessages;
        }

        public static async Task<byte[]> DecryptAesAsync(WebSocket client, byte[] encryptedData)
        {
            if (ClientAes.TryGetValue(client, out Aes? aes))
            {
                ICryptoTransform decryptor = aes.CreateDecryptor();

                using (MemoryStream ms = new(encryptedData))
                {
                    using (CryptoStream cs = new(ms, decryptor, CryptoStreamMode.Read))
                    {
                        using MemoryStream resultStream = new();
                        await cs.CopyToAsync(resultStream);

                        return resultStream.ToArray();
                    }
                }
            }

            throw new InvalidOperationException("Client data not found.");
        }

        private static byte[] DecryptRSA(byte[] encryptedData)
            => _rsa!.Decrypt(encryptedData, RSAEncryptionPadding.Pkcs1);

        #endregion

        #region Compress/ Decompress

        public static byte[] CompressData(byte[] data)
        {
            using Compressor compressor = new(new CompressionOptions(1));
            var compressedData = compressor.Wrap(data);
            return compressedData.Length >= data.Length
                ? data
                : compressedData;
        }

        public static byte[] DecompressData(byte[] data)
        {
            try
            {
                using Decompressor decompressor = new();
                return decompressor.Unwrap(data);
            }
            catch (Exception)
            {
                return data;
            }
        }

        #endregion

        public static string Hash(string data)
        {
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                byte[] saltBytes = new byte[16];
                rng.GetBytes(saltBytes);

                using Rfc2898DeriveBytes pbkdf2 = new(data, saltBytes, 100000, HashAlgorithmName.SHA256);
                byte[] dataHash = pbkdf2.GetBytes(32);

                //[..array, ..array] is the same as calling .Concat()
                byte[] dataSaltHash = [.. dataHash, .. saltBytes];
                return Convert.ToBase64String(dataSaltHash);
            }
        }
    }
}
