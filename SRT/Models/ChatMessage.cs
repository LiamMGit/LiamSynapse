namespace SRT.Models
{
    public struct ChatMessage
    {
        public ChatMessage(string id, string username, string message)
        {
            Id = id;
            Username = username;
            Message = message;
        }

        public string Id { get; }

        public string Username { get; }

        public string Message { get; }
    }
}
