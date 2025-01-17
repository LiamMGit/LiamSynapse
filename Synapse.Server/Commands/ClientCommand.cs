using Microsoft.Extensions.Logging;
using Synapse.Networking.Models;
using Synapse.Server.Clients;
using Synapse.Server.Extras;
using Synapse.Server.Models;
using Synapse.Server.Services;

namespace Synapse.Server.Commands;

public class ClientCommand(ILogger<ClientCommand> log, IListenerService listenerService, IEventService eventService)
{
    private readonly Random _random = new();

    [Command("motd")]
    public void Motd(IClient client, string arguments)
    {
        string message = arguments.Unwrap();
        if (string.IsNullOrWhiteSpace(message))
        {
            client.SendPriorityServerMessage("{Motd}", eventService.Motd);
            return;
        }

        if (!client.HasPermission(Permission.Coordinator))
        {
            throw new CommandPermissionException();
        }

        eventService.Motd = message;
        client.LogAndSend(log, "Motd successfully changed");
        log.LogInformation("{ChangedMotd}", message);
    }

    [Command("roll")]
    public void Roll(IClient client, string arguments)
    {
        if (!client.Chatter)
        {
            throw new CommandChatterException();
        }

        string[] args = arguments.SplitArguments();
        int min = 1;
        int max = 100;
        switch (args.Length)
        {
            case > 2:
                throw new CommandTooManyArgumentException();
            case 2:
                Parse(args[0], ref min);
                Parse(args[1], ref max);
                break;
            case 1:
                Parse(args[0], ref max);
                break;
        }

        int roll = _random.Next(min, max + 1);
        listenerService.BroadcastServerMessage(
            "{Client} rolled {Roll} ({Min}-{Max})",
            client.DisplayUsername,
            roll,
            min,
            max);
        return;

        void Parse(string arg, ref int val)
        {
            if (int.TryParse(arg, out int i))
            {
                val = i;
            }
            else
            {
                throw new CommandParseException(arg);
            }
        }
    }

    [Command("tell")]
    [Command("t")]
    [Command("whisper")]
    [Command("w")]
    public void Tell(IClient client, string arguments)
    {
        arguments.SplitCommand(out string name, out string subArguments);
        string message = subArguments.Unwrap();
        message.NotEnough();
        IClient target = listenerService.Chatters.Keys.ScanQuery(name, CommandExtensions.ByUsername, false);

        string censored = StringUtils.Sanitize(message);

        client.SendChatMessage(
            new ChatMessage(target.Id, target.DisplayUsername, target.GetColor(), MessageType.WhisperTo, censored));
        target.SendChatMessage(
            new ChatMessage(client.Id, client.DisplayUsername, client.GetColor(), MessageType.WhisperFrom, censored));

        log.LogInformation("{Source} whispers {Target}: {Message}", client.DisplayUsername, target.DisplayUsername, censored);
    }

    [Command("who")]
    public void Who(IClient client, string arguments)
    {
        string flags = arguments.GetFlags(out string extra);
        string username = extra.Unwrap();
        bool verbose = false;
        if (flags.Contains('v'))
        {
            if (!client.HasPermission(Permission.Moderator))
            {
                throw new CommandPermissionException();
            }

            verbose = true;
        }

        IClient[] query;
        if (!string.IsNullOrWhiteSpace(username))
        {
            query = listenerService
                .Chatters.Keys
                .Where(n => n.Username.StartsWith(username, StringComparison.CurrentCultureIgnoreCase))
                .ToArray();

            switch (query.Length)
            {
                case <= 0:
                    throw new CommandQueryFailedException(username);
            }
        }
        else
        {
            int totalPlayers = listenerService.Clients.Count;
            int totalChatters = listenerService.Chatters.Count;
            client.SendPriorityServerMessage(
                "({Chatters}) currently chatting, ({Players}) currently online",
                totalPlayers,
                totalChatters);
            query = listenerService.Chatters.Keys.ToArray();
        }

        if (query.Length <= 0)
        {
            return;
        }

        int limit = flags.Contains('e') ? 100 : 20;
        IEnumerable<IClient> chatters = query.Take(limit);
        string names = string.Join(
            ", ",
            verbose ? chatters.Select(n => n.ToString()) : chatters.Select(n => n.DisplayUsername));
        if (query.Length > limit)
        {
            names += ", ...";
        }

        client.SendPriorityServerMessage("{Chatters}", names);
    }
}
