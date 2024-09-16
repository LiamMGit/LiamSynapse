using System.Text;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Synapse.Server.Clients;
using Synapse.Server.Commands;

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
        bool quoting = false;
        StringBuilder extraBuilder = new();
        StringBuilder flagBuilder = new();
        StringBuilder active = extraBuilder;
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            switch (c)
            {
                case '"':
                    quoting = !quoting;
                    active = extraBuilder;
                    break;

                case '-' when !quoting:
                    active = flagBuilder;
                    continue;

                case ' ':
                    if (!quoting && active == flagBuilder)
                    {
                        active = extraBuilder;
                        continue;
                    }

                    if (i + 1 < input.Length &&
                        input[i + 1] == '-')
                    {
                        continue;
                    }

                    break;
            }

            active.Append(c);
        }

        extra = extraBuilder.ToString();
        return flagBuilder.ToString();
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

    public static void SplitCommand(this string input, out string command)
    {
        input.SplitCommand(out command, out string arguments);
        if (arguments != string.Empty)
        {
            throw new CommandTooManyArgumentException();
        }
    }

    public static void SplitCommand(this string input, out string command, out string arguments)
    {
        bool escaping = false;
        bool quoting = false;
        bool quoted = false;
        StringBuilder commandBuilder = new();
        StringBuilder argumentBuilder = new();
        StringBuilder active = commandBuilder;
        foreach (char c in input)
        {
            if (escaping)
            {
                escaping = false;
            }
            else
            {
                switch (c)
                {
                    case '\\' when active == commandBuilder:
                    {
                        escaping = true;
                        continue;
                    }

                    case '"' when active == commandBuilder:
                    {
                        if (!quoted)
                        {
                            quoting = true;
                            quoted = true;
                            continue;
                        }

                        if (quoting)
                        {
                            quoting = false;
                            continue;
                        }

                        break;
                    }

                    case ' ' when !quoting && active == commandBuilder:
                        active = argumentBuilder;
                        continue;
                }
            }

            active.Append(c);
        }

        command = commandBuilder.ToString();
        arguments = argumentBuilder.ToString();
    }

    public static string[] SplitArguments(this string input)
    {
        bool escaping = false;
        bool quoting = false;
        List<StringBuilder> builders = [];
        StringBuilder active = new();
        foreach (char c in input)
        {
            if (escaping)
            {
                escaping = false;
            }
            else
            {
                switch (c)
                {
                    case '\\':
                    {
                        escaping = true;
                        continue;
                    }

                    case '"':
                        if (quoting)
                        {
                            FinishActive();
                        }

                        quoting = !quoting;
                        continue;

                    case ' ' when !quoting:
                        FinishActive();
                        continue;
                }
            }

            active.Append(c);
        }

        FinishActive();

        return builders.Select(n => n.ToString()).ToArray();

        void FinishActive()
        {
            if (active.Length == 0)
            {
                return;
            }

            builders.Add(active);
            active = new StringBuilder();
        }
    }

    public static string Unwrap(this string input)
    {
        bool escaping = false;
        bool quoting = false;
        StringBuilder builder = new();
        foreach (char c in input)
        {
            if (escaping)
            {
                escaping = false;
            }
            else
            {
                switch (c)
                {
                    case '\\':
                    {
                        escaping = true;
                        continue;
                    }

                    case '"':
                    {
                        if (quoting)
                        {
                            return builder.ToString();
                        }

                        quoting = true;
                        continue;
                    }
                }
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    public static void NotEnough(this string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new CommandNotEnoughArgumentException();
        }
    }

    public static void TooMany(this string input)
    {
        if (!string.IsNullOrWhiteSpace(input))
        {
            throw new CommandTooManyArgumentException();
        }
    }

    public static T ScanQuery<T>(
        this IEnumerable<T> list,
        string arguments,
        Func<T, string> func,
        bool showMatches = true)
    {
        T[] query = list
            .Where(n => func(n).StartsWith(arguments, StringComparison.CurrentCultureIgnoreCase))
            .ToArray();

        return query.Length switch
        {
            > 1 => throw new CommandException(showMatches ? $"Ambiguous match found: [{string.Join(", ", query)}]" : "Ambiguous match found"),
            <= 0 => throw new CommandQueryFailedException(arguments),
            _ => query[0]
        };
    }
}
