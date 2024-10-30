using Bit.Core.Auth.Models;

namespace Bit.Core.Auth.Utilities;

public class DuoUtilities
{
    /// <summary>
    /// We are checking for existenace because we handle validation for Duo when we create or update
    /// the configuration in the DuoUniversalConfigService. Users are not able to save an invalid config.
    /// This is just a simple check to ensure the metadata is present.
    /// </summary>
    /// <param name="provider">The Duo TwoFactor Provider</param>
    /// <returns></returns>
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
