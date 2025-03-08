using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace Server_Messenger
{
    internal static class HandleUserRequests
    {
        public static async Task ReceivedAesAsync(WebSocket client, JsonElement message)
        {
            Security.ClientAes.TryAdd(client, message.GetAes());

            var payload = new
            {
                opCode = OpCode.ReadyToReceive,
            };
            await Server.SendPayloadAsync(client, payload);
        }

        #region Create Account
        public static async Task CreateAccountAsync(WebSocket client, JsonElement message)
        {
            User? user = JsonSerializer.Deserialize<User>(message, Server.JsonSerializerOptions);

            if (user == null)
            {
                await Server.ClosingConnAsync(client);
                return;
            }

            PersonalDataDatabase database = new();
            (NpgsqlExceptionInfos npgsqlExceptionInfos, long userId) = await database.CreateAccountAsync(user);

            if (npgsqlExceptionInfos.Exception == NpgsqlExceptions.None)
            {
                int verificationCode = RandomNumberGenerator.GetInt32(10000000, 99999999);

                VerificationInfos verificationInfo = new()
                {
                    VerificationCode = verificationCode,
                    VerificationAttempts = 0,
                    UserId = userId,
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

        public static async Task RequestToVerifyAsync(WebSocket client, JsonElement message)
        {
            Server.VerificationCodes.TryGetValue(client, out VerificationInfos verificationInfos);
            int userVerificationCode = message.GetProperty("verificationCode").GetInt32();
            object payload;

            byte maxVerificationAttempts = 3;
            if (verificationInfos.VerificationAttempts == maxVerificationAttempts)
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
                Server.Clients.TryAdd(verificationInfos.UserId, client);
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

        #endregion

        #region Handle Relationship Update
        public static async Task HandleRelationshipUpdateAsync(WebSocket client, JsonElement message)
        {
            RelationshipUpdate relationshipUpdate = JsonSerializer.Deserialize<RelationshipUpdate>(message.GetProperty("relationshipUpdate"), Server.JsonSerializerOptions);

            PersonalDataDatabase database = new();
            NpgsqlExceptionInfos npgsqlExceptionInfos = await database.UpdateRelationshipAsync(relationshipUpdate);

            var payload = new
            {
                opCode = OpCode.AnswerToRequestedRelationshipUpdate,
                npgsqlExceptionInfos,
            };

            await Server.SendPayloadAsync(client, payload);

            if (npgsqlExceptionInfos.Exception == NpgsqlExceptions.None)
            {
                await InformAffectedUser(relationshipUpdate);
            }
        }

        /// <summary>
        /// Informs the affected user that a relationship has been updated
        /// </summary>
        /// <returns></returns>
        private static async Task InformAffectedUser(RelationshipUpdate pRelationshipUpdate)
        {
            long affectedClientId = pRelationshipUpdate.Relationship!.Id;

            if (affectedClientId == -1)
            {
                PersonalDataDatabase database = new();
                var user = (Relationship)await database.GetUser(pRelationshipUpdate.Relationship.Username, pRelationshipUpdate.Relationship.HashTag);
                affectedClientId = user!.Id;
            }

            RelationshipUpdate relationshipUpdate = pRelationshipUpdate with
            {
                Relationship = (Relationship)pRelationshipUpdate.User,
                User = null
            };

            var payload = new
            {
                opCode = OpCode.ARelationshipWasUpdated,
                relationshipUpdate
            };

            WebSocket affectedClient = Server.Clients[affectedClientId];
            await Server.SendPayloadAsync(affectedClient, payload);
        }

        #endregion

        public static async Task RequestToLoginAsync(WebSocket client, JsonElement message)
        {
            LoginRequest loginRequest = JsonSerializer.Deserialize<LoginRequest>(message.GetProperty("loginRequest"), Server.JsonSerializerOptions);

            if (loginRequest.IsEmpty())
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
                ? OpCode.AnswerToLogin
                : OpCode.AnswerToAutoLogin;

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
                    UserId = user.Id,
                };

                await Server.SendEmail(user, verificationCode);
                Server.VerificationCodes.TryAdd(client, verificationInfos);
                return;
            }
            else
            {
                Server.Clients.TryAdd(user.Id, client);
            }

            await SendFriendshipsAsync(client, database, user.Id);
        }

        private static async Task SendFriendshipsAsync(WebSocket client, PersonalDataDatabase database, long userID)
        {
            (NpgsqlExceptionInfos npgsqlExceptionInfos, HashSet<Relationship>? relationships) = await database.GetUsersRelationships(userID);

            if (npgsqlExceptionInfos.Exception == NpgsqlExceptions.NoDataEntrys)
                return;

            if (npgsqlExceptionInfos.Exception == NpgsqlExceptions.None && relationships?.Count == 0)
                return;

            var payload = new
            {
                opCode = OpCode.SendFriendships,
                npgsqlExceptionInfos,
                relationships,
            };

            await Server.SendPayloadAsync(client, payload);
        }
    }
}
