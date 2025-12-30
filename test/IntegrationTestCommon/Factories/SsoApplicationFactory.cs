using System.Collections.Concurrent;
using Bit.Sso;
using Microsoft.AspNetCore.Hosting;

namespace Bit.IntegrationTestCommon.Factories;

public class SsoApplicationFactory : WebApplicationFactoryBase<Startup>
{
    public const string DefaultDeviceIdentifier = "92b9d953-b9b6-4eaf-9d3e-11d57144dfeb";

    /// <summary>
    /// MIght need this later for something token related.
    /// </summary>
    public ConcurrentDictionary<string, string> RegistrationTokens { get; private set; } = new ConcurrentDictionary<string, string>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
    }
}
