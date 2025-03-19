using System.Text.Json;

namespace Server_Messenger
{
    public static class HandleSettingsUpdate
    {
        public static async Task HandleReceivedMessage(JsonDocument jsonDocument)
        {
            JsonElement message = jsonDocument.RootElement;

            SettingsUpdate settingsUpdate = message.GetSettingsUpdate();
            switch (settingsUpdate)
            {
                case SettingsUpdate.ChangeProfilPicture:
                    await ChangeProfilPicture(message);
                    break;
            }
        }

        public static async Task ChangeProfilPicture(JsonElement message)
        {
            ProfilePictureUpdate profilePictureUpdate = JsonSerializer.Deserialize<ProfilePictureUpdate>(message.GetProperty("profilePictureUpdate"), Server.JsonSerializerOptions)!;
            PersonalDataDatabase personalDataDatabase = new();
            await personalDataDatabase.ChangeProfilePicture(profilePictureUpdate);
        }
    }
}
