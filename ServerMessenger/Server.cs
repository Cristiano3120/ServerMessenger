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
        #pragma warning disable CS8618  

        public static void StartServer()
        {
            Console.WriteLine("Starting Server");
            HandleClientMessages.Initialize();
            Security.Initialize();
            AccountInfoDatabase.Initialize();
            Task.Run(() => AcceptClients());
        }

        public static void StopServer(string reason)
        {
            Console.WriteLine("Stopping the Server");
            throw new NotImplementedException(reason);
        }

        public static async Task SendPayloadAsync(TcpClient client, string payload, EncryptionMode encryption = EncryptionMode.AES)
        {
            try
            {
                Console.WriteLine(payload);
                Console.WriteLine($"Trying to send {encryption} encrypted data");
                var buffer = payload != null ? Encoding.UTF8.GetBytes(payload) : throw new ArgumentNullException(nameof(payload));
                switch (encryption)
                {
                    case EncryptionMode.AES:
                        Security.EncryptDataAes(client, buffer);
                        break;
                }
                await client.Client.SendAsync(buffer);
            }
            catch (SocketException ex)
            {
                DisplayError.SocketException(ex, "Client", "SendPayloadAsync()");
            }
            catch (ObjectDisposedException ex)
            {
                DisplayError.ObjectDisposedException(ex, "Client", "SendPayloadAsync()");
            }
            catch (ArgumentNullException)
            {
                Console.WriteLine($"Error(Client.SendPayloadAsync(): Payload was null)");
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
                        Console.WriteLine("Accepting a client");
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
            while (client.Connected)
            {
                try
                {
                    int bytesRead = await client.Client.ReceiveAsync(buffer);
                    var tempBuffer = new byte[bytesRead];
                    Array.Copy(buffer, tempBuffer, bytesRead);
                    var root = Security.DecryptMessage(client, tempBuffer) ?? throw new Exception("Root was null");
                    var code = root.GetProperty("code").GetByte();
                    Console.WriteLine($"Received code {code}");
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
                    }
                }
                catch (NotImplementedException ex)
                {
                    Console.WriteLine($"Error(ListenForMessages(): {ex.Message})");
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error(ListenForMessages(): {ex.Message})");
                }
            }
            Security.RemoveAes(client);
            Console.WriteLine("Lost connection to the Client");
        }
        
        private static IPAddress GetIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    Console.WriteLine("Server IP Address: " + ip);
                    return ip;
                }
            }
            throw new Exception("No IPv4 address found.");
        }
    }
}
