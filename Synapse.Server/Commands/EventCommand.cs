using Synapse.Networking.Models;
using Synapse.Server.Clients;
using Synapse.Server.Extras;
using Synapse.Server.Models;
using Synapse.Server.Services;
using Synapse.Server.Stages;

namespace Synapse.Server.Commands;

public class EventCommand
{
    private readonly IEventService _eventService;
    private readonly IListenerService _listenerService;
    private readonly IMapService _mapService;
    private readonly PlayStage _playStage;

    public EventCommand(
        IEventService eventService,
        IMapService mapService,
        IListenerService listenerService,
        IEnumerable<Stage> stages)
    {
        _eventService = eventService;
        _mapService = mapService;
        _listenerService = listenerService;
        _playStage = (PlayStage)stages.First(n => n is PlayStage);
    }

    [Command("event", Permission.Coordinator)]
    public void Event(IClient client, string arguments)
    {
        arguments.SplitCommand(out string subCommand, out string subArguments);
        switch (subCommand)
        {
            case "start":
            {
                subArguments.SplitCommand(out string time);
                if (string.IsNullOrWhiteSpace(time))
                {
                    switch (_eventService.CurrentStage)
                    {
                        case IntroStage introStage:
                            introStage.AutoStart(client);
                            break;

                        case PlayStage playStage:
                            playStage.AutoStart(client);
                            break;
                    }

                    return;
                }

                if (!int.TryParse(time, out int startTimer))
                {
                    throw new CommandParseException(time);
                }

                TimeSpan startDur = TimeSpan.FromSeconds(startTimer);
                _ = _eventService.CurrentStage switch
                {
                    IntroStage introStage => introStage.Start(startDur, client),
                    PlayStage playStage => playStage.Start(startDur, client),
                    _ => null
                };

                break;
            }

            case "play":
            {
                subArguments.SplitCommand(out string time);
                if (string.IsNullOrWhiteSpace(time))
                {
                    switch (_eventService.CurrentStage)
                    {
                        case IntroStage introStage:
                            introStage.AutoPlay(client);
                            break;

                        case PlayStage playStage:
                            playStage.AutoPlay(client);
                            break;
                    }

                    return;
                }

                if (!int.TryParse(time, out int nextTimer))
                {
                    throw new CommandParseException(time);
                }

                TimeSpan nextDur = TimeSpan.FromSeconds(nextTimer);
                _ = _eventService.CurrentStage switch
                {
                    IntroStage introStage => introStage.Play(nextDur, client),
                    PlayStage playStage => playStage.Play(nextDur, client),
                    _ => null
                };

                break;
            }

            case "stop":
                subArguments.TooMany();

                switch (_eventService.CurrentStage)
                {
                    case IntroStage introStage:
                        introStage.Stop(client);
                        break;

                    case PlayStage playStage:
                        playStage.Stop(client);
                        break;
                }

                _listenerService.AllClients(n => n.SendOpcode(ClientOpcode.StopLevel));
                break;

            case "index":
            {
                subArguments.SplitCommand(out string indexLine, out string flags);
                indexLine.NotEnough();
                int index;
                switch (indexLine)
                {
                    case "p":
                        index = _mapService.Index - 1;
                        break;
                    case "n":
                        index = _mapService.Index + 1;
                        break;
                    default:
                    {
                        if (!int.TryParse(indexLine, out index))
                        {
                            throw new CommandParseException(indexLine);
                        }

                        break;
                    }
                }

                if (index < 0 || index >= _mapService.MapCount)
                {
                    client.SendServerMessage(
                        "Cannot set map to out of range index [{Index}], valid indices are: [0-{MaxIndex}]",
                        index,
                        _mapService.MapCount - 1);
                    return;
                }

                flags = flags.GetFlags();
                _playStage.SetIndex(index, client, flags.Contains('s'), flags.Contains('a'));
                client.SendServerMessage("Map set to [{Map}] ({Index})", _mapService.CurrentMap.Name, index);

                break;
            }

            case "status":
                subArguments.TooMany();
                _eventService.PrintStatus(client);

                break;

            case "stage":
            {
                subArguments = subArguments.Unwrap();
                if (string.IsNullOrWhiteSpace(subArguments))
                {
                    _eventService.PrintStage(client);
                    return;
                }

                int index;
                switch (subArguments)
                {
                    case "p":
                        index = _eventService.StageIndex - 1;
                        break;
                    case "n":
                        index = _eventService.StageIndex + 1;
                        break;
                    default:
                    {
                        if (!int.TryParse(subArguments, out index))
                        {
                            throw new CommandParseException(subArguments);
                        }

                        break;
                    }
                }

                if (index < 0 || index >= _eventService.Stages.Length)
                {
                    client.SendServerMessage(
                        "Cannot set stage to out of range index [{Index}], valid indices are: [0-{MaxIndex}]",
                        index,
                        _eventService.Stages.Length - 1);
                    return;
                }

                _eventService.SetStage(index, client);

                break;
            }

            default:
                throw new CommandUnrecognizedSubcommandException("event", subCommand);
        }
    }
}
