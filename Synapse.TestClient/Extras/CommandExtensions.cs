namespace Synapse.TestClient.Extras;

public static class CommandExtensions
{
    public static void SplitCommand(this string input, out string command, out string arguments)
    {
        int index = input.IndexOf(' ');
        command = index == -1 ? input : input[..index];
        arguments = index == -1 ? string.Empty : input[(index + 1)..];
    }
}
