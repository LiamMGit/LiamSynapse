using Microsoft.Extensions.Logging;
using Synapse.Networking.Models;
using Synapse.Server.Clients;
using Synapse.Server.Extras;
using Synapse.Server.Models;
using Synapse.Server.Services;

namespace Synapse.Server.Commands;

public class BlacklistCommand(
    ILogger<BlacklistCommand> log,
    IListenerService listenerService,
    IBlacklistService blacklistService,
    IRoleService roleService)
{
    public static Func<SerializedUser, string> SerializedUserIdFlag(string input)
    {
        return input.Contains('i') ? n => n.Id : n => n.Username;
    }

    [Command("allow", Permission.Moderator)]
    public void Allow(IClient client, string arguments)
    {
        string flags = arguments.GetFlags(out string extra);
        IClient target = GetClient(client, extra.Unwrap(), flags.IdFlag());
        listenerService.Whitelist(target);
        client.LogAndSend(log, "Whitelisted [{Client}]", target);
    }

    [Command("ban", Permission.Moderator)]
    public void Ban(IClient client, string arguments)
    {
        string flags = arguments.GetFlags(out string extra);
        extra.SplitCommand(out string subCommand, out string subArguments);
        subArguments.SplitCommand(out string subSubCommand, out string subSubArguments);

        TimeSpan? timeSpan = null;
        if (!string.IsNullOrWhiteSpace(subSubArguments))
        {
            timeSpan = TimeSpan.Parse(subSubArguments); // TODO: spruce up this parsing
        }

        IClient target = GetClient(client, subCommand.Unwrap(), flags.IdFlag());
        listenerService.Blacklist(target, subSubCommand, timeSpan != null ? DateTime.UtcNow + timeSpan : null);
        client.LogAndSend(
            log,
            "Banned [{Client}] for [{TimeSpan}] with reason: [{Reason}]",
            target,
            timeSpan != null ? timeSpan.Value : "permanently",
            subSubCommand);
    }

    [Command("banip", Permission.Moderator)]
    public void BanIp(IClient client, string arguments)
    {
        string flags = arguments.GetFlags(out string extra);
        IClient target = GetClient(client, extra.Unwrap(), flags.IdFlag());
        listenerService.BanIp(target);
        client.LogAndSend(log, "Ip banned [{Client}]", target);
    }

    [Command("bannedips", Permission.Moderator)]
    public void BannedIps(IClient client, string arguments)
    {
        arguments.SplitCommand(out string subCommand, out string subArguments);
        subArguments = subArguments.Unwrap();
        switch (subCommand)
        {
            case "reload":
                blacklistService.LoadBannedIps(true);
                client.SendIfClient("Reloaded banned ips");
                break;

            case "list":
                if (blacklistService.BannedIps.Count > 0)
                {
                    client.SendServerMessage("{BannedIps}", string.Join(", ", blacklistService.BannedIps.Values));
                }
                else
                {
                    client.SendServerMessage("Banned ips list currently empty");
                }

                break;

            case "add":
                client.LogAndSend(
                    log,
                    blacklistService.AddBannedIp(subArguments)
                        ? "Added [{Ip}] to bannedips"
                        : "Failed to add [{Ip}] to bannedips, ip already exists",
                    subArguments);
                break;

            case "remove":
                string ip = blacklistService.BannedIps.Keys.ScanQuery(subArguments, n => n);
                client.LogAndSend(
                    log,
                    blacklistService.RemoveBannedIp(ip)
                        ? "Removed [{Ip}] from bannedips"
                        : "Failed to remove [{Ip}] from bannedips, id not found",
                    ip);

                break;

            default:
                throw new CommandUnrecognizedSubcommandException("bannedips", subCommand);
        }
    }

    [Command("blacklist", Permission.Moderator)]
    public void Blacklist(IClient client, string arguments)
    {
        arguments.SplitCommand(out string subCommand, out string subArguments);
        switch (subCommand)
        {
            case "reload":
                _ = blacklistService.LoadBlacklist(true);
                client.SendIfClient("Reloaded blacklist");
                break;

            case "list":
                if (blacklistService.Blacklist.Count > 0)
                {
                    client.SendServerMessage("{Blacklist}", string.Join(", ", blacklistService.Blacklist.Values));
                }
                else
                {
                    client.SendServerMessage("Blacklist currently empty");
                }

                break;

            case "add":
                subArguments.SplitCommand(out string id, out string username);
                username = username.Unwrap();
                client.LogAndSend(
                    log,
                    blacklistService.AddBlacklist(id, username, null, null)
                        ? "Added {Username} ({Id}) to blacklist"
                        : "Failed to add {Username} ({Id}) to blacklist, id already exists",
                    id,
                    username);

                break;

            case "remove":
                string flags = subArguments.GetFlags(out string subSubArguments);
                subSubArguments = subSubArguments.Unwrap();
                SerializedUser user = GetSerializedUser(
                    subSubArguments,
                    SerializedUserIdFlag(flags),
                    blacklistService.Blacklist.Values);
                client.LogAndSend(
                    log,
                    blacklistService.RemoveBlacklist(user)
                        ? "Removed [{User}] from blacklist"
                        : "Failed to remove [{User}] from blacklist, id not found",
                    user);

                break;

            default:
                throw new CommandUnrecognizedSubcommandException("blacklist", subCommand);
        }
    }

    [Command("kick", Permission.Moderator)]
    public void Kick(IClient client, string arguments)
    {
        string flags = arguments.GetFlags(out string extra);
        extra.SplitCommand(out string id, out string reason);
        reason = reason.Unwrap();
        IClient target = GetClient(client, id, flags.IdFlag());

        if (string.IsNullOrWhiteSpace(reason))
        {
            reason = "Kicked";
        }

        // TODO: implement reason
        _ = target.Disconnect(DisconnectCode.Banned);
        client.LogAndSend(log, "Kicked [{Client}] ({Reason})", target, reason);
    }

    // TODO: maybe add commands for manipulating roles themselves
    [Command("roles", Permission.Coordinator)]
    public void Roles(IClient client, string arguments)
    {
        arguments.SplitCommand(out string subCommand, out string subArguments);
        string flags = subArguments.GetFlags(out string extra);
        switch (subCommand)
        {
            case "reload":
                roleService.LoadRoles(true);
                roleService.LoadAdmins(true);
                client.SendIfClient("Reloaded roles");
                break;

            case "list":
                if (roleService.RoleDatas.Count > 0)
                {
                    client.SendServerMessage("{Admins}", string.Join(", ", roleService.RoleDatas.Values));
                }
                else
                {
                    client.SendServerMessage("Admins currently empty");
                }

                break;

            case "listroles":
                if (roleService.Roles.Count > 0)
                {
                    client.SendServerMessage("{Roles}", string.Join(", ", roleService.Roles.Values));
                }
                else
                {
                    client.SendServerMessage("Roles currently empty");
                }

                break;

            /*case "adduser":
            {
                subArguments.SplitCommand(out string id, out string username);
                client.LogAndSend(_log,
                    _blacklistService.AddRoleUser(id, username)
                        ? "Added [({Id}) {Username}] to role list"
                        : "Failed to add [({Id}) {Username}] to role list, id already exists", id, username);
            }

                break;

            case "removeuser":
            {
                string flags2 = subArguments.GetFlags(out string subSubArguments);
                if (TryGetSerializedUser(
                        client,
                        subSubArguments,
                        SerializedUserIdFlag(flags2),
                        _blacklistService.Roles.Values,
                        out SerializedUser? user))
                {
                    client.LogAndSend(_log,
                        _blacklistService.RemoveRoleUser(user)
                            ? "Removed [{User}] from role list"
                            : "Failed to remove [{User}] from role list, id not found", user);
                }
            }

                break;*/

            case "add":
                FindByClient(flags.IdFlag(), Add);
                break;

            case "remove":
                FindByClient(flags.IdFlag(), Remove);
                break;

            default:
                throw new CommandUnrecognizedSubcommandException("roles", subCommand);
        }

        return;

        string Add(IClient target, Role role)
        {
            return roleService.AddRole(target.Id, target.Username, role)
                ? "Added [{Role}] to [{Target}]"
                : "Failed to add [{Role}] to [{Target}], role already exists";
        }

        string Remove(IClient target, Role role)
        {
            return roleService.RemoveRole(target.Id, role)
                ? "Removed [{Role}] from [{Target}]"
                : "Failed to remove [{Role}] from [{Target}], role not found";
        }

        void FindByClient(Func<IClient, string> func, Func<IClient, Role, string> action)
        {
            extra.SplitCommand(out string username, out string roleName);
            roleName = roleName.Unwrap();
            Role role = roleService.Roles.Values.ScanQuery(roleName, n => n.Name);
            IClient target = GetClient(client, username, func);
            client.LogAndSend(
                log,
                action(target, role),
                role.Name,
                target);
            /*else if (_blacklistService.Roles.Values.TryScanQuery(client, username, n => n.Username, out RoleUser? user))
            {
                client.LogAndSend(_log,
                    _blacklistService.AddRole(user, role)
                        ? "Added [{Role}] to [{User}]"
                        : "Failed to add [{Role}] to [{User}], role already exists", role, user);
            }*/
        }
    }

    [Command("whitelist", Permission.Moderator)]
    public void Whitelist(IClient client, string arguments)
    {
        arguments.SplitCommand(out string subCommand, out string subArguments);
        switch (subCommand)
        {
            case "reload":
                blacklistService.LoadWhitelist(true);
                client.SendIfClient("Reloaded whitelist");
                break;

            case "list":
                if (blacklistService.Whitelist == null)
                {
                    client.SendServerMessage("No active whitelist");
                }
                else
                {
                    if (blacklistService.Whitelist.Count > 0)
                    {
                        client.SendServerMessage("{Whitelist}", string.Join(", ", blacklistService.Whitelist.Values));
                    }
                    else
                    {
                        client.SendServerMessage("Whitelist currently empty");
                    }
                }

                break;

            case "add":
                subArguments.SplitCommand(out string id, out string username);
                username = username.Unwrap();
                client.LogAndSend(
                    log,
                    blacklistService.AddWhitelist(id, username)
                        ? "Added [({Id}) {Username}] to whitelist"
                        : "Failed to add [({Id}) {Username}] to whitelist, id already exists",
                    id,
                    username);
                break;

            case "remove":
                if (blacklistService.Whitelist == null)
                {
                    client.SendServerMessage("No active whitelist");
                    return;
                }

                string flags = subArguments.GetFlags(out string subSubArguments);
                SerializedUser user = GetSerializedUser(
                    subSubArguments.Unwrap(),
                    SerializedUserIdFlag(flags),
                    blacklistService.Whitelist.Values);
                client.LogAndSend(
                    log,
                    blacklistService.RemoveWhitelist(user)
                        ? "Removed [{User}] from whitelist"
                        : "Failed to remove [{User}] from whitelist, id not found",
                    user);

                break;

            default:
                throw new CommandUnrecognizedSubcommandException("whitelist", subCommand);
        }
    }

    private static SerializedUser GetSerializedUser(
        string arguments,
        Func<SerializedUser, string> func,
        IEnumerable<SerializedUser> users)
    {
        return users.ScanQuery(arguments, func);
    }

    private IClient GetClient(
        IClient client,
        string arguments,
        Func<IClient, string> func)
    {
        IClient result = listenerService.Clients.Values.ScanQuery(arguments, func);
        if (result.GetImmunity() >= client.GetImmunity())
        {
            throw new CommandException("Target has greater or equal immunity");
        }

        return result;
    }
}
