﻿using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ServerMessenger
{
    /// <summary>
    /// The generel logic for the Server.
    /// </summary>
    internal static class Server
    {
        public static Dictionary<string, TcpClient> Clients { get; private set; } = new();
        public static readonly object lockClientsDict = new();

        public static void StartServer()
        {
            DisplayError.Initialize();
            _ = DisplayError.LogAsync("Starting Server");
            HandleClientMessages.Initialize();
            Security.Initialize();
            AccountInfoDatabase.Initialize();
            ChatsDatabse.Initialize();
            Task.Run(AcceptClients);
        }

        /// <summary>
        /// Stops the Server and logs the reason.
        /// </summary>
        public static void StopServer(string reason)
        {
            _ = DisplayError.LogAsync(reason);
            _ = DisplayError.LogAsync("Stopping the Server");
            throw new Exception(reason);
        }

        public static async Task SendPayloadAsync(TcpClient client, string payload, EncryptionMode encryption = EncryptionMode.AES)
        {
            try
            {
                _ = DisplayError.LogAsync($"Sending: {payload}");

                if (client == null || client.Connected == false) throw new ArgumentNullException(nameof(client));
                var buffer = payload != null ? Encoding.UTF8.GetBytes(payload) : throw new ArgumentNullException(nameof(payload));

                if (encryption == EncryptionMode.AES)
                {
                    _ = DisplayError.LogAsync("Encrypting data.");
                    buffer = Security.EncryptDataAes(client, buffer);
                }
                Console.WriteLine($"Buffer length: {buffer.Length}");
                await client.Client.SendAsync(buffer);
            }
            catch (SocketException ex)
            {
                DisplayError.SocketException(ex, "Server", "SendPayloadAsync()");
            }
            catch (ArgumentNullException ex)
            {
                DisplayError.ArgumentNullException(ex, "Server", "SendPayloadAsync()");
            }
            catch (Exception ex)
            {
                DisplayError.DisplayBasicErrorInfos(ex, "Server", "SendPayloadAsync()");
            }
        }

        private static void AcceptClients()
        {
            try
            {
                var listener = new TcpListener(GetIPAddress(), 50000);
                listener.Start();
                while (true)
                {
                    if (listener.Pending())
                    {
                        _ = DisplayError.LogAsync("Accepting a client");
                        var client = listener.AcceptTcpClient();
                        _ = Task.Run(() => _ = HandleClientAsync(client));
                    }
                }
            }
            catch (SocketException ex)
            {
                DisplayError.SocketException(ex, "Server", "AcceptClients()");
            }
            catch (Exception ex)
            {
                DisplayError.DisplayBasicErrorInfos(ex, "Server", "AcceptClients()");
            }
        }

        private static async Task HandleClientAsync(TcpClient client)
        {
            Security.SendClientRSAkey(client);
            var buffer = new byte[65536];
            var username = string.Empty;
            while (client.Connected)
            {
                try
                {
                    int bytesRead = await client.Client.ReceiveAsync(buffer);
                    var tempBuffer = new byte[bytesRead];
                    Array.Copy(buffer, tempBuffer, bytesRead);
                    var root = Security.DecryptMessage(client, tempBuffer) ?? throw new ArgumentNullException(nameof(tempBuffer));
                    var code = root.GetProperty("code").GetByte();
                    _ = DisplayError.LogAsync($"Received code {code}");
                    _ = DisplayError.LogAsync($"Received: {root}");
                    switch (code)
                    {
                        case 1: //Receiving Aes key
                            Security.ReceiveAes(client, root);
                            break;
                        case 2: //Receiving request to create an Acc
                            _ = HandleClientMessages.HandleCreateAccount(client, root);
                            break;
                        case 5: //Receiving request to send a verification email
                            HandleClientMessages.HandleVerificationProcess(client, root);
                            break;
                        case 6: //Request to put the user into the Db
                            _ = HandleClientMessages.VerificationSuccesful(client, root);
                            break;
                        case 8: //Request to login
                            _ = HandleClientMessages.HandleLogin(client, root);
                            break;
                        case 10: //Request to add a friend
                            _ = HandleClientMessages.HandleFriendRequest(client, root);
                            break;
                        case 14: //Request to change a RelationshipState
                            _ = HandleClientMessages.HandleRelationShipStateUpdate(root);
                            break;
                        case 15: //Reiceiving profil pic
                            var id = root.GetProperty("id").GetInt32();
                            var imageBytes = root.GetProperty("base64Image").GetBytesFromBase64();
                            _ = AccountInfoDatabase.ChangeProfilPic(id, imageBytes);
                            break;
                        case 18: //A user sent a message to another user
                            _ = HandleClientMessages.HandleSentTextMessage(root);
                            break;
                    }
                }
                catch (ArgumentNullException ex)
                {
                    DisplayError.ArgumentNullException(ex, "Server", "SendPayloadAsync()");
                }
                catch (Exception ex)
                {
                    _ = DisplayError.LogAsync($"Error(ListenForMessages(): {ex.Message})");
                }
            }
            ClosingConnectionToClient(client, username);
            _ = DisplayError.LogAsync("Lost connection to the Client");
        }

        private static void ClosingConnectionToClient(TcpClient client, string username)
        {
            if (username != string.Empty)
            {
                _ = DisplayError.LogAsync($"Removing {username} from the online dict");
                lock (lockClientsDict)
                {
                    Clients.Remove(username);
                }
            }
            Security.RemoveAes(client);
            client.Close();
            Console.Clear();
        }

        private static IPAddress GetIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    _ = DisplayError.LogAsync("Server IP Address: " + ip);
                    return ip;
                }
            }
            throw new Exception("No IPv4 address found.");
        }
    }
}
