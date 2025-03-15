using System.Net.Mail;
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
        public static ConcurrentDictionary<long, WebSocket> Clients { get; private set; } = new();
        public static ConcurrentDictionary<WebSocket, VerificationInfos> VerificationCodes { get; private set; } = new();
        public static JsonSerializerOptions JsonSerializerOptions { get; private set; } = new();
        public static JsonElement Config { get; private set; } = JsonExtensions.ReadConfig();
        private static readonly string _emailPassword = ReadEmailPassword();

        public static async Task Start()
        {
            Logger.LogWarning("Starting the Server!");

            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                Logger.LogWarning("WARNING: CATCHED AN UNHANDELD EXCEPTION!");
                Logger.LogError(args);
                args.SetObserved();
            };

            JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            JsonSerializerOptions.Converters.Add(new JsonConverters.UserConverter());
            JsonSerializerOptions.Converters.Add(new JsonConverters.RelationshipConverter());
            JsonSerializerOptions.WriteIndented = true;

            _ = Task.Run(ListenForConnectionsAsync);
            Security.Init();

            PersonalDataDatabase personalDataDatabase = new();
            await personalDataDatabase.AddTestUsersToDb();
        }

        public static async Task ShutdownAsync()
        {
            Logger.LogWarning("Server is shutting down!");
            foreach (WebSocket client in Security.ClientAes.Keys)
            {
                await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server shutting down!", CancellationToken.None);
            }
        }

        #region Handle clients

        private static async Task ListenForConnectionsAsync()
        {
            Logger.LogWarning("Listening for Clients");

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

                    await Security.SendClientRSAAsync(client);
                    _ = HandleClientAsync(client);
                }
            }
        }

        private static async Task HandleClientAsync(WebSocket client)
        {
            var buffer = new byte[65536];
            MemoryStream ms = new();

            while (client.State == WebSocketState.Open)
            {
                try
                {
                    WebSocketReceiveResult receivedDataInfo = await client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    Logger.LogInformation(ConsoleColor.Cyan, false, $"[RECEIVED]: The received payload is {receivedDataInfo.Count} bytes long");

                    if (receivedDataInfo.MessageType == WebSocketMessageType.Close)
                    {
                        await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                        break;
                    }

                    await ms.WriteAsync(buffer.AsMemory(0, receivedDataInfo.Count));
                    if (!receivedDataInfo.EndOfMessage)
                    {
                        continue;
                    }

                    byte[] completeBytes = ms.ToArray();
                    byte[] decryptedData = Security.DecryptMessage(client, completeBytes);
                    byte[] decompressedBytes = Security.DecompressData(decryptedData);
                    var completeMessage = Encoding.UTF8.GetString(decompressedBytes);

                    Logger.LogPayload(ConsoleColor.Green, completeMessage, "[RECEIVED]:");
                    ClearMs(ms);

                    await HandleReceivedMessageAsync(client, JsonDocument.Parse(completeMessage).RootElement);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex);
                    await client.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, null, CancellationToken.None);
                    break;
                }
            }
            await ClosingConnAsync(client);
        }

        private static async Task HandleReceivedMessageAsync(WebSocket client, JsonElement message)
        {
            OpCode code = message.GetOpCode();
            try
            {
                switch (code)
                {
                    case OpCode.ReceiveAes:
                        await HandleUserRequests.ReceivedAesAsync(client, message);
                        break;
                    case OpCode.RequestToCreateAccount:
                        Logger.LogInformation("Received RequestToCreateAccount");
                        await HandleUserRequests.CreateAccountAsync(client, message);
                        break;
                    case OpCode.RequestToLogin:
                        await HandleUserRequests.RequestToLoginAsync(client, message);
                        break;
                    case OpCode.RequestToVerifiy:
                        await HandleUserRequests.RequestToVerifyAsync(client, message);
                        break;
                    case OpCode.UpdateRelationship:
                        await HandleUserRequests.HandleRelationshipUpdateAsync(client, message);
                        break;
                    case OpCode.UserSentChatMessage:
                        await HandleUserRequests.HandleChatMessageAsync(message);
                        break;
                }
            }
            catch (InvalidOperationException ex)
            {
                Logger.LogError(ex);
                await client.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, null, CancellationToken.None);
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

        public static async Task ClosingConnAsync(WebSocket client)
        {
            Logger.LogInformation("Cleaning up the connection");

            if (client.State == WebSocketState.Open)
                await client.CloseAsync(WebSocketCloseStatus.InternalServerError, "", CancellationToken.None);

            if (VerificationCodes.Remove(client, out VerificationInfos verificationInfos))
            {
                PersonalDataDatabase database = new();
                await database.RemoveUserAsync(verificationInfos.UserId);
            }

            long key = Clients.FirstOrDefault(pair => EqualityComparer<WebSocket>.Default.Equals(pair.Value, client)).Key;
            Clients.Remove(key, out _);
            Security.ClientAes.Remove(client, out _);

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
                    Logger.LogWarning("MESSAGE CAN´T BE SENT: WEBSOCKETSTATE: ABORTED");
                    return;
                }

                var jsonPayload = JsonSerializer.Serialize(payload, JsonSerializerOptions);
                var buffer = Encoding.UTF8.GetBytes(jsonPayload);
                var compressedData = Security.CompressData(buffer);

                if (encryptionMode == EncryptionMode.Aes)
                    buffer = Security.EncryptAes(client, compressedData);

                await client.SendAsync(buffer, WebSocketMessageType.Binary, true, CancellationToken.None);

                Logger.LogInformation(ConsoleColor.Cyan, false, $"[SENDING(Aes)]: {buffer.Length} bytes");
                Logger.LogPayload(ConsoleColor.Blue, jsonPayload, "[SENDING(Aes)]:");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                await client.CloseAsync(WebSocketCloseStatus.InternalServerError, null, CancellationToken.None);
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
            return Config.GetProperty("ServerUri").GetString() ?? throw new JsonException("The server uri couldn´t be accessed");
        }

        private static string ReadEmailPassword()
            => Config.GetProperty("Gmail").GetProperty("Password").GetString()!;

        #endregion

        public static async Task SendEmail(User user, int verificationCode)
        {
            Logger.LogInformation($"Sending an email. Code: {verificationCode}");
            var fromAddress = "ccardoso7002@gmail.com";
            var toAddress = $"{user.Email}";
            var subject = $"Verification Email";
            var body = $"Hello {user.Username} {user.HashTag} this is your verification code: {verificationCode}." +
                $" If you did not attempt to create an account, please disregard this email.";

#pragma warning disable IDE0059
            MailMessage mail = new(fromAddress, toAddress, subject, body);

            SmtpClient smtpClient = new("smtp.gmail.com", 587)
            {
                Credentials = new NetworkCredential(fromAddress, _emailPassword),
                EnableSsl = true
            };

            #pragma warning restore IDE0059
            await Task.Delay(1); // Just so vs doesnt warn me
            //await smtpClient.SendMailAsync(mail);
        }

        /// <summary>
        /// Resolves a relative path by dynamically adjusting the base directory of the project.
        /// It removes the portion of the base directory up to and including the specified segment 
        /// ("ServerMessenger/ServerMessenger/") and combines the remaining path with the given relative path.
        /// </summary>
        /// <param name="relativePath"> 
        /// Example: If you want to get the file "appsettings.json" that is in directory "Settings" you would give this as an param:
        /// the relativePath = "Settings/appsettings.json" </param>
        /// <returns>A fully resolved path based on the project's base directory and the given relative path.</returns>
        public static string GetDynamicPath(string relativePath)
        {
            var projectBasePath = AppContext.BaseDirectory;

            var binIndex = projectBasePath.IndexOf(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal);

            if (binIndex == -1)
            {
                throw new Exception("Could not determine project base path!");
            }

            projectBasePath = projectBasePath[..binIndex];
            return Path.Combine(projectBasePath, relativePath);
        }
    }
}
