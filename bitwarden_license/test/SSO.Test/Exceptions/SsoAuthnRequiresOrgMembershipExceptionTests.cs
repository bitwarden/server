using Bit.Sso.Exceptions;

namespace Bit.SSO.Test.Exceptions;

public class SsoAuthnRequiresOrgMembershipExceptionTests
{
    [Fact]
    public void Constructor_AssignsProperties()
    {
        var orgId = Guid.NewGuid();
        var ex = new SsoAuthnRequiresOrgMembershipException(
            organizationId: orgId,
            organizationDisplayName: "Acme Corp",
            userEmail: "user@example.com");

        Assert.Equal(orgId, ex.OrganizationId);
        Assert.Equal("Acme Corp", ex.OrganizationDisplayName);
        Assert.Equal("user@example.com", ex.UserEmail);
    }

    [Fact]
    public void Constructor_SetsDescriptiveMessage()
    {
        var ex = new SsoAuthnRequiresOrgMembershipException(
            organizationId: Guid.NewGuid(),
            organizationDisplayName: "Acme Corp",
            userEmail: "user@example.com");

        // The message is used by server logs, not the redirect URL,
        // so we just sanity-check it includes the org name for diagnosability.
        Assert.Contains("Acme Corp", ex.Message);
    }
}
