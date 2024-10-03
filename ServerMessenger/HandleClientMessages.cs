using System.Net.Mail;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

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
            _ = DisplayError.LogAsync("Initializing HandleClientMessages");
            if (File.Exists(@"C:\Users\Crist\Desktop\gmailAppPassword.txt"))
            {
                _ = DisplayError.LogAsync("File exists");
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
            _ = DisplayError.LogAsync("Sending an email");
            string fromAddress = "ccardoso7002@gmail.com";
            string toAddress = $"{user.Email}";
            string subject = "Verification Email";
            string body = $"Hello {user.FirstName} {user.LastName} this is your verification code: {verificationCode}." +
                $" If you did not attempt to create an account, please disregard this email.";

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
                _ = DisplayError.LogAsync("Client disconnected!");
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

            _ = DisplayError.LogAsync(errorMessage);
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
                _ = DisplayError.LogAsync("Client disconnected!");
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
                _ = DisplayError.LogAsync("Client disconnected!");
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
            var profilPic = Convert.ToBase64String(File.ReadAllBytes(@"C:\Users\Crist\source\repos\ServerMessenger\ServerMessenger\defaultPic.png"));
            var result = await AccountInfoDatabase.PutAccountIntoTheDb(user);
            var payload = new
            {
                code = 7,
                result,
                user.Username,
                user.Email,
                user.Password,
                profilPic,
            };
            var jsonString = JsonSerializer.Serialize(payload);
            _ = Server.SendPayloadAsync(client, jsonString);
            lock (Server._lockClientsDict)
            {
                Server.Clients.Add(user.Username, client);
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
                    _ = DisplayError.LogAsync("Client disconnected!");
                    return;
                }
                _ = DisplayError.LogAsync("Handling login");

                var email = root.GetProperty("email").GetString();
                var password = root.GetProperty("password").GetString();

                var resultLogin = await AccountInfoDatabase.CheckLoginDataAsync(email!, password!);
                var result = resultLogin.isSuccess;
                var username = resultLogin.username;
                var id = resultLogin.id;
                string profilPic = "";

                if (id.HasValue && result.HasValue && result.Value)
                {
                    profilPic = await AccountInfoDatabase.GetProfilPicAsync(id.Value, null) ?? "";
                }

                var loginPayload = new
                {
                    code = 9,
                    result,
                    email,
                    password,
                    username,
                    id,
                    profilPic
                };
                var loginJsonString = JsonSerializer.Serialize(loginPayload);
                _ = Server.SendPayloadAsync(client, loginJsonString);
                _ = DisplayError.LogAsync("Sent login response and profile picture parts.");

                if (id is not null)
                {
                    _ = SendUserFriendships(client, id.Value);
                }

                if (!string.IsNullOrEmpty(username))
                {
                    lock (Server._lockClientsDict)
                    {
                        Server.Clients.Remove(username);
                        Server.Clients.Add(username, client);
                    }
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
                    var profilPic = await AccountInfoDatabase.GetProfilPicAsync(userId: friendId, username: null);
                    string base64ProfilePic = "";
                    if (!string.IsNullOrEmpty(profilPic))
                    {
                        using var image = Image.Load(Convert.FromBase64String(profilPic));
                        using var memoryStream = new MemoryStream();

                        image.Mutate(x => x.Resize(100, 100));  
                        image.SaveAsPng(memoryStream);
                        base64ProfilePic = Convert.ToBase64String(memoryStream.ToArray());
                    }

                    listUsernames.Add(new Friend
                    {
                        FriendId = friendId,
                        Status = status,
                        Username = username!,
                        ProfilPic = base64ProfilePic ?? ""
                    });
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
                    _ = DisplayError.LogAsync("Client disconnected!");
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

                var profilPic = await AccountInfoDatabase.GetProfilPicAsync(userId: null, username: usernameSender);
                if (result == true)
                {
                    //Sending friend request
                    var payloadSendingRequest = new
                    {
                        code = 12,
                        usernameSender,
                        senderId,
                        profilPic,
                    };
                    var jsonStringSendingRequest = JsonSerializer.Serialize(payloadSendingRequest);

                    TcpClient? receivingClient;
                    bool online;
                    lock (Server._lockClientsDict)
                    {
                        online = Server.Clients.TryGetValue(usernameReceiver!, out receivingClient);
                    }

                    var friendId = await AccountInfoDatabase.GetUserIdByUsernameAsync(usernameReceiver!);
                    _ = AccountInfoDatabase.PutFriendRequestIntoDbAsync(senderId, friendId.Value);

                    if (online)
                    {
                        _ = DisplayError.LogAsync("Client is online");
                        _ = Server.SendPayloadAsync(receivingClient!, jsonStringSendingRequest);
                    }
                }
            }
            catch (Exception ex)
            {
                DisplayError.DisplayBasicErrorInfos(ex, "HandleClientMessages", "HandleFriendRequest");
            }
        }

        public static async Task HandleRelationShipStateUpdate(JsonElement root)
        {
            try
            {
                var userId = root.GetProperty("userId").GetInt32();
                var username = root.GetProperty("username").GetString();
                var friendId = root.GetProperty("friendId").GetInt32();
                var friendUsername = root.GetProperty("friendUsername").GetString();
                var taskByte = root.GetProperty("task").GetByte();
                var profilPic = await AccountInfoDatabase.GetProfilPicAsync(userId, null);
                _ = DisplayError.LogAsync((RelationshipStateEnum)taskByte);
                if (await AccountInfoDatabase.UpdateRelationshipState(userId, friendId, (RelationshipStateEnum)taskByte))
                {
                    _ = DisplayError.LogAsync("Sending the relationship change to the affected client");
                    var payload = new
                    {
                        code = 17,
                        taskByte,
                        username,
                        profilPic,
                        userId,
                    };
                    var jsonString = JsonSerializer.Serialize(payload);
                    lock (Server._lockClientsDict)
                    {
                        _ = Server.SendPayloadAsync(Server.Clients[friendUsername!], jsonString);
                    }
                }
            }
            catch (Exception ex)
            {
                DisplayError.DisplayBasicErrorInfos(ex, "HandleClientMessages", "HandleRelationShipStateUpdate");
            }
        }
    }
}
