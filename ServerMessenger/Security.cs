using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks.Sources;

namespace ServerMessenger
{
    /// <summary>
    /// Provides methods for encryption, decryption and more.
    /// </summary>
    internal static class Security
    {
        private static RSAParameters _publicServerRSAKey;
        private static RSAParameters _privateServerRSAKey;
        public static readonly Dictionary<TcpClient, Aes> _clientAes = new();
        public static readonly object _lock = new ();

        public static void Initialize()
        {
            _ = DisplayError.LogAsync("Initializing Security");
            GenerateRSAKeys();
        }

        #region Aes

        public static void ReceiveAes(TcpClient client, JsonElement root)
        {
            if (!client.Connected)
            {
                _ = DisplayError.LogAsync("Client disconnected!");
                return;
            }
            var aes = Aes.Create();
            aes.Key = root.GetProperty("Key").GetBytesFromBase64();
            aes.IV = root.GetProperty("IV").GetBytesFromBase64();
            lock (_lock)
            {
                _clientAes.Remove(client);
                _clientAes.Add(client, aes);
            }
            _ = DisplayError.LogAsync("Saved Clients Aes key");

            var payload = new
            {
                code = 16,
            };
            var jsonString = JsonSerializer.Serialize(payload);
            _ = Server.SendPayloadAsync(client, jsonString);
        }

        public static void RemoveAes(TcpClient client)
        {
            _ = DisplayError.LogAsync("Removing the client from the dict");
            lock (_lock)
            {
                _clientAes.Remove(client);
            }
        }
        #endregion

        #region Encryption

        public static byte[] EncryptDataAes(TcpClient client, byte[] data)
        {
            using (Aes aes = Aes.Create())
            {
                lock (_lock)
                {
                    var clientAes = _clientAes.GetValueOrDefault(client) ?? throw new KeyNotFoundException("The client is not in the dictionary");
                    aes.Key = clientAes.Key;
                    aes.IV = clientAes.IV;
                }
                
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
            _ = DisplayError.LogAsync("Sending RSA key to the client");
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
                        decryptionMode--;
                        _ = DisplayError.LogAsync($"Error(Security.DecryptMessage(): {ex.Message})");
                        _ = DisplayError.LogAsync($"Error(Security.DecryptMessage): Couldnt decrypt the data." +
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
            Aes? clientAes;
            lock (_lock)
            {
                _clientAes.TryGetValue(client, out clientAes);
            }
            
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
