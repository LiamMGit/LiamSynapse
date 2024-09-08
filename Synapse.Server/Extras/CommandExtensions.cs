using System.Diagnostics.CodeAnalysis;
using System.Text;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Synapse.Server.Clients;

namespace Synapse.Server.Extras;

public static class CommandExtensions
{
    public static Func<IClient, string> ById => n => n.Id;

    public static Func<IClient, string> ByUsername => n => n.Username;

    [Pure]
    public static string GetFlags(this string input)
    {
        return GetFlags(input, out _);
    }

    [Pure]
    public static string GetFlags(this string input, out string extra)
    {
        string[] args = input.Split(' ');
        ILookup<bool, string> split = args.ToLookup(n => n.StartsWith('-'));
        extra = string.Join(' ', split[false]);
        return split[true].Select(n => n[1..]).Join();
    }

    public static Func<IClient, string> IdFlag(this string input)
    {
        return input.Contains('i') ? ById : ByUsername;
    }

    public static void LogAndSend(
        this IClient client,
        ILogger logger,
        [StructuredMessageTemplate] string message,
        params object?[] args)
    {
        // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
#pragma warning disable CA2254
        logger.LogInformation(message, args);
#pragma warning restore CA2254
        client.SendIfClient(message, args);
    }

    public static void SendIfClient(
        this IClient client,
        [StructuredMessageTemplate] string message,
        params object?[] args)
    {
        if (client is not ServerClient)
        {
            _ = client.SendServerMessage(message, args);
        }
    }

    public static void SplitCommand(this string input, out string command, out string arguments)
    {
        int index = input.IndexOf(' ');
        command = index == -1 ? input : input[..index];
        arguments = index == -1 ? string.Empty : input[(index + 1)..];
    }

    public static bool TryScanQuery<T>(
        this IEnumerable<T> list,
        IClient client,
        string arguments,
        Func<T, string> func,
        [NotNullWhen(true)] out T? result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(arguments))
        {
            client.SendServerMessage("Invalid arguments");
            return false;
        }

        T[] query = list
            .Where(n => func(n).StartsWith(arguments, StringComparison.CurrentCultureIgnoreCase))
            .ToArray();

        switch (query.Length)
        {
            case > 1:
                client.SendServerMessage(
                    $"Ambiguous match found: [{string.Join(", ", query)}]");
                break;

            case <= 0:
                client.SendServerMessage($"Could not find [{arguments}]");
                break;

            default:
                result = query[0]!;
                return true;
        }

        return false;
    }

    private static string Join(this IEnumerable<string> strings)
    {
        StringBuilder builder = new();
        foreach (string s in strings)
        {
            builder.Append(s);
        }

        return builder.ToString();
    }
}
