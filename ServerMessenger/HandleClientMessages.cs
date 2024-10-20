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
            var filepathAppPassword = @"C:\Users\Crist\Desktop\gmailAppPassword.txt";
            _ = DisplayError.LogAsync("Initializing HandleClientMessages");
            if (File.Exists(filepathAppPassword))
            {
                _ = DisplayError.LogAsync("File exists");
                using (var streamReader = new StreamReader(filepathAppPassword))
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

            var smtpClient = new SmtpClient("smtp.gmail.com", 587);
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
            lock (Server.lockClientsDict)
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
                    lock (Server.lockClientsDict)
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
                _ = DisplayError.LogAsync("Sending relationships");
                var listIds = await AccountInfoDatabase.GetFriendshipsAsync(id);
                var listUsernames = new List<Friend>();

                foreach (var (friendId, status) in listIds)
                {
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
                await Server.SendPayloadAsync(client, jsonString);

                var usernameClient = await AccountInfoDatabase.GetUsernameByIdAsync(id);
                foreach (var (friendUsername, status, profilPic) in listUsernames)
                {
                    await SendUserChats(client, usernameClient!, friendUsername);
                }
            }
            catch (Exception ex)
            {
                DisplayError.DisplayBasicErrorInfos(ex, "HandleClientMessages", "SendUserFriendship");
            }
        }

        private static async Task SendUserChats(TcpClient client, string username, string friendUsername)
        {
            try
            {
                var friend = new UserAfterLogin()
                {
                    Username = friendUsername,
                };

                var user = new UserAfterLogin()
                {
                    Username = username
                };
                var chatID = await ChatsDatabse.GetChatID([user, friend]);
                var messages = await ChatsDatabse.GetMessagesFromChat(chatID);

                var payload = new
                {
                    code = 20,
                    usernameFriend = friend.Username,
                    messages
                };

                await Task.Delay(500);
                var jsonString = JsonSerializer.Serialize(payload);
                await Server.SendPayloadAsync(client, jsonString);
            }
            catch (Exception ex)
            {
                DisplayError.DisplayBasicErrorInfos(ex, "HandleClientMessages", "SendUserChats");
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
                        profilPic,
                    };
                    var jsonStringSendingRequest = JsonSerializer.Serialize(payloadSendingRequest);

                    TcpClient? receivingClient;
                    bool online;
                    lock (Server.lockClientsDict)
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
                var friendUsername = root.GetProperty("friendUsername").GetString();
                var taskByte = root.GetProperty("task").GetByte();
                var profilPic = await AccountInfoDatabase.GetProfilPicAsync(userId, null);
                var friendId = await AccountInfoDatabase.GetUserIdByUsernameAsync(friendUsername!);
                _ = DisplayError.LogAsync((RelationshipStateEnum)taskByte);

                var relationShipState = (RelationshipStateEnum)taskByte;
                if ((RelationshipStateEnum)taskByte == RelationshipStateEnum.Accepted)
                {
                    var chatUsers = new List<UserAfterLogin>
                    {
                        new UserAfterLogin() { Username = username! },
                        new UserAfterLogin() { Username = friendUsername! }
                    };
                    _ = ChatsDatabse.CreateChatAsync(chatUsers);
                }
                else if (relationShipState == RelationshipStateEnum.Delete || relationShipState == RelationshipStateEnum.Blocked)
                {
                    var user1 = new UserAfterLogin()
                    { 
                        Username = username!
                    };

                    var user2 = new UserAfterLogin()
                    {
                        Username = friendUsername!
                    };

                    var chatID = await ChatsDatabse.GetChatID([user1, user2]);
                    ChatsDatabse.DeleteChatAsync(chatID);
                }
                
                if (await AccountInfoDatabase.UpdateRelationshipState(userId, friendId.Value, (RelationshipStateEnum)taskByte))
                {
                    _ = DisplayError.LogAsync("Sending the relationship change to the affected client");
                    var payload = new
                    {
                        code = 17,
                        taskByte,
                        username,
                        profilPic,
                    };

                    var jsonString = JsonSerializer.Serialize(payload);
                    lock (Server.lockClientsDict)
                    {
                        if (Server.Clients.TryGetValue(friendUsername!, out var client))
                        {
                            _ = Server.SendPayloadAsync(client, jsonString);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DisplayError.DisplayBasicErrorInfos(ex, "HandleClientMessages", "HandleRelationShipStateUpdate");
            }
        }

        public static async Task HandleSentTextMessage(JsonElement root)
        {
            try
            {
                var usernameSender = root.GetProperty("username").GetString();
                var usernameReceiver = root.GetProperty("friendUsername").GetString();
                var content = root.GetProperty("message").GetString();
                var time = root.GetProperty("time").GetDateTime();

                lock (Server.lockClientsDict)
                {
                    if (Server.Clients.TryGetValue(usernameReceiver!, out var receiverTcp))
                    {
                        var payload = new
                        {
                            code = 19,
                            usernameSender,
                            content,
                            time,
                        };

                        var jsonString = JsonSerializer.Serialize(payload);
                        _ = Server.SendPayloadAsync(receiverTcp, jsonString);
                    }
                }

                var user = new UserAfterLogin()
                {
                    Username = usernameSender!,
                };

                var friend = new UserAfterLogin()
                {
                    Username = usernameReceiver!,
                };

                var message = new Message()
                {
                    Content = content!,
                    Sender = user,
                    Time = time,
                };

                var chatID = await ChatsDatabse.GetChatID([user, friend]);
                _ = ChatsDatabse.AddMessageToChat(chatID, message);
            }
            catch (Exception ex)
            {
                DisplayError.DisplayBasicErrorInfos(ex, "HandleClientMessages", "HandleSentTextMessage");
            }
        }
    }
}
