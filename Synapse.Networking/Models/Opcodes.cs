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
    AcknowledgeScore,
    InvalidateScores,
    LeaderboardScores,
    StopLevel
}

public enum ServerOpcode
{
    Authentication,
    Disconnect,
    Ping,
    SetChatter,
    SetDivision,
    ChatMessage,
    Command,
    ScoreSubmission,
    LeaderboardRequest
}
