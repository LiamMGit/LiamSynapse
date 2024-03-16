namespace Synapse.Models
{
    public enum ClientOpcode
    {
        Authenticated = 0,
        Disconnected = 1,
        RefusedPacket = 2,
        PlayStatus = 4,
        ChatMessage = 10,
        UserBanned = 11,
        LeaderboardScores = 21,
        StopLevel = 22
    }

    public enum ServerOpcode
    {
        Authentication = 0,
        Disconnect = 1,
        SetChatter = 9,
        ChatMessage = 10,
        Command = 12,
        ScoreSubmission = 20,
        LeaderboardRequest = 21
    }
}
