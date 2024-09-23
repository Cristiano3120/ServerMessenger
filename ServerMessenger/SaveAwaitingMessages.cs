using System.Text.Json;

namespace ServerMessenger
{
    internal sealed class SaveAwaitingMessages
    {
        public Queue<JsonElement> AwaitingMessages { get; private set; }

        public SaveAwaitingMessages()
        {
            AwaitingMessages = new();
        }
    }
}
