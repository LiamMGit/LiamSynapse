using System.Collections.Immutable;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Synapse.Server.Clients;
using Synapse.Server.Commands;
using Synapse.Server.Extras;
using Synapse.Server.Models;

namespace Synapse.Server.Services;

public interface ICommandService
{
    public void Run();
}

public class CommandService : ICommandService
{
    private readonly ImmutableDictionary<string, CommandInfo> _commands;
    private readonly IClient _serverClient;

    public CommandService(
        IServiceProvider provider,
        IListenerService listenerService,
        IClient serverClient)
    {
        _serverClient = serverClient;

        Dictionary<string, CommandInfo> commands = new();
        Register<EventCommand>();
        Register<MessageCommand>();
        Register<ScoresCommand>();
        Register<BlacklistCommand>();
        Register<ClientCommand>();
        _commands = commands.ToImmutableDictionary();
        listenerService.CommandReceived += SendCommand;
        return;

        void Register<T>()
        {
            object instance = ActivatorUtilities.CreateInstance(provider, typeof(T));
            foreach (MethodInfo methodInfo in typeof(T).GetMethods())
            {
                CommandAttribute[] attributes = methodInfo.GetCustomAttributes<CommandAttribute>().ToArray();
                foreach (CommandAttribute attribute in attributes)
                {
                    commands.Add(attribute.Command, new CommandInfo(instance, methodInfo, attribute));
                }
            }
        }
    }

    public void Run()
    {
        while (true)
        {
            string? line = Console.ReadLine();

            if (line == "quit")
            {
                return;
            }

            SendCommand(_serverClient, line);
        }
    }

    public void SendCommand(IClient client, string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        line.SplitCommand(out string message, out string arguments);

        if (_commands.TryGetValue(message, out CommandInfo command))
        {
            try
            {
                Permission? permission = command.Attribute.Permission;
                if (permission == null || client.HasPermission(permission.Value))
                {
                        command.MethodInfo.Invoke(command.Instance, [client, arguments]);
                }
                else
                {
                    throw new CommandPermissionException();
                }
            }
            catch (Exception e)
            {
                client.SendServerMessage("{ExceptionMessage}", e.InnerException?.Message ?? e.Message);
            }
        }
        else
        {
            client.SendServerMessage("Did not recognize command [{Message}]", message);
        }
    }

    private readonly struct CommandInfo(object instance, MethodInfo methodInfo, CommandAttribute attribute) : IEquatable<CommandInfo>
    {
        public object Instance { get; } = instance;

        public MethodInfo MethodInfo { get; } = methodInfo;

        public CommandAttribute Attribute { get; } = attribute;

        public bool Equals(CommandInfo other)
        {
            return Instance.Equals(other.Instance) && MethodInfo.Equals(other.MethodInfo) && Attribute.Equals(other.Attribute);
        }

        public override bool Equals(object? obj)
        {
            return obj is CommandInfo other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Instance, MethodInfo, Attribute);
        }
    }
}
