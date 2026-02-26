using Bit.IntegrationTestCommon.Factories;

namespace Bit.Sso.IntegrationTest.Utilities;

public class SsoApplicationFactory : WebApplicationFactoryBase<Startup>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
    }
}
