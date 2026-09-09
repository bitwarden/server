using Bit.Sso.Exceptions;

namespace Bit.SSO.Test.Exceptions;

public class SsoAuthnStagedOrgUserRequiresInviteAcceptanceExceptionTests
{
    [Fact]
    public void Constructor_AssignsProperties()
    {
        var orgId = Guid.NewGuid();
        var ex = new SsoAuthnStagedOrgUserRequiresInviteAcceptanceException(
            organizationId: orgId,
            organizationDisplayName: "Acme Corp",
            userEmail: "staged@example.com");

        Assert.Equal(orgId, ex.OrganizationId);
        Assert.Equal("Acme Corp", ex.OrganizationDisplayName);
        Assert.Equal("staged@example.com", ex.UserEmail);
    }

    [Fact]
    public void Constructor_SetsDescriptiveMessage()
    {
        var ex = new SsoAuthnStagedOrgUserRequiresInviteAcceptanceException(
            organizationId: Guid.NewGuid(),
            organizationDisplayName: "Acme Corp",
            userEmail: "staged@example.com");

        // The message is used by server logs/error pages, not the redirect URL,
        // so we just sanity-check it includes the org name for diagnosability.
        Assert.Contains("Acme Corp", ex.Message);
    }
}
