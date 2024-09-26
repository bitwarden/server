namespace Bit.Core.Auth.Utilities;

public class DuoUtilities
{
    public static bool ValidHost(string host)
    {
        if (Uri.TryCreate($"https://{host}", UriKind.Absolute, out var uri))
        {
            return (string.IsNullOrWhiteSpace(uri.PathAndQuery) || uri.PathAndQuery == "/") &&
                uri.Host.StartsWith("api-") &&
                (uri.Host.EndsWith(".duosecurity.com") || uri.Host.EndsWith(".duofederal.com"));
        }
        throw new ArgumentException("Invalid Duo host configured.", nameof(host));
    }
}

