using System.Net;
using Bit.Sso.IntegrationTest.Utilities;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Bit.Sso.IntegrationTest.ExternalCallback;

/// <summary>
/// For a user who already has an SSO link to the organization, <c>/Account/ExternalCallback</c> accepts the
/// external login with no new link and no new account, but only while the user still has an organization user
/// row. A native client gets a page with status 200 that redirects, not a 302.
/// </summary>
public class ExternalCallbackAlreadySsoLinkedUserTests
{
    /*
    * Test to verify /Account/ExternalCallback returns error when user is found via SSO
    * but has no organization user record.
    */
    [Fact]
    public async Task ExternalCallback_WithSsoUser_AndNoOrgUser_ReturnsError()
    {
        // Arrange
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithUser()
            .WithSsoUser()
            .BuildAsync();

        var client = testData.Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert - Should fail because org user cannot be found
        var stringResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains("Could not find organization user", stringResponse);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    /*
    * SUCCESS PATH: Test to verify /Account/ExternalCallback succeeds when an existing
    * SSO-linked user logs in (user exists in SsoUser table).
    */
    [Fact]
    public async Task ExternalCallback_WithExistingSsoUser_ReturnsSuccess()
    {
        // Arrange - User with SSO link already exists
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithUser()
            .WithOrganizationUser()
            .WithSsoUser()
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

    /*
    * SUCCESS PATH: Test to verify /Account/ExternalCallback returns a View with 200 status
    * when the client is a native application (uses custom URI scheme like "bitwarden://callback").
    * Native clients get a different response for better UX - a 200 with redirect view instead of 302.
    * See AccountController lines 371-378.
    */
    [Fact]
    public async Task ExternalCallback_WithNativeClient_ReturnsViewWith200Status()
    {
        // Arrange - Existing SSO user with native client context
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithUser()
            .WithOrganizationUser()
            .WithSsoUser()
            .AsNativeClient()
            .BuildAsync();

        var client = testData.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert - Native clients get 200 status with a redirect view instead of 302
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // The Location header should be empty for native clients (set in controller)
        // and the response should contain the redirect view
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(content); // View content should be present
    }
}
