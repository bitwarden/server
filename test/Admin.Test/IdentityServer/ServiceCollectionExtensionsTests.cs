using Bit.Admin;
using Bit.Admin.IdentityServer;
using Microsoft.Extensions.DependencyInjection;

namespace Admin.Test.IdentityServer;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAdminUpstreamOidc_ThrowsWhenEmailClaimTypeIsNull()
    {
        var services = new ServiceCollection();
        var adminSettings = new AdminSettings
        {
            Oidc = new AdminSettings.OidcSettings
            {
                Authority = "https://idp.example.com",
                ClientId = "id",
                ClientSecret = "secret",
                EmailClaimType = null,
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddAdminUpstreamOidc(adminSettings));

        Assert.Contains("EmailClaimType", ex.Message);
    }
}
