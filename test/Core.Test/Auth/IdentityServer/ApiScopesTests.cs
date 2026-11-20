using Bit.Core.Auth.IdentityServer;
using Xunit;

namespace Bit.Core.Test.Auth.IdentityServer;

public class ApiScopesTests
{
    [Fact]
    public void OrganizationApiKeyScopes_DoesNotContainLegacyOrganizationScope()
    {
        Assert.DoesNotContain(ApiScopes.ApiOrganization, ApiScopes.OrganizationApiKeyScopes);
    }
}
