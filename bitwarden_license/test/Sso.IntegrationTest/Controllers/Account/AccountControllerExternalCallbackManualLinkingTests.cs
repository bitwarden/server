using System.Net;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Sso.IntegrationTest.Utilities;
using Bit.Test.Common.AutoFixture.Attributes;
using Bitwarden.License.Test.Sso.IntegrationTest.Utilities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;
using AuthenticationSchemes = Bit.Core.Auth.IdentityServer.AuthenticationSchemes;

namespace Bit.Sso.IntegrationTest.Controllers.Account;

/// <summary>
/// <c>/Account/ExternalCallback</c> when the request carries a user identifier to link an existing account
/// to SSO by hand.
/// </summary>
public class AccountControllerExternalCallbackManualLinkingTests(SsoApplicationFactory factory) : IClassFixture<SsoApplicationFactory>
{
    private readonly SsoApplicationFactory _factory = factory;

    /*
    * Test to verify /Account/ExternalCallback returns error when email cannot be found
    * during new user provisioning (Scenario 2) after bypassing the first email check
    * via manual linking path (userIdentifier is set).
    */
    [Fact]
    public async Task ExternalCallback_WithUserIdentifier_AndNoEmail_ReturnsError()
    {
        // Arrange
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithUserIdentifier("")
            .WithNullEmail()
            .BuildAsync();

        var client = testData.Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert - Should fail because email cannot be found during new user provisioning
        var stringResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains("Cannot find email claim", stringResponse);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    /*
    * Test to verify /Account/ExternalCallback returns error when userIdentifier
    * is malformed (doesn't contain comma separator for userId,token format).
    * There is only a single test case here but in the future we may need to expand the
    * tests to cover other invalid formats.
    */
    [Theory]
    [BitAutoData("No-Comas-Identifier")]
    public async Task ExternalCallback_WithInvalidUserIdentifierFormat_ReturnsError(
        string UserIdentifier
    )
    {
        // Arrange
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithUserIdentifier(UserIdentifier)
            .BuildAsync();

        var client = testData.Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert - Should fail because userIdentifier format is invalid
        var stringResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid user identifier", stringResponse);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    /*
    * Test to verify /Account/ExternalCallback returns error when userIdentifier
    * contains valid userId but invalid/mismatched token.
    *
    * NOTE: This test uses the substitute pattern instead of SsoTestDataBuilder because:
    * - The userIdentifier in the auth result must contain a userId that matches a user in the system
    * - User.SetNewId() always overwrites the Id (unlike Organization.SetNewId() which has a guard)
    * - This means we cannot pre-set a User.Id before database insertion
    * - The auth mock must be configured BEFORE accessing factory.Services (required by SubstituteService)
    * - Therefore, we cannot coordinate the userId between the auth mock and the seeded user
    * - Using substitutes allows us to control the exact userId and mock UserManager.VerifyUserTokenAsync
    */
    [Fact]
    public async Task ExternalCallback_WithUserIdentifier_AndInvalidToken_ReturnsError()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var providerUserId = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid();
        var testEmail = "test_user@integration.test";
        var testName = "Test User";
        // Valid format but token won't verify
        var userIdentifier = $"{userId},invalid-token";

        var claimedUser = new User
        {
            Id = userId,
            Email = testEmail,
            Name = testName
        };

        var organization = new Organization
        {
            Id = organizationId,
            Name = "Test Organization",
            Enabled = true,
            UseSso = true
        };

        var ssoConfig = new SsoConfig
        {
            OrganizationId = organizationId,
            Enabled = true
        };
        ssoConfig.SetData(new SsoConfigurationData());

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Mock organization repository
                var orgRepo = Substitute.For<IOrganizationRepository>();
                orgRepo.GetByIdAsync(organizationId).Returns(organization);
                orgRepo.GetByIdentifierAsync(organizationId.ToString()).Returns(organization);
                services.AddSingleton(orgRepo);

                // Mock SSO config repository
                var ssoConfigRepo = Substitute.For<ISsoConfigRepository>();
                ssoConfigRepo.GetByOrganizationIdAsync(organizationId).Returns(ssoConfig);
                services.AddSingleton(ssoConfigRepo);

                // Mock user repository - no existing user via SSO
                var userRepo = Substitute.For<IUserRepository>();
                userRepo.GetBySsoUserAsync(providerUserId, organizationId).Returns((User?)null);
                services.AddSingleton(userRepo);

                // Mock user service - returns user for manual linking lookup
                var userService = Substitute.For<IUserService>();
                userService.GetUserByIdAsync(userId.ToString()).Returns(claimedUser);
                services.AddSingleton(userService);

                // Mock UserManager to return false for token verification
                var userManager = Substitute.For<UserManager<User>>(
                    Substitute.For<IUserStore<User>>(), null, null, null, null, null, null, null, null);
                userManager.VerifyUserTokenAsync(
                    claimedUser,
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<string>())
                    .Returns(false);
                services.AddSingleton(userManager);

                // Mock authentication service with userIdentifier that has valid format but invalid token
                var authService = Substitute.For<IAuthenticationService>();
                authService.AuthenticateAsync(
                        Arg.Any<HttpContext>(),
                        AuthenticationSchemes.SsoExternalCookie)
                    .Returns(MockSuccessfulAuthResult.Build(organizationId, providerUserId, testEmail, testName, null, userIdentifier));
                services.AddSingleton(authService);
            });
        }).CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert - Should fail because token verification failed
        var stringResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains("Supplied userId and token did not match", stringResponse);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }
}
