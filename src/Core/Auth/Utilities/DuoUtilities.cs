using Bit.Core.Auth.Models;

namespace Bit.Core.Auth.Utilities;

public class DuoUtilities
{
    public static bool HasProperDuoMetadata(TwoFactorProvider provider)
    {
        return provider?.MetaData != null &&
               provider.MetaData.ContainsKey("ClientId") &&
               provider.MetaData.ContainsKey("ClientSecret") &&
               provider.MetaData.ContainsKey("Host") &&
               ValidDuoHost((string)provider.MetaData["Host"]);
    }

    public static bool ValidDuoHost(string host)
    {
        if (Uri.TryCreate($"https://{host}", UriKind.Absolute, out var uri))
        {
            return (string.IsNullOrWhiteSpace(uri.PathAndQuery) || uri.PathAndQuery == "/") &&
                uri.Host.StartsWith("api-") &&
                (uri.Host.EndsWith(".duosecurity.com") || uri.Host.EndsWith(".duofederal.com"));
        }
        return false;
    }
}
