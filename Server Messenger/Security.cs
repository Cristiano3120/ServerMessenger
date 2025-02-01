using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using ZstdNet;

namespace Server_Messenger
{
    internal static class Security
    {
        private const string _pathToAesData = @"C:\Users\Crist\source\repos\Server Messenger\Server Messenger\NeededFiles\AESData.txt";
        private static RSAParameters _publicKey;
        private static Aes? _databaseAes;
        private static RSA? _rsa;

        #region Init
        public static void Init()
        {
            InitRSA();
            InitDatabaseAes();
        }

        private static void InitRSA()
        {
            _rsa = RSA.Create();
            _publicKey = _rsa.ExportParameters(false);
        }

        private static void InitDatabaseAes()
        {
            using var streamReader = new StreamReader(_pathToAesData);
            var password = streamReader.ReadLine()!;
            var salt = Encoding.UTF8.GetBytes(streamReader.ReadLine()!);

            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 500000, HashAlgorithmName.SHA256);
            _databaseAes = Aes.Create();
            _databaseAes.Key = pbkdf2.GetBytes(32);
            _databaseAes.IV = pbkdf2.GetBytes(16);
        }

        #endregion

        public static async Task SendClientRSA(WebSocket client)
        {
            var payload = new
            {
                code = OpCode.SendRSA,
                modulus = Convert.ToBase64String(_publicKey.Modulus!),
                exponent = Convert.ToBase64String(_publicKey.Exponent!),
            };
            await Server.SendPayloadAsync(client, payload, EncryptionMode.None);
        }

        #region Encryption

        public static TReturn EncryptAesDatabase<TParam, TReturn>(TParam dataToEncrypt) where TParam : class where TReturn : class
        {
            byte[] dataBytes = dataToEncrypt switch
            {
                byte[] bytes => bytes,
                string str => Encoding.UTF8.GetBytes(str),
                _ => throw new InvalidOperationException("TParam has an invalid type. Needs to be of type byte[] or string."),
            };

            using (var ms = new MemoryStream())
            {
                using (ICryptoTransform encryptor = _databaseAes!.CreateEncryptor())
                {
                    using (var cryptoStream = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        cryptoStream.Write(dataBytes, 0, dataBytes.Length);
                        cryptoStream.FlushFinalBlock();
                    }
                }

                return typeof(TReturn) == typeof(byte[])
                    ? (TReturn)(object)ms.ToArray()
                    : (TReturn)(object)Convert.ToBase64String(ms.ToArray());
            }
        }

        public static byte[] EncryptAes(WebSocket client, byte[] dataToEncrypt)
        {
            if (dataToEncrypt.Length == 0)
                throw new InvalidOperationException("Data to encrypt is empty.");

            if (Server.ClientsData.TryGetValue(client, out UserData? userdata))
            {
                (long _, Aes aes) = userdata;

                using (var ms = new MemoryStream())
                {
                    using (ICryptoTransform encryptor = aes.CreateEncryptor())
                    {
                        using (var cryptoStream = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                        {
                            cryptoStream.Write(dataToEncrypt, 0, dataToEncrypt.Length);
                            cryptoStream.FlushFinalBlock();
                        }
                    }

                    return ms.ToArray();
                }
            }

            throw new InvalidOperationException("Client data not found.");
        }

        #endregion

        #region Decryption

        public static byte[] DecryptMessage(WebSocket client, byte[] encryptedData)
        {
            try
            {
                return DecryptAes(client, encryptedData);
            }
            catch
            {
                return DecryptRSA(encryptedData);
            }
        }

        public static TReturn DecryptAesDatabase<TParam, TReturn>(TParam encryptedData) where TParam : class where TReturn : class
        {
            byte[] encryptedBytes = encryptedData switch
            {
                byte[] bytes => bytes,
                string str => Convert.FromBase64String(str),
                _ => throw new InvalidOperationException("TParam has an invalid type. Needs to be of type byte[] or string."),
            };

            using ICryptoTransform decryptor = _databaseAes!.CreateDecryptor();
            using var ms = new MemoryStream(encryptedBytes);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var resultStream = new MemoryStream();
            cs.CopyTo(resultStream);
            var decryptedBytes = resultStream.ToArray();

            TReturn? @return = default;
            return @return switch
            {
                _ when typeof(TReturn) == typeof(byte[]) => (TReturn)(object)decryptedBytes,
                _ when typeof(TReturn) == typeof(string) => (TReturn)(object)Encoding.UTF8.GetString(decryptedBytes),
                _ => throw new InvalidOperationException("TReturn has an invalid type. Needs to be of type byte[] or string."),
            };
        }

        public static byte[] DecryptAes(WebSocket client, byte[] encryptedData)
        {
            if (Server.ClientsData.TryGetValue(client, out UserData? userData))
            {
                (_, Aes aes) = userData;

                ICryptoTransform decryptor = aes.CreateDecryptor();

                using var ms = new MemoryStream(encryptedData);
                using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
                using var resultStream = new MemoryStream();
                cs.CopyTo(resultStream);

                return resultStream.ToArray();
            }

            throw new InvalidOperationException("Client data not found.");
        }

        private static byte[] DecryptRSA(byte[] encryptedData)
            => _rsa!.Decrypt(encryptedData, RSAEncryptionPadding.Pkcs1);

        #endregion

        #region Compress/ Decompress

        public static byte[] CompressData(byte[] data)
        {
            using var compressor = new Compressor(new CompressionOptions(1));
            var compressedData = compressor.Wrap(data);
            return compressedData.Length >= data.Length
                ? data
                : compressedData;
        }

        public static byte[] DecompressData(byte[] data)
        {
            try
            {
                using var decompressor = new Decompressor();
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
            using var rng = RandomNumberGenerator.Create();
            byte[] saltBytes = new byte[16];
            rng.GetBytes(saltBytes);

            using var pbkdf2 = new Rfc2898DeriveBytes(data, saltBytes, 100000, HashAlgorithmName.SHA256);
            byte[] dataHash = pbkdf2.GetBytes(32);

            //[..array, ..array] is the same as calling .Concat()
            byte[] dataSaltHash = [.. dataHash, .. saltBytes];
            return Convert.ToBase64String(dataSaltHash);
        }
    }
}
