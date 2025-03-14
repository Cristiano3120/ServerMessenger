﻿using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using ZstdNet;

namespace Server_Messenger
{
    internal static class Security
    {
        public static ConcurrentDictionary<WebSocket, Aes> ClientAes { get; private set; } = new();
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
            var password = Server.Config.GetProperty("DatabaseAes").GetProperty("password").GetString()!;
            var salt = Encoding.UTF8.GetBytes(Server.Config.GetProperty("DatabaseAes").GetProperty("salt").GetString()!);

            using Rfc2898DeriveBytes pbkdf2 = new(password, salt, 500000, HashAlgorithmName.SHA256);
            _databaseAes = Aes.Create();
            _databaseAes.Key = pbkdf2.GetBytes(32);
            _databaseAes.IV = pbkdf2.GetBytes(16);
        }

        #endregion

        public static async Task SendClientRSAAsync(WebSocket client)
        {
            var payload = new
            {
                opCode = OpCode.SendRSA,
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

            using (MemoryStream ms = new())
            {
                using (ICryptoTransform encryptor = _databaseAes!.CreateEncryptor())
                {
                    using (CryptoStream cryptoStream = new(ms, encryptor, CryptoStreamMode.Write))
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

        public static User EncryptAesDatabase(User user)
            => new()
            {
                Username = EncryptAesDatabase<string, string>(user.Username),
                HashTag = EncryptAesDatabase<string, string>(user.HashTag),
                Email = EncryptAesDatabase<string, string>(user.Email),
                Password = EncryptAesDatabase<string, string>(user.Password),
                Biography = EncryptAesDatabase<string, string>(user.Biography),
                ProfilePicture = EncryptAesDatabase<byte[], byte[]>(user.ProfilePicture),
                Token = EncryptAesDatabase<string, string>(user.Token),
                Birthday = user.Birthday!.Value,
                FaEnabled = user.FaEnabled,
                Id = user.Id,
            };


        public static byte[] EncryptAes(WebSocket client, byte[] dataToEncrypt)
        {
            if (dataToEncrypt.Length == 0)
                throw new InvalidOperationException("Data to encrypt is empty.");

            if (ClientAes.TryGetValue(client, out Aes? aes))
            {
                using (MemoryStream ms = new())
                {
                    using (ICryptoTransform encryptor = aes.CreateEncryptor())
                    {
                        using (CryptoStream cryptoStream = new(ms, encryptor, CryptoStreamMode.Write))
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
            using (MemoryStream ms = new(encryptedBytes))
            {
                using (CryptoStream cs = new(ms, decryptor, CryptoStreamMode.Read))
                {
                    using MemoryStream resultStream = new();

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
            }   
        }

        /// <summary>
        /// Decrypts the user obj and removes sensible data from it
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public static User? DecryptAesDatabase(User? user)
        {
            if (user == null)
                return null;

            return new()
            {
                Username = DecryptAesDatabase<string, string>(user.Username),
                HashTag = DecryptAesDatabase<string, string>(user.HashTag),
                Email = DecryptAesDatabase<string, string>(user.Email),
                Password = "",
                Biography = DecryptAesDatabase<string, string>(user.Biography),
                ProfilePicture = DecryptAesDatabase<byte[], byte[]>(user.ProfilePicture),
                Token = DecryptAesDatabase<string, string>(user.Token),
                Birthday = user.Birthday!.Value,
                FaEnabled = user.FaEnabled,
                Id = user.Id,
            };
        }

        public static byte[] DecryptAes(WebSocket client, byte[] encryptedData)
        {
            if (ClientAes.TryGetValue(client, out Aes? aes))
            {
                ICryptoTransform decryptor = aes.CreateDecryptor();

                using (MemoryStream ms = new(encryptedData))
                {
                    using (CryptoStream cs = new(ms, decryptor, CryptoStreamMode.Read))
                    {
                        using MemoryStream resultStream = new();
                        cs.CopyTo(resultStream);

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
