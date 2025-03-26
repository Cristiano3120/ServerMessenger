using System.Net.WebSockets;
using System.Text.Json;

namespace Server_Messenger
{
    public static class HandleSettingsUpdate
    {
        public static async Task HandleReceivedMessage(WebSocket client, JsonDocument jsonDocument)
        {
            JsonElement message = jsonDocument.RootElement;

            SettingsUpdate settingsUpdate = message.GetSettingsUpdate();
            switch (settingsUpdate)
            {
                case SettingsUpdate.ChangeProfilPicture:
                    await ChangeProfilPictureAsync(message);
                    break;
                case SettingsUpdate.ChangeUsername:
                    await ChangeUsernameAsync(client, message);
                    break;
            }
        }

        private static async Task ChangeProfilPictureAsync(JsonElement message)
        {
            ProfilePictureUpdate profilePictureUpdate = JsonSerializer.Deserialize<ProfilePictureUpdate>(message.GetProperty("profilePictureUpdate"), Server.JsonSerializerOptions)!;
            PersonalDataDatabase personalDataDatabase = new();
            await personalDataDatabase.ChangeProfilePictureAsync(profilePictureUpdate);
        }

        private static async Task ChangeUsernameAsync(WebSocket client, JsonElement message)
        {
            UsernameUpdate usernameUpdate = JsonSerializer.Deserialize<UsernameUpdate>(message.GetProperty("usernameUpdate"), Server.JsonSerializerOptions);
            PersonalDataDatabase personalDataDatabase = new();
            UsernameUpdateResult usernameUpdateResult = await personalDataDatabase.ChangeUsernameAsync(usernameUpdate);

            var payload = new
            {
                opCode = OpCode.SettingsUpdate,
                settingsUpdate = SettingsUpdate.AnswerToUsernameChange,
                usernameUpdateResult,
                usernameUpdate,
            };
            await Server.SendPayloadAsync(client, payload);
        }
    }
}
