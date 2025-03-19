using System.Text.Json;

namespace Server_Messenger
{
    public static class HandleSettingsUpdate
    {
        public static void HandleReceivedMessage(ref JsonElement message)
        {
            SettingsUpdate settingsUpdate = message.GetSettingsUpdate();
            switch (settingsUpdate)
            {
                case SettingsUpdate.ChangeProfilPicture:
                    ChangeProfilPicture(ref message);
                    break;
            }
        }

        public static void ChangeProfilPicture(ref JsonElement message)
        {
            ProfilePictureUpdate profilePictureUpdate = JsonSerializer.Deserialize<ProfilePictureUpdate>(message.GetProperty("profilePictureUpdate"), Server.JsonSerializerOptions)!;
            PersonalDataDatabase personalDataDatabase = new();
            personalDataDatabase.ChangeProfilePicture(profilePictureUpdate);
        }
    }
}
