using System.Net;
using Bit.Core.Auth.Models.Data;
using Bit.Sso.IntegrationTest.Utilities;
using Bitwarden.License.Test.Sso.IntegrationTest.Utilities;
using Microsoft.AspNetCore.Authentication;
using NSubstitute;
using Xunit;
using AuthenticationSchemes = Bit.Core.Auth.IdentityServer.AuthenticationSchemes;

namespace Bit.Sso.IntegrationTest.ExternalCallback;

/// <summary>
/// Requests to <c>/Account/ExternalCallback</c> that fail before any user lookup: a missing or failed
/// external authentication, a missing or disabled SSO configuration, an unexpected ACR value, missing
/// user ID or email claims, and a provider scheme that is not an organization ID.
/// </summary>
public class ExternalCallbackRequestValidationTests(SsoApplicationFactory factory) : IClassFixture<SsoApplicationFactory>
{
    private readonly SsoApplicationFactory _factory = factory;

    /*
    * Test to verify the /Account/ExternalCallback endpoint exists and is reachable.
    */
    [Fact]
    public async Task ExternalCallback_EndpointExists_ReturnsExpectedStatusCode()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act - Verify the endpoint is accessible (even if it fails due to missing auth)
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert - The endpoint should exist and return 500 (not 404) due to missing authentication
        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    /*
    * Test to verify calling /Account/ExternalCallback without an authentication cookie
    * results in an error as expected.
    */
    [Fact]
    public async Task ExternalCallback_WithNoAuthenticationCookie_ReturnsError()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act - Call ExternalCallback without proper authentication setup
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert - Should fail because there's no external authentication cookie
        Assert.False(response.IsSuccessStatusCode);
        // The endpoint will throw an exception when authentication fails
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    /*
    * Test to verify behavior of /Account/ExternalCallback simulating failed authentication.
    */
    [Fact]
    public async Task ExternalCallback_WithMockedAuthenticationService_FailedAuth_ReturnsError()
    {
        // Arrange
        var testData = await new SsoTestDataBuilder()
            .WithFailedAuthentication()
            .BuildAsync();

        var client = testData.Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert
        Assert.False(response.IsSuccessStatusCode);
    }

    /*
    * Test to verify /Account/ExternalCallback returns error when SSO config exists but is disabled.
    */
    [Fact]
    public async Task ExternalCallback_WithDisabledSsoConfig_ReturnsError()
    {
        // Arrange
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig(ssoConfig => ssoConfig!.Enabled = false)
            .BuildAsync();

        var client = testData.Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert - Should fail because SSO config is disabled
        var stringResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains("Organization not found or SSO configuration not enabled", stringResponse);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    /*
    * Test to verify /Account/ExternalCallback returns error when the organization has no SSO config.
    */
    [Fact]
    public async Task ExternalCallback_WithNoSsoConfig_ReturnsError()
    {
        // Arrange
        var testData = await new SsoTestDataBuilder()
            .BuildAsync();

        var client = testData.Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert - Should fail because the organization has no SSO config
        var stringResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains("Organization not found or SSO configuration not enabled", stringResponse);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    /*
    * Test to verify /Account/ExternalCallback returns error when SSO config expects an ACR value
    * but the authentication response has a missing or invalid ACR claim.
    */
    [Fact]
    public async Task ExternalCallback_WithExpectedAcrValue_AndInvalidAcr_ReturnsError()
    {
        // Arrange
        var testData = await new SsoTestDataBuilder()
        .WithSsoConfig(ssoConfig => ssoConfig!.SetData(
            new SsoConfigurationData
            {
                ExpectedReturnAcrValue = "urn:expected:acr:value"
            }))
            .BuildAsync();

        var client = testData.Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert - Should fail because ACR claim is missing or invalid
        var stringResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains("Expected authentication context class reference (acr) was not returned with the authentication response or is invalid", stringResponse);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    /*
    * Test to verify /Account/ExternalCallback returns error when the authentication response
    * does not contain any recognizable user ID claim (sub, NameIdentifier, uid, upn, eppn).
    */
    [Fact]
    public async Task ExternalCallback_WithNoUserIdClaim_ReturnsError()
    {
        // Arrange
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .OmitProviderUserId()
            .BuildAsync();

        var client = testData.Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback"); ;

        // Assert - Should fail because no user ID claim was found
        var stringResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains("Unknown userid", stringResponse);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    /*
    * Test to verify /Account/ExternalCallback returns error when no email claim is found
    * and the providerUserId cannot be used as a fallback email (doesn't contain @).
    */
    [Fact]
    public async Task ExternalCallback_WithNoEmailClaim_ReturnsError()
    {
        // Arrange
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithNullEmail()
            .BuildAsync();

        var client = testData.Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert - Should fail because no email claim was found
        var stringResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains("Cannot find email claim", stringResponse);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    /*
    * Test to verify /Account/ExternalCallback returns error when the provider scheme
    * is not a valid GUID (SSOProviderIsNotAnOrgId).
    *
    * NOTE: This test uses the substitute pattern instead of SsoTestDataBuilder because:
    * - Organization.Id is of type Guid and cannot be set to a non-GUID value
    * - The auth mock scheme must be a non-GUID string to trigger this error path
    */
    [Fact]
    public async Task ExternalCallback_WithInvalidProviderGuid_ReturnsError()
    {
        // Arrange
        var invalidScheme = "not-a-valid-guid";
        var providerUserId = Guid.NewGuid().ToString();
        var testEmail = "test@example.com";
        var testName = "Test User";

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Mock authentication service with invalid (non-GUID) scheme
                var authService = Substitute.For<IAuthenticationService>();
                authService.AuthenticateAsync(
                        Arg.Any<HttpContext>(),
                        AuthenticationSchemes.SsoExternalCookie)
                    .Returns(MockSuccessfulAuthResult.Build(invalidScheme, providerUserId, testEmail, testName));
                services.AddSingleton(authService);
            });
        }).CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert - Should fail because provider is not a valid organization GUID
        var stringResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains($"SSO provider, {invalidScheme} is not an organization id", stringResponse);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    /*
    * Test to verify /Account/ExternalCallback returns error when the organization ID
    * in the auth result does not match any organization in the database.
    * NOTE: This code path is unreachable because the SsoConfig must exist to proceed, but there is a circular dependency:
    * - SsoConfig cannot exist without a valid Organization but the test is testing that an Organization cannot be found.
    */
    [Fact(Skip = "This code path is unreachable because the SsoConfig must exist to proceed. But the SsoConfig cannot exist without a valid Organization.")]
    public async Task ExternalCallback_WithNonExistentOrganization_ReturnsError()
    {
        // Arrange
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithNonExistentOrganizationInAuth()
            .BuildAsync();

        var client = testData.Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert - Should fail because organization cannot be found by the ID in auth result
        var stringResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains("Could not find organization", stringResponse);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }
}
