using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Server_Messenger
{
    internal static class Server
    {
        private static readonly ConcurrentDictionary<long, WebSocket> _clients = new();
        public static ConcurrentDictionary<WebSocket, UserData> ClientsData { get; private set; } = new();
        public static JsonSerializerOptions JsonSerializerOptions { get; private set; } = new();
        public const string _pathToConfig = @"C:\Users\Crist\source\repos\Server Messenger\Server Messenger\Settings\appsettings.json";

        public static void Start()
        {
            Logger.LogWarning("Starting the Server!");

            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                Logger.LogWarning("WARNING: CATCHED AN UNHANDELD EXCEPTION!");
                Logger.LogError(args);
                args.SetObserved();
            };

            JsonSerializerOptions.Converters.Add(new JsonConverters.UserConverter());
            JsonSerializerOptions.WriteIndented = true;

            Task.Run(ListenForConnections);
            Security.Init();
        }

        public static async Task Shutdown()
        {
            Logger.LogWarning("Server is shutting down!");
            foreach (var client in _clients.Values)
            {
                await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server shutting down!", CancellationToken.None);
            }
        }

        #region Handle clients

        private static async Task ListenForConnections()
        {
            HttpListener listener = new();
            listener.Prefixes.Add(GetUri(true));
            listener.Start();

            while (listener.IsListening)
            {
                HttpListenerContext context = await listener.GetContextAsync();
                if (context.Request.IsWebSocketRequest)
                {
                    Logger.LogInformation("Accepting a client");
                    HttpListenerWebSocketContext webSocketContext = await context.AcceptWebSocketAsync(null);
                    WebSocket client = webSocketContext.WebSocket;

                    await Security.SendClientRSA(client);
                    _ = HandleClient(client);
                }
            }
        }

        private static async Task HandleClient(WebSocket client)
        {
            var buffer = new byte[65536];
            var ms = new MemoryStream();

            while (client.State == WebSocketState.Open)
            {
                try
                {
                    var receivedDataInfo = await client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    Logger.LogInformation(ConsoleColor.Cyan, $"[RECEIVED]: The received payload is {receivedDataInfo.Count} bytes long");

                    await ms.WriteAsync(buffer.AsMemory(0, receivedDataInfo.Count));
                    if (!receivedDataInfo.EndOfMessage)
                    {
                        continue;
                    }

                    byte[] completeBytes = ms.ToArray();
                    byte[] decryptedData = Security.DecryptMessage(client, completeBytes);
                    byte[] decompressedBytes = Security.DecompressData(decryptedData);
                    var completeMessage = Encoding.UTF8.GetString(decompressedBytes);

                    Logger.LogInformation(ConsoleColor.Green, logs: $"[RECEIVED]: {completeMessage}");
                    ClearMs(ms);

                    await HandleReceivedMessage(client, JsonDocument.Parse(completeMessage).RootElement);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex);
                    var cts = new CancellationTokenSource();
                    cts.CancelAfter(TimeSpan.FromSeconds(10));
                    await client.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, null, cts.Token);
                    cts.Dispose();
                    break;
                }
            }
            ClosingConn(client);
        }

        private static async Task HandleReceivedMessage(WebSocket client, JsonElement message)
        {
            var code = message.GetProperty("code").GetOpCode();
            try
            {
                switch (code)
                {
                    case OpCode.ReceiveAes:
                        await HandleUserRequests.ReceivedAes(client, message);
                        break;
                    case OpCode.RequestToCreateAccount:
                        Logger.LogInformation("Received RequestToCreateAccount");
                        await HandleUserRequests.CreateAccount(client, message);
                        break;
                    case OpCode.RequestLogin:
                        await HandleUserRequests.RequestToLogin(client, message);
                        break;
                }
            }
            catch (InvalidOperationException ex)
            {
                Logger.LogError(ex);
                var cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromSeconds(10));
                await client.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, null, cts.Token);
                cts.Dispose();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }
        }

        #region Clean up

        private static void ClearMs(MemoryStream ms)
        {
            var buffer = ms.GetBuffer();
            Array.Clear(buffer, 0, buffer.Length);
            ms.Position = 0;
            ms.SetLength(0);
        }

        public static void ClosingConn(WebSocket client)
        {
            Logger.LogInformation("Cleaning up the connection");
            if (ClientsData.TryRemove(client, out UserData? userData))
                _clients.TryRemove(userData.Id, out _);
            client.Dispose();
        }

        #endregion

        #endregion

        internal static async Task SendPayloadAsync(WebSocket client, object payload, EncryptionMode encryptionMode = EncryptionMode.Aes)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(payload);

                if (client.State != WebSocketState.Open)
                {
                    return;
                }

                var jsonPayload = JsonSerializer.Serialize(payload, JsonSerializerOptions);
                var buffer = Encoding.UTF8.GetBytes(jsonPayload);
                var compressedData = Security.CompressData(buffer);

                if (encryptionMode == EncryptionMode.Aes)
                    buffer = Security.EncryptAes(client, compressedData);

                await client.SendAsync(buffer, WebSocketMessageType.Binary, true, CancellationToken.None);

                Logger.LogInformation(ConsoleColor.Blue, $"[SENDING(Aes)]: {jsonPayload}");
                Logger.LogInformation($"Buffer length: {buffer.Length}");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                await client.CloseAsync(WebSocketCloseStatus.Empty, null, CancellationToken.None);
            }
        }

        #region Start Server helpers

        private static string GetUri(bool testing)
        {
            return testing
                ? "http://127.0.0.1:5000/"
                : StartNgrok();
        }

        private static string StartNgrok()
        {
            ProcessStartInfo processInfo = new()
            {
                FileName = @"C:\Users\Crist\source\repos\Server Messenger\Server Messenger\NeededFiles\StartServer.bat",
                UseShellExecute = true,
                CreateNoWindow = false
            };

            _ = Process.Start(processInfo)
                ?? throw new InvalidOperationException("Something went wrong while trying to execute the .bat file that starts the webserver");

            Logger.LogWarning($"Started the webserver!");
            var jsonFileContent = File.ReadAllText(_pathToConfig);
            JsonElement jsonElement = JsonDocument.Parse(jsonFileContent).RootElement;
            return jsonElement.GetProperty("ServerUri").GetString() ?? throw new JsonException("The server uri couldn´t be accessed");
        }

        #endregion
    }
}
