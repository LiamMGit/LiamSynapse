using JetBrains.Annotations;
using Synapse.Server.Models;

namespace Synapse.Server.Extras;

// TODO: add help command
[MeansImplicitUse]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class CommandAttribute : Attribute
{
    public CommandAttribute(string command)
    {
        Command = command;
    }

    public CommandAttribute(string command, Permission permission)
    {
        Command = command;
        Permission = permission;
    }

    public string Command { get; }

    public Permission? Permission { get; }
}
