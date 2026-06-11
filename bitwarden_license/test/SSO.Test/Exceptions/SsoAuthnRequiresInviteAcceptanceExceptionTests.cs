using Bit.Sso.Exceptions;

namespace Bit.SSO.Test.Exceptions;

public class SsoAuthnRequiresInviteAcceptanceExceptionTests
{
    [Fact]
    public void Constructor_AssignsProperties()
    {
        var ex = new SsoAuthnRequiresInviteAcceptanceException(
            organizationDisplayName: "Acme Corp",
            userEmail: "invited@example.com");

        Assert.Equal("Acme Corp", ex.OrganizationDisplayName);
        Assert.Equal("invited@example.com", ex.UserEmail);
    }

    [Fact]
    public void Constructor_SetsDescriptiveMessage()
    {
        var ex = new SsoAuthnRequiresInviteAcceptanceException(
            organizationDisplayName: "Acme Corp",
            userEmail: "invited@example.com");

        // The message is used by server logs/error pages, not the redirect URL,
        // so we just sanity-check it includes the org name for diagnosability.
        Assert.Contains("Acme Corp", ex.Message);
    }
}
