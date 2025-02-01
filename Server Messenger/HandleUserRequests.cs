using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text.Json;
using Npgsql;

namespace Server_Messenger
{
    internal static class HandleUserRequests
    {
        public static async Task ReceivedAesAsync(WebSocket client, JsonElement message)
        {
            Logger.LogInformation("Received Aes");
            Server.ClientsData.TryAdd(client, new UserData()
            {
                Aes = message.GetAes(),
            });

            var payload = new
            {
                code = OpCode.ReadyToReceive,
            };
            await Server.SendPayloadAsync(client, payload);
        }

        #region CreateAcc

        public static async Task CreateAccountAsync(WebSocket client, JsonElement message)
        {
            Logger.LogInformation("Received a request to create an account");
            User? user = JsonSerializer.Deserialize<User>(message, Server.JsonSerializerOptions);

            if (user == null)
            {
                await Server.ClosingConnAsync(client);
                return;
            }

            (NpgsqlExceptionInfos npgsqlException, string token) = await PersonalDataDatabase.CreateAccountAsync(user);

            if (npgsqlException.Exception == NpgsqlExceptions.None)
            {
                int verificationCode = RandomNumberGenerator.GetInt32(10000000, 99999999);

                VerificationInfos verificationInfo = new()
                {
                    VerificationCode = verificationCode,
                    VerificationAttempts = 0,
                    Email = user.Email
                };

                Server.VerificationCodes.TryAdd(client, verificationInfo);
                await Server.SendEmail(user, verificationCode);
            }

            var payload = new
            {
                code = OpCode.AnswerToCreateAccount,
                npgsqlException,
                token,
                user,       
            };
            await AnswerClientAsync(client, npgsqlException, payload, user);
        }

        #endregion

        public static async Task RequestToLoginAsync(WebSocket client, JsonElement message)
        {
            Logger.LogInformation("Received an login request");
            string email = message.GetProperty("email").GetString()!;
            string password = message.GetProperty("password").GetString()!;

            (User? user, NpgsqlExceptionInfos npgsqlException) = await PersonalDataDatabase.CheckLoginDataAsync(email, password);

            var payload = new
            {
                code = OpCode.AnswerToLogin,
                npgsqlException,
                user,
            };
            await AnswerClientAsync(client, npgsqlException, payload, user);

            if (user != null && user.FaEnabled)
            {
                int verificationCode = RandomNumberGenerator.GetInt32(10000000, 99999999);
                VerificationInfos verificationInfos = new()
                {
                    VerificationCode = verificationCode,
                    VerificationAttempts = 0,
                    Email = user.Email,
                };
                await Server.SendEmail(user, verificationCode);
                Server.VerificationCodes.TryAdd(client, verificationInfos);
            }
        }

        public static async Task RequestToAutoLoginAsync(WebSocket client, JsonElement message)
        {
            Logger.LogInformation("Received an auto-login request");
            string token = message.GetProperty("token").GetString()!;

            (User? user, NpgsqlExceptionInfos npgsqlException) = await PersonalDataDatabase.CheckLoginDataAsync(token);

            var payload = new
            {
                code = OpCode.AutoLoginResponse,
                npgsqlException,
                user,
            };
            await AnswerClientAsync(client, npgsqlException, payload, user);
        }

        public static async Task RequestToVerifyAsync(WebSocket client, JsonElement message)
        {
            Server.VerificationCodes.TryGetValue(client, out VerificationInfos? verificationInfos);
            int userVerificationCode = message.GetProperty("verificationCode").GetInt32();
            object payload;

            if (verificationInfos == null || verificationInfos.VerificationAttempts == 5)
            {
                payload = new
                {
                    code = OpCode.VerificationWentWrong,
                };
                await Server.SendPayloadAsync(client, payload);
                return;
            }

            bool success = userVerificationCode == verificationInfos.VerificationCode;
            if (success)
            {
                Server.VerificationCodes.Remove(client, out _);
            }
            else
            {
                verificationInfos.VerificationAttempts++;
            }

            payload = new
            {
                code = OpCode.RequestToVerifiy,
                success,
            };
            await Server.SendPayloadAsync(client, payload);
        }

        private static async Task AnswerClientAsync(WebSocket client, NpgsqlExceptionInfos npgsqlException, object payload, User? user)
        {
            if (npgsqlException.Exception == NpgsqlExceptions.None && user != null)
            {
                Server.ClientsData.TryGetValue(client, out UserData? userData);

                //Add the Id to the user data 
                userData = userData! with 
                { 
                    Id = user.Id 
                };

                Server.ClientsData.AddOrUpdate(client, userData, (_, _) => { return userData; });
            }

            await Server.SendPayloadAsync(client, payload);
        }
    }
}
