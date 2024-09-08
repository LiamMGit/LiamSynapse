using System.Text.RegularExpressions;

namespace Synapse.Server.Extras;

public static partial class StringUtils
{
    public static string Sanitize(string message)
    {
        try
        {
            const int maxLength = 200;
            return OnlyASCII().Replace(message.Length > maxLength ? message[..maxLength] : message, "?");
        }
        catch (RegexMatchTimeoutException)
        {
            return string.Empty;
        }
    }

    [GeneratedRegex(@"[^\u0020-\u007E]", RegexOptions.None, 1000)]
    private static partial Regex OnlyASCII();
}
