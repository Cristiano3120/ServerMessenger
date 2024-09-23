using System.Net.Mail;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace ServerMessenger
{
    /// <summary>
    /// Provides methods for reacting to a specific client message.
    /// </summary>
    internal static class HandleClientMessages
    {
        private static string _emailAppPaswword = string.Empty;

        public static void Initialize()
        {
            if (File.Exists(@"C:\Users\Crist\Desktop\gmailAppPassword.txt"))
            {
                _ = DisplayError.Log("File exists");
                using (var streamReader = new StreamReader(@"C:\Users\Crist\Desktop\gmailAppPassword.txt"))
                {
                    _emailAppPaswword = streamReader.ReadToEnd();
                }

                if (string.IsNullOrEmpty(_emailAppPaswword))
                {
                    Server.StopServer("Error(HandleClientMessages.Initialize()): The data in the file is corrupted!");
                }
            }
            else
            {
                Server.StopServer("Error(HandleClientMessages.Initialize()): File is corrupted or missing!");
            }
        }

        private static void SendEmail(User user, long verificationCode)
        {
            string fromAddress = "ccardoso7002@gmail.com";
            string toAddress = $"{user.Email}";
            string subject = "Verification Email";
            string body = $"Hello {user.FirstName} {user.LastName} this is your verification code: {verificationCode}";

            var mail = new MailMessage(fromAddress, toAddress, subject, body);

            SmtpClient smtpClient = new SmtpClient("smtp.gmail.com", 587);
            smtpClient.Credentials = new NetworkCredential(fromAddress, _emailAppPaswword);
            smtpClient.EnableSsl = true;
            smtpClient.Send(mail);
        }

        /// <summary>
        /// Gets called when receiving a code 2 message from the client.
        /// It checks the send email and username in the database and reacts accordingly.
        /// </summary>
        public static async Task HandleCreateAccount(TcpClient client, JsonElement root)
        {
            if (!client.Connected)
            {
                _ = DisplayError.Log("Client disconnected!");
                return;
            }
            var user = new User()
            {
                Email = root.GetProperty("Email").GetString()!,
                Username = root.GetProperty("Username").GetString()!,
            };
            UserCheckResult? result = await AccountInfoDatabase.CheckIfEmailOrUsernameExists(user);
            var errorMessage = result switch
            {
                UserCheckResult.None => "None",
                UserCheckResult.EmailExists => "Email",
                UserCheckResult.UsernameExists => "Username",
                UserCheckResult.BothExists => "Email and Username",
                _ => ""
            };

            if (result == UserCheckResult.None)
            {
                lock (Server._lockAwaitingMessagesDict)
                {
                    Server._clientsAwaitingMessages.Add(user.Username, new SaveAwaitingMessages());
                }
            }

            _ = DisplayError.Log(errorMessage);
            var payload = new
            {
                code = 4,
                status = errorMessage,
            };
            var jsonString = JsonSerializer.Serialize(payload);
            _ = Server.SendPayloadAsync(client, jsonString);
        }

        /// <summary>
        /// Gets called when receiving a code 5 message from the client.
        /// It sends an email with a verification code to the user.
        /// </summary>
        public static void HandleVerificationProcess(TcpClient client, JsonElement root)
        {
            if (!client.Connected)
            {
                _ = DisplayError.Log("Client disconnected!");
                return;
            }
            var user = new User()
            {
                Email = root.GetProperty("Email").GetString()!,
                FirstName = root.GetProperty("FirstName").GetString()!,
                LastName = root.GetProperty("LastName").GetString()!,
            };
            var verificationCode = root.GetProperty("VerifyCode").GetInt32();
            SendEmail(user, verificationCode);
        }

        /// <summary>
        /// Gets called when receiving a code 6 message from the client. 
        /// This method tries to put the user in the database.
        /// After that it sends a code 7 message to the client which tells the client if the operation was succesful.
        /// </summary>
        public static async Task VerificationSuccesful(TcpClient client, JsonElement root)
        {
            if (!client.Connected)
            {
                _ = DisplayError.Log("Client disconnected!");
                return;
            }
            var user = new User()
            {
                Email = root.GetProperty("Email").GetString()!,
                Username = root.GetProperty("Username").GetString()!,
                Password = root.GetProperty("Password").GetString()!,
                FirstName = root.GetProperty("FirstName").GetString()!,
                LastName = root.GetProperty("LastName").GetString()!,
                Day = root.GetProperty("Day").GetInt32()!,
                Month = root.GetProperty("Month").GetInt32()!,
                Year = root.GetProperty("Year").GetInt32()!,
            };
            var result = await AccountInfoDatabase.PutAccountIntoTheDb(user);
            var payload = new
            {
                code = 7,
                result,
                user.Username,
                user.Email,
                user.Password,
            };
            var jsonString = JsonSerializer.Serialize(payload);
            _ = Server.SendPayloadAsync(client, jsonString);
            lock (Server._lockClientsDict)
            {
                Server._clients.Add(user.Username, client);
            }
            lock (Server._lockAwaitingMessagesDict)
            {
                Server._clientsAwaitingMessages.Add(user.Username, new SaveAwaitingMessages());
            }
        }

        /// <summary>
        /// Gets called when receiving a code 8 message from the client. 
        /// It checks if the login data that the client enterd is correct
        /// and sends an according response.
        /// </summary>
        public static async Task HandleLogin(TcpClient client, JsonElement root)
        {
            try
            {
                if (!client.Connected)
                {
                    _ = DisplayError.Log("Client disconnected!");
                    return;
                }
                _ = DisplayError.Log("Handling login");
                var email = root.GetProperty("email").GetString();
                var password = root.GetProperty("password").GetString();

                var resultLogin = await AccountInfoDatabase.CheckLoginDataAsync(email!, password!);
                var result = resultLogin.isSuccess;
                var username = resultLogin.username;
                var id = resultLogin.id;

                var payload = new
                {
                    code = 9,
                    result,
                    email,
                    password,
                    username,
                    id,
                };
                var jsonString = JsonSerializer.Serialize(payload);
                _ = Server.SendPayloadAsync(client, jsonString);
                _ = DisplayError.Log("Sent a response to the client wheter he can login or not.");
                _ = DisplayError.Log("Put user into the online dict");
                if (id is not null)
                {
                    _ = SendUserFriendships(client, id.Value);
                }
                if (!string.IsNullOrEmpty(username))
                {
                    lock (Server._lockClientsDict)
                    {
                        Server._clients.Add(username, client);
                    }
                    _ = Task.Run(() => { Server.CheckingForWaitingMessages(client, username); });
                }
            }
            catch (Exception ex)
            {
                DisplayError.DisplayBasicErrorInfos(ex, "HandleClientMessages", "HandleLogin");
            }
        }

        private static async Task SendUserFriendships(TcpClient client, int id)
        {
            try
            {
                var listIds = await AccountInfoDatabase.GetFriendshipsAsync(id);

                var listUsernames = new List<Friend>();
                foreach (var (friendId, status) in listIds)
                {
                    Console.WriteLine(id);
                    Console.WriteLine(friendId);
                    var username = await AccountInfoDatabase.GetUsernameByIdAsync(friendId);
                    listUsernames.Add(new Friend { FriendId = friendId, Status = status, Username = username! });
                }

                var payload = new
                {
                    code = 13,
                    friends = listUsernames
                };
                var jsonString = JsonSerializer.Serialize(payload);
                _ = Server.SendPayloadAsync(client, jsonString);
            }
            catch (Exception ex)
            {
                DisplayError.DisplayBasicErrorInfos(ex, "HandleClientMessages", "SendUserFriendship");
            }
        }

        public static async Task HandleFriendRequest(TcpClient client, JsonElement root)
        {
            try
            {
                if (!client.Connected)
                {
                    _ = DisplayError.Log("Client disconnected!");
                    return;
                }

                var usernameSender = root.GetProperty("usernameSender").GetString();
                var usernameReceiver = root.GetProperty("usernameReceiver").GetString();
                var senderId = root.GetProperty("senderId").GetInt32();
                var result = await AccountInfoDatabase.CheckIfUserExists(usernameReceiver!);

                var payload = new
                {
                    code = 11,
                    result,
                };
                var jsonString = JsonSerializer.Serialize(payload);
                _ = Server.SendPayloadAsync(client, jsonString);

                if (result == true)
                {
                    //Sending friend request
                    var payloadSendingRequest = new
                    {
                        code = 12,
                        usernameSender,
                        senderId,
                    };
                    var jsonStringSendingRequest = JsonSerializer.Serialize(payloadSendingRequest);

                    TcpClient? receivingClient;
                    bool online;
                    lock (Server._lockClientsDict)
                    {
                        online = Server._clients.TryGetValue(usernameReceiver!, out receivingClient);
                    }

                    var friendId = await AccountInfoDatabase.GetUserIdByUsernameAsync(usernameReceiver!);
                    _ = AccountInfoDatabase.PutFriendRequestIntoDbAsync(senderId, friendId.Value);

                    if (online)
                    {
                        _ = DisplayError.Log("Client is online");
                        _ = Server.SendPayloadAsync(receivingClient!, jsonStringSendingRequest);
                    }
                }
            }
            catch (Exception ex)
            {
                DisplayError.DisplayBasicErrorInfos(ex, "HandleClientMessages", "HandleFriendRequest");
            }
        }
    }
}
