using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ServerMessenger
{
    /// <summary>
    /// The generel logic for the Server.
    /// </summary>
    internal static class Server
    {
        public static Dictionary<string, TcpClient> _clients { get; private set; } = new();
        public static readonly object _lockClientsDict = new();
        public static readonly object _lockAwaitingMessagesDict = new();

        public static void StartServer()
        {
            DisplayError.Initialize();
            _ = DisplayError.Log("Starting Server");
            HandleClientMessages.Initialize();
            Security.Initialize();
            AccountInfoDatabase.Initialize();
            Task.Run(() => AcceptClients());
        }

        public static void StopServer(string reason)
        {
            _ = DisplayError.Log("Stopping the Server");
            throw new Exception(reason);
        }

        public static async Task SendPayloadAsync(TcpClient client, string payload, EncryptionMode encryption = EncryptionMode.AES)
        {
            try
            {
                _ = DisplayError.Log($"Sending: {payload}");
                _ = DisplayError.Log($"Trying to send {encryption} encrypted data");
                var buffer = payload != null ? Encoding.UTF8.GetBytes(payload) : throw new ArgumentNullException(nameof(payload));
                if (encryption == EncryptionMode.AES)
                {
                    _ = DisplayError.Log("Encrypting data.");
                    buffer = Security.EncryptDataAes(client, buffer);
                }
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
                        _ = DisplayError.Log("Accepting a client");
                        var client = listener.AcceptTcpClient();
                        _ = Task.Run(() => { _ = HandleClientAsync(client); });
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
            var buffer = new byte[8092];
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
                    _ = DisplayError.Log($"Received code {code}");
                    _ = DisplayError.Log($"Received: {root}");
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
                        case 10: //Request to add a friend (USER THAT NEEDS TO GET THE REQUEST COULD BE OFFLINE)
                            _ = HandleClientMessages.HandleFriendRequest(client, root);
                            break;
                        case 14: //Accept or decline request
                            var userId = root.GetProperty("userId").GetInt32();
                            var friendId = root.GetProperty("friendId").GetInt32();
                            var taskByte = root.GetProperty("task").GetByte();
                            Console.WriteLine((RelationshipStateEnum)taskByte);
                            _ = AccountInfoDatabase.UpdateRelationshipState(userId, friendId, (RelationshipStateEnum)taskByte);
                            break;
                    }
                }
                catch (ArgumentNullException ex)
                {
                    DisplayError.ArgumentNullException(ex, "Server", "SendPayloadAsync()");
                }
                catch (Exception ex)
                {
                    _ = DisplayError.Log($"Error(ListenForMessages(): {ex.Message})");
                }
            }
            ClosingConnectionToClient(client, username);
            _ = DisplayError.Log("Lost connection to the Client");
        }

        private static void ClosingConnectionToClient(TcpClient client, string username)
        {
            client.Close();
            if (username != string.Empty)
            {
                _ = DisplayError.Log($"Removing {username} from the online dict");
                lock (_lockClientsDict)
                {
                    _clients.Remove(username);
                }
            }
            Security.RemoveAes(client);
        }

        private static IPAddress GetIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    _ = DisplayError.Log("Server IP Address: " + ip);
                    return ip;
                }
            }
            throw new Exception("No IPv4 address found.");
        }
    }
}
