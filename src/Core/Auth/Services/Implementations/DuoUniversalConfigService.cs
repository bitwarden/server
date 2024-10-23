using Bit.Core.Auth.Utilities;
using Duo = DuoUniversal;

namespace Bit.Core.Auth.Services;

public class DuoUniversalConfigService() : IDuoUniversalConfigService
{
    public async Task<bool> ValidateDuoConfiguration(string clientSecret, string clientId, string host)
    {
        // Do some simple checks to ensure data integrity
        if (!DuoUtilities.ValidDuoHost(host) &&
            string.IsNullOrWhiteSpace(clientSecret) &&
            string.IsNullOrWhiteSpace(clientId))
        {
            return false;
        }
        // The AuthURI is not important for this health check so we pass in a non-empty string
        var client = new Duo.ClientBuilder(clientId, clientSecret, host, "non-empty").Build();

        // This could throw an exception, the false flag will allow the exception to bubble up
        return await client.DoHealthCheck(false);
    }
}
