using System.Net.WebSockets;
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

        public static async Task CreateAccount(WebSocket client, JsonElement message)
        {
            User? user = JsonSerializer.Deserialize<User>(message, Server.JsonSerializerOptions);

            if (user == null)
            {
                Server.ClosingConn(client);
                return;
            }

            (NpgsqlExceptionInfos error, DatabaseInfos dbInfos) = await PersonalDataDatabase.CreateAccount(user);

            await AnswerClient(client, OpCode.AnswerToCreateAccount, user, error, dbInfos);
        }

        public static async Task RequestToLogin(WebSocket client, JsonElement message)
        {
            string email = message.GetProperty("email").GetString()!;
            string password = message.GetProperty("password").GetString()!;

            (User? user, NpgsqlExceptionInfos error, DatabaseInfos dbInfos) = await PersonalDataDatabase.CheckLoginData(email, password);
            await AnswerClient(client, OpCode.AnswerToLogin, user, error, dbInfos);
        }

        private static async Task AnswerClient(WebSocket client, OpCode code, User? user, NpgsqlExceptionInfos error, DatabaseInfos dbInfos)
        {
            if (user != null && error.Exception == NpgsqlExceptions.None)
            {
                Server.ClientsData.TryGetValue(client, out var value);

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
                error,
                dbInfos
            };
            await Server.SendPayloadAsync(client, payload);
        }
    }
}
