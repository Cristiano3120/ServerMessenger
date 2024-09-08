using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ServerMessenger
{
    /// <summary>
    /// Provides methods for encryption, decryption and more.
    /// </summary>
    internal static class Security
    {
        private static RSAParameters _publicServerRSAKey;
        private static RSAParameters _privateServerRSAKey;
        private static readonly Dictionary<TcpClient, Aes> _clientAes = new();

        public static void Initialize()
        {
            GenerateRSAKeys();
        }

        #region Aes

        public static void ReceiveAes(TcpClient client, JsonElement root)
        {
            if (!client.Connected)
            {
                Console.WriteLine("Client disconnected!");
                return;
            }
            var aes = Aes.Create();
            aes.Key = root.GetProperty("Key").GetBytesFromBase64();
            aes.IV = root.GetProperty("IV").GetBytesFromBase64();
            _clientAes.Add(client, aes);
            Console.WriteLine("Saved clients Aes key");
        }

        public static void RemoveAes(TcpClient client)
        {
            Console.WriteLine("Removing the client from the dict");
            _clientAes.Remove(client);
        }
        #endregion

        #region Encryption

        public static byte[] EncryptDataAes(TcpClient client, byte[] data)
        {
            using (Aes aes = Aes.Create())
            {
                var clientAes = _clientAes.GetValueOrDefault(client) ?? throw new KeyNotFoundException("The client is not in the dictionary");
                aes.Key = clientAes.Key;
                aes.IV = clientAes.IV;

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        csEncrypt.Write(data, 0, data.Length);
                        csEncrypt.FlushFinalBlock();
                        return msEncrypt.ToArray();
                    }
                }
            }
        }

        public static byte[] EncryptDataAESDatabase(byte[] data, byte[] key, byte[] iv)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;

                using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                using (var msEncrypt = new MemoryStream())
                using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                {
                    csEncrypt.Write(data, 0, data.Length);
                    csEncrypt.FlushFinalBlock();
                    return msEncrypt.ToArray();
                }
            }
        }

        #endregion

        #region Generate keys

        private static void GenerateRSAKeys()
        {
            using (var rsa = new RSACryptoServiceProvider(2048))
            {
                rsa.PersistKeyInCsp = false;
                _publicServerRSAKey = rsa.ExportParameters(false);
                _privateServerRSAKey = rsa.ExportParameters(true);
            }
        }
        #endregion

        #region Send keys

        public static void SendClientRSAkey(TcpClient client)
        {
            var modulus = Convert.ToBase64String(_publicServerRSAKey.Modulus!);
            var exponent = Convert.ToBase64String(_publicServerRSAKey.Exponent!);
            var payload = new
            {
                code = 0,
                modulus,
                exponent,
            };
            var jsonString = JsonSerializer.Serialize(payload);
            Console.WriteLine("Sending RSA key to the client");
            _ = Server.SendPayloadAsync(client, jsonString, EncryptionMode.None);
        }
        #endregion

        #region Decryption

        public static JsonElement? DecryptMessage(TcpClient client, byte[] buffer)
        {
            var decryptionMode = EncryptionMode.AES;
            while (decryptionMode >= EncryptionMode.None)
            {
                try
                {
                    string data = decryptionMode switch
                    {
                        EncryptionMode.AES => DecryptDataAES(client, buffer),
                        EncryptionMode.RSA => DecryptDataRSA(buffer),
                        EncryptionMode.None => Encoding.UTF8.GetString(buffer),
                        _ => throw new NotSupportedException("This encryption mode isnt supported at the moment!")
                    };
                    return JsonDocument.Parse(data).RootElement;
                }
                catch (Exception ex) when (ex is CryptographicException || ex is JsonException)
                {
                    if (decryptionMode > 0)
                    {
                        Console.WriteLine($"Error(Security.DecryptMessage(): {ex.Message})");
                        decryptionMode--;
                        Console.WriteLine($"Error(Security.DecryptMessage): Couldnt decrypt the data." +
                        $"Trying again with {decryptionMode} decryption");
                    }
                }
            }
            return null;
        }

        public static string DecryptDataAES(byte[] data, byte[] key, byte[] iv)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;

                using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                using (var msDecrypt = new MemoryStream(data))
                using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                using (var srDecrypt = new StreamReader(csDecrypt))
                {
                    return srDecrypt.ReadToEnd();
                }
            }
        }

        private static string DecryptDataAES(TcpClient client, byte[] encryptedData)
        {
            _clientAes.TryGetValue(client, out var clientAes);
            if (clientAes is null)
            {
                throw new CryptographicException("Aes isnt in the dict yet");
            }

            using (var decryptor = clientAes.CreateDecryptor(clientAes.Key, clientAes.IV))
            using (var msDecrypt = new System.IO.MemoryStream(encryptedData))
            using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
            using (var srDecrypt = new System.IO.StreamReader(csDecrypt))
            {
                return srDecrypt.ReadToEnd();
            }
        }

        private static string DecryptDataRSA(byte[] encryptedData)
        {
            using (var rsa = RSA.Create())
            {
                rsa.ImportParameters(_privateServerRSAKey);
                var data = rsa.Decrypt(encryptedData, RSAEncryptionPadding.OaepSHA256);
                return Encoding.UTF8.GetString(data);
            }
        }
        #endregion 
    }
}
