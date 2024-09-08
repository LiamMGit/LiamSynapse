using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
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
        if (!TryGetClient(client, extra, flags.IdFlag(), out IClient? target))
        {
            return;
        }

        listenerService.Whitelist(target);
        client.LogAndSend(log, "Whitelisted [{Client}]", target);
    }

    [Command("ban", Permission.Moderator)]
    public void Ban(IClient client, string arguments)
    {
        string flags = arguments.GetFlags(out string extra);
        if (!TryGetClient(client, extra, flags.IdFlag(), out IClient? target))
        {
            return;
        }

        listenerService.Blacklist(target);
        client.LogAndSend(log, "Banned [{Client}]", target);
    }

    [Command("banip", Permission.Moderator)]
    public void BanIp(IClient client, string arguments)
    {
        string flags = arguments.GetFlags(out string extra);
        if (!TryGetClient(client, extra, flags.IdFlag(), out IClient? target))
        {
            return;
        }

        listenerService.BanIp(target);
        client.LogAndSend(log, "Ip banned [{Client}]", target);
    }

    [Command("bannedips", Permission.Moderator)]
    public void BannedIps(IClient client, string arguments)
    {
        arguments.SplitCommand(out string subCommand, out string subArguments);
        switch (subCommand)
        {
            case "reload":
                blacklistService.LoadBannedIps(true);
                client.SendIfClient("Reloaded banned ips");
                break;

            case "list":
                if (blacklistService.BannedIps.Count > 0)
                {
                    client.SendServerMessage("{Bannedips}", string.Join(", ", blacklistService.BannedIps.Values));
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
                        ? "Added [({Ip}] to bannedips"
                        : "Failed to add [({Ip}] to bannedips, ip already exists",
                    subArguments);
                break;

            case "remove":
                if (blacklistService.BannedIps.Keys.TryScanQuery(client, subArguments, n => n, out string? ip))
                {
                    client.LogAndSend(
                        log,
                        blacklistService.RemoveBannedIp(ip)
                            ? "Removed [({Ip}] from bannedips"
                            : "Failed to remove [({Ip}] from bannedips, id not found",
                        ip);
                }

                break;

            default:
                client.SendServerMessage("Did not recognize bannedips subcommand [{Message}]", subCommand);
                break;
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
                client.LogAndSend(
                    log,
                    blacklistService.AddBlacklist(id, username)
                        ? "Added [({Id}) {Username}] to blacklist"
                        : "Failed to add [({Id}) {Username}] to blacklist, id already exists",
                    id,
                    username);

                break;

            case "remove":
                string flags = subArguments.GetFlags(out string subSubArguments);
                if (TryGetSerializedUser(
                        client,
                        subSubArguments,
                        SerializedUserIdFlag(flags),
                        blacklistService.Blacklist.Values,
                        out SerializedUser? user))
                {
                    client.LogAndSend(
                        log,
                        blacklistService.RemoveBlacklist(user)
                            ? "Removed [{User}] from blacklist"
                            : "Failed to remove [{User}] from blacklist, id not found",
                        user);
                }

                break;

            default:
                client.SendServerMessage("Did not recognize blacklist subcommand [{Message}]", subCommand);
                break;
        }
    }

    [Command("kick", Permission.Moderator)]
    public void Kick(IClient client, string arguments)
    {
        arguments.SplitCommand(out string id, out string extra);
        string flags = extra.GetFlags(out string reason);
        if (!TryGetClient(client, id, flags.IdFlag(), out IClient? target))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            reason = "Kicked";
        }

        _ = target.Disconnect(reason);
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
                client.SendServerMessage("Did not recognize roles subcommand [{Message}]", subCommand);
                break;
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
            if (roleService.Roles.Values.TryScanQuery(client, roleName, n => n.Name, out Role? role) &&
                TryGetClient(client, username, func, out IClient? target))
            {
                client.LogAndSend(
                    log,
                    action(target, role),
                    role.Name,
                    target);
            }
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
                if (TryGetSerializedUser(
                        client,
                        subSubArguments,
                        SerializedUserIdFlag(flags),
                        blacklistService.Whitelist.Values,
                        out SerializedUser? user))
                {
                    client.LogAndSend(
                        log,
                        blacklistService.RemoveWhitelist(user)
                            ? "Removed [{User}] from whitelist"
                            : "Failed to remove [{User}] from whitelist, id not found",
                        user);
                }

                break;

            default:
                client.SendServerMessage("Did not recognize whitelist subcommand [{Message}]", subCommand);
                break;
        }
    }

    private static bool TryGetSerializedUser(
        IClient client,
        string arguments,
        Func<SerializedUser, string> func,
        IEnumerable<SerializedUser> users,
        [NotNullWhen(true)] out SerializedUser? result)
    {
        return users.TryScanQuery(client, arguments, func, out result);
    }

    private bool TryGetClient(
        IClient client,
        string arguments,
        Func<IClient, string> func,
        [NotNullWhen(true)] out IClient? result)
    {
        return listenerService.Clients.Values.TryScanQuery(client, arguments, func, out result) &&
               result.GetImmunity() < client.GetImmunity();
    }
}
