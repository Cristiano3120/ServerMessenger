﻿using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Schema;

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
            await AnswerClientAsync(client, npgsqlExceptionInfos, payload, user);
        }

        public static async Task HandleRelationshipUpdateAsync(WebSocket client, JsonElement message)
        {
            JsonElement relationshipUpdateProperty = message.GetProperty("relationshipUpdate");

            Relationshipstate relationshipstate = relationshipUpdateProperty.GetProperty("requestedRelationshipstate").GetRelationshipstate();
            Relationship relationship = JsonSerializer.Deserialize<Relationship>(relationshipUpdateProperty, Server.JsonSerializerOptions)!;
            User user = JsonSerializer.Deserialize<User>(relationshipUpdateProperty, Server.JsonSerializerOptions)!;

            RelationshipUpdate relationshipUpdate = new()
            {
                User = user,
                Relationship = relationship,
                RequestedRelationshipstate = relationshipstate
            };

            if (user == null || relationship == null)
            {
                await Server.ClosingConnAsync(client);
                return;
            }

            PersonalDataDatabase database = new();
            (NpgsqlExceptionInfos npgsqlExceptionInfos, User? affectedUser) = await database.UpdateRelationshipAsync(relationshipUpdate);
            var payload = new
            {
                opCode = OpCode.AnswerToRequestedRelationshipUpdate,
                npgsqlExceptionInfos,
                affectedUser
            };

            await Server.SendPayloadAsync(client, payload);
        }

        public static async Task RequestToLoginAsync(WebSocket client, JsonElement message)
        {
            Logger.LogInformation("Received an login request");
            string email = message.GetProperty("email").GetString()!;
            string password = message.GetProperty("password").GetString()!;
            bool stayLoggedIn = message.GetProperty("stayLoggedIn").GetBoolean();

            PersonalDataDatabase database = new();
            (User? user, NpgsqlExceptionInfos npgsqlExceptionInfos) = await database.CheckLoginDataAsync(email, password, stayLoggedIn);

            var payload = new
            {
                opCode = OpCode.AnswerToLogin,
                npgsqlExceptionInfos,
                user,
            };
            await AnswerClientAsync(client, npgsqlExceptionInfos, payload, user);

            if (user == null)
                return;

            if (user.FaEnabled)
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

            (NpgsqlExceptionInfos exceptionInfos, HashSet<Relationship>? relationships) = await database.GetUsersRelationships(user.Id);

            var payload2 = new
            {
                opCode = OpCode.SendFriendships,
                npgsqlExceptionInfos = exceptionInfos,
                relationships,
            };

            await Server.SendPayloadAsync(client, payload2);
        }

        public static async Task RequestToAutoLoginAsync(WebSocket client, JsonElement message)
        {
            Logger.LogInformation("Received an auto-login request");
            string token = message.GetProperty("token").GetString()!;

            PersonalDataDatabase database = new();
            (User? user, NpgsqlExceptionInfos npgsqlExceptionInfos) = await database.CheckLoginDataAsync(token);

            var payload = new
            {
                opCode = OpCode.AutoLoginResponse,
                npgsqlExceptionInfos,
                user,
            };
            await AnswerClientAsync(client, npgsqlExceptionInfos, payload, user);
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

        private static async Task AnswerClientAsync(WebSocket client, NpgsqlExceptionInfos npgsqlExceptionInfos, object payload, User? user)
        {
            if (npgsqlExceptionInfos.Exception == NpgsqlExceptions.None && user != null)
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
