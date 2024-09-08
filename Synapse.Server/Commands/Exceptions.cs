namespace Synapse.Server.Commands;

public class CommandException(string message) : Exception(message);

public class CommandNotEnoughArgumentException() : CommandException("Not enough arguments");

public class CommandTooManyArgumentException() : CommandException("Too many arguments");

public class CommandPermissionException() : CommandException("You do not have permission to do that");

public class CommandChatterException() : CommandException("You must join the chat to do that");

public class CommandQueryFailedException(string query) : CommandException($"Could not find [{query}]");

public class CommandUnrecognizedSubcommandException(string group, string subCommand) : CommandException($"Did not recognize [{group}] subcommand [{subCommand}]");

public class CommandParseException(object obj) : CommandException($"Could not parse [{obj}]");

public class CommandInvalidMapIndexException(int index, int max) : CommandException($"Invalid map index [{index}], must be within 0 - [{max - 1}]");
