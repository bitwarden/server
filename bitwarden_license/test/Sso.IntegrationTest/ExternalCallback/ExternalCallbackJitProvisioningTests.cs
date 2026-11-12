using System.Net;
using Bit.Sso.IntegrationTest.Utilities;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Bit.Sso.IntegrationTest.ExternalCallback;

/// <summary>
/// A new user with no Bitwarden account and no organization user row signs in through
/// <c>/Account/ExternalCallback</c>. SSO creates both just in time.
/// </summary>
public class ExternalCallbackJitProvisioningTests
{
    /*
    * SUCCESS PATH: Test to verify /Account/ExternalCallback succeeds when JIT provisioning
    * a new user (user doesn't exist, gets created automatically).
    */
    [Fact]
    public async Task ExternalCallback_WithJitProvisioning_ReturnsSuccess()
    {
        // Arrange - No user, no org user - JIT provisioning will create both
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .BuildAsync();

        var client = testData.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false // Prevent auto-redirects to capture initial response
        });

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert - Should succeed and redirect
        Assert.True(
            response.StatusCode == HttpStatusCode.Redirect,
            $"Expected success/redirect but got {response.StatusCode}");

        Assert.NotNull(response.Headers.Location);
    }

    // Note: "User should be found ln 304" appears to be unreachable defensive code.
    // CreateUserAndOrgUserConditionallyAsync always returns a non-null user or throws an exception,
    // so possibleSsoLinkedUser cannot be null when the feature flag check executes.
}
