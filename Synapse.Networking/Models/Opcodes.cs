namespace Synapse.Networking.Models;

public enum ClientOpcode
{
    Authenticated,
    Disconnect,
    RefusedPacket,
    Ping,
    Status,
    ChatMessage,
    UserBanned,
    LeaderboardScores,
    StopLevel
}

public enum ServerOpcode
{
    Authentication,
    Disconnect,
    Ping,
    SetChatter,
    ChatMessage,
    Command,
    ScoreSubmission,
    LeaderboardRequest
}
