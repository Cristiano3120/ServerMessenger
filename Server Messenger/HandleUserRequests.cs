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
            Logger.LogInformation("Received a request to create an account");
            User? user = JsonSerializer.Deserialize<User>(message, Server.JsonSerializerOptions);

            if (user == null)
            {
                Server.ClosingConn(client);
                return;
            }

            NpgsqlExceptionInfos error = await PersonalDataDatabase.CreateAccount(user);

            await AnswerClient(client, OpCode.AnswerToCreateAccount, user, error);
        }

        public static async Task RequestToLogin(WebSocket client, JsonElement message)
        {
            Logger.LogInformation("Received an login request");
            string email = message.GetProperty("email").GetString()!;
            string password = message.GetProperty("password").GetString()!;

            (User? user, NpgsqlExceptionInfos error) = await PersonalDataDatabase.CheckLoginData(email, password);
            await AnswerClient(client, OpCode.AnswerToLogin, user, error);
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
