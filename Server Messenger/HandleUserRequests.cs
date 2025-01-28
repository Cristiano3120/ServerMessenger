using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text.Json;

namespace Server_Messenger
{
    internal static class HandleUserRequests
    {
        public static async Task ReceivedAes(WebSocket client, JsonElement message)
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

        public static async Task CreateAccount(WebSocket client, JsonElement message)
        {
            Logger.LogInformation("Received a request to create an account");
            User? user = JsonSerializer.Deserialize<User>(message, Server.JsonSerializerOptions);

            if (user == null)
            {
                await Server.ClosingConn(client);
                return;
            }

            NpgsqlExceptionInfos error = await PersonalDataDatabase.CreateAccount(user);

            if (error.Exception == NpgsqlExceptions.None)
            {
                int verificationCode = RandomNumberGenerator.GetInt32(10000000, 99999999);

                VerificationInfos verificationInfo = new()
                {
                    VerificationCode = verificationCode,
                    VerificationAttempts = 0,
                    Email = user.Email
                };

                Server.VerificationCodes.TryAdd(client, verificationInfo);
                Server.SendEmail(user, verificationCode);
            }

            await AnswerClient(client, OpCode.AnswerToCreateAccount, user, error);
        }

        #endregion

        public static async Task RequestToLogin(WebSocket client, JsonElement message)
        {
            Logger.LogInformation("Received an login request");
            string email = message.GetProperty("email").GetString()!;
            string password = message.GetProperty("password").GetString()!;

            (User? user, NpgsqlExceptionInfos error) = await PersonalDataDatabase.CheckLoginData(email, password);
            await AnswerClient(client, OpCode.AnswerToLogin, user, error);

            if (user != null && user.FaEnabled)
            {
                int verificationCode = RandomNumberGenerator.GetInt32(10000000, 99999999);
                VerificationInfos verificationInfos = new()
                {
                    VerificationCode = verificationCode,
                    VerificationAttempts = 0,
                    Email = user.Email,
                };
                Server.SendEmail(user, verificationCode);
                Server.VerificationCodes.TryAdd(client, verificationInfos);
            }
        }

        public static async Task RequestToVerify(WebSocket client, JsonElement message)
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

        private static async Task AnswerClient(WebSocket client, OpCode code, User? user, NpgsqlExceptionInfos error)
        {
            if (user != null && error.Exception == NpgsqlExceptions.None)
            {
                Server.ClientsData.TryGetValue(client, out UserData? value);

                value = value! with 
                { 
                    Id = user.Id 
                };

                Server.ClientsData.AddOrUpdate(client, value, (_, _) =>
                {
                    return value;
                });
            }

            var payload = new
            {
                code,
                user,
                npgsqlException = error,
            };
            await Server.SendPayloadAsync(client, payload);
        }
    }
}
