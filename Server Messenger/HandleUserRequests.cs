using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text.Json;

namespace Server_Messenger
{
    internal static class HandleUserRequests
    {
        public static async Task ReceivedAesAsync(WebSocket client, JsonElement message)
        {
            Logger.LogInformation("Received Aes");
            Security.ClientAes.TryAdd(client, message.GetAes());

            var payload = new
            {
                opCode = OpCode.ReadyToReceive,
            };
            await Server.SendPayloadAsync(client, payload);
        }

        public static async Task CreateAccountAsync(WebSocket client, JsonElement message)
        {
            Logger.LogInformation("Received a request to create an account");
            User? user = JsonSerializer.Deserialize<User>(message, Server.JsonSerializerOptions);

            if (user == null)
            {
                await Server.ClosingConnAsync(client);
                return;
            }

            PersonalDataDatabase database = new();
            NpgsqlExceptionInfos npgsqlExceptionInfos = await database.CreateAccountAsync(user);

            if (npgsqlExceptionInfos.Exception == NpgsqlExceptions.None)
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
                opCode = OpCode.AnswerToCreateAccount,
                npgsqlExceptionInfos,
                user,
            };

            await Server.SendPayloadAsync(client, payload);
        }

        public static async Task HandleRelationshipUpdateAsync(WebSocket client, JsonElement message)
        {
            JsonElement relationshipUpdateProperty = message.GetProperty("relationshipUpdate");

            Relationshipstate relationshipstate = relationshipUpdateProperty.GetProperty("requestedRelationshipstate").GetRelationshipstate();
            Relationship wantedRelationship = JsonSerializer.Deserialize<Relationship>(relationshipUpdateProperty, Server.JsonSerializerOptions)!;
            User user = JsonSerializer.Deserialize<User>(relationshipUpdateProperty, Server.JsonSerializerOptions)!;

            RelationshipUpdate relationshipUpdate = new()
            {
                User = user,
                Relationship = wantedRelationship,
                RequestedRelationshipstate = relationshipstate
            };

            if (user == null || wantedRelationship == null)
            {
                await Server.ClosingConnAsync(client);
                return;
            }

            PersonalDataDatabase database = new();
            (NpgsqlExceptionInfos npgsqlExceptionInfos, Relationship? relationship) = await database.UpdateRelationshipAsync(relationshipUpdate);

            var payload = new
            {
                opCode = OpCode.AnswerToRequestedRelationshipUpdate,
                npgsqlExceptionInfos,
                relationship
            };

            await Server.SendPayloadAsync(client, payload);
        }

        #region Login

        public static async Task RequestToLoginAsync(WebSocket client, JsonElement message)
        {
            Logger.LogInformation("Received an login request");
            LoginRequest? loginRequest = JsonSerializer.Deserialize<LoginRequest>(message.GetProperty("loginRequest"), Server.JsonSerializerOptions);

            if (loginRequest == null)
            {
                await Server.ClosingConnAsync(client);
                return;
            }

            string token = loginRequest.Token;
            PersonalDataDatabase database = new();
            (User? user, NpgsqlExceptionInfos npgsqlExceptionInfos) = token == ""
                ? await database.CheckLoginDataAsync(loginRequest)
                : await database.CheckLoginDataAsync(token);

            OpCode opCode = token == ""
                ? OpCode.AnswerToLoginRequest
                : OpCode.AnswerToAutoLoginRequest;

            var payload = new
            {
                opCode,
                npgsqlExceptionInfos,
                user,
            };

            await Server.SendPayloadAsync(client, payload);

            if (user == null)
                return;

            if (loginRequest.Token == "" && user.FaEnabled)
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
                return;
            }

            await SendFriendshipsAsync(client, database, user.Id);
        }

        private static async Task SendFriendshipsAsync(WebSocket client, PersonalDataDatabase database, long userID)
        {
            (NpgsqlExceptionInfos npgsqlExceptionInfos, HashSet<Relationship>? relationships) = await database.GetUsersRelationships(userID);

            if (npgsqlExceptionInfos.Exception == NpgsqlExceptions.NoDataEntrys)
                return;

            //var payload = new
            //{
            //    opCode = OpCode.SendFriendships,
            //    npgsqlExceptionInfos,
            //    relationships,
            //};

            //await Server.SendPayloadAsync(client, payload);
        }

        #endregion

        public static async Task RequestToVerifyAsync(WebSocket client, JsonElement message)
        {
            Server.VerificationCodes.TryGetValue(client, out VerificationInfos? verificationInfos);
            int userVerificationCode = message.GetProperty("verificationCode").GetInt32();
            object payload;

            if (verificationInfos == null || verificationInfos.VerificationAttempts == 5)
            {
                payload = new
                {
                    opCode = OpCode.VerificationWentWrong,
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
                opCode = OpCode.RequestToVerifiy,
                success,
            };
            await Server.SendPayloadAsync(client, payload);
        }
    }
}
