using System.Net;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Sso.IntegrationTest.Utilities;
using Bit.Test.Common.AutoFixture.Attributes;
using Bitwarden.License.Test.Sso.IntegrationTest.Utilities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using NSubstitute;
using Xunit;
using AuthenticationSchemes = Bit.Core.AuthenticationSchemes;

namespace Bit.Sso.IntegrationTest.Controllers;

public class AccountControllerTests(SsoApplicationFactory factory) : IClassFixture<SsoApplicationFactory>
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

    [Fact]
    public async Task ExternalCallback_FindUserFromExternalProviderAsync_OrganizationOrSsoConfigNotFound_ReturnsError()
    {
        // Arrange
        var testData = await new SsoTestDataBuilder()
            .BuildAsync();

        var client = testData.Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert - Should fail because user has invalid status
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
    * Test to verify /Account/ExternalCallback returns error when an existing user
    * uses Key Connector but has no org user record (was removed from organization).
    */
    [Fact]
    public async Task ExternalCallback_WithExistingKeyConnectorUser_AndNoOrgUser_ReturnsError()
    {
        // Arrange
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithUser(user =>
            {
                user.UsesKeyConnector = true;
            })
            .BuildAsync();

        var client = testData.Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert - Should fail because user uses Key Connector but has no org user record
        var stringResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains("You were removed from the organization managing single sign-on for your account", stringResponse);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    /*
    * Test to verify /Account/ExternalCallback returns error when an existing user
    * uses Key Connector and has an org user record in the invited status.
    */
    [Fact]
    public async Task ExternalCallback_WithExistingKeyConnectorUser_AndInvitedOrgUser_ReturnsError()
    {
        // Arrange
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig(ssoConfig => { })
            .WithUser(user =>
            {
                user.UsesKeyConnector = true;
            })
            .WithOrganizationUser(orgUser =>
            {
                orgUser.Status = OrganizationUserStatusType.Invited;
            })
            .BuildAsync();

        var client = testData.Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert - Should fail because user uses Key Connector but the Org user is in the invited status
        var stringResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains("You were removed from the organization managing single sign-on for your account", stringResponse);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    /*
    * Test to verify /Account/ExternalCallback returns error when an existing user
    * uses Key Connector and encounters a Staged OrganizationUser row (created by
    * directory import) in the SSO-target org. Key Connector users have no master
    * password and cannot complete the "sign in with master password and accept the
    * invite" flow that the Staged-promotion path would email them into. The Key
    * Connector guard must reject them cleanly here — without this branch the flow
    * would fall through to PromoteStagedOrgUserAndSendInviteAsync, consume a seat,
    * mail an unactionable invite, and dead-end on UserAlreadyExistsKeyConnector on
    * the next SSO attempt.
    */
    [Fact]
    public async Task ExternalCallback_WithExistingKeyConnectorUser_AndStagedOrgUser_ReturnsError()
    {
        // Arrange — existing KC user (UsesKeyConnector=true) AND a Staged
        // OrganizationUser row (UserId=null) matching the SSO-claimed email.
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithUser(user =>
            {
                user.UsesKeyConnector = true;
            })
            .WithStagedOrganizationUser()
            .WithPM34423StagedStatusFlag()
            .WithMockedSendOrganizationInvitesCommand()
            .BuildAsync();

        var client = testData.Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert — Key Connector guard rejects the login before the Staged branch runs.
        var stringResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains("You were removed from the organization managing single sign-on for your account", stringResponse);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        // Assert — no invite email was issued (guard must fire before the promotion branch).
        var inviteCommand = testData.Factory.Services.GetRequiredService<ISendOrganizationInvitesCommand>();
        await inviteCommand.DidNotReceive().SendInvitesAsync(Arg.Any<SendInvitesRequest>());

        // Assert — Staged row was not mutated (still Staged, UserId still null).
        var orgUserRepo = testData.Factory.Services.GetRequiredService<IOrganizationUserRepository>();
        var refreshedOrgUser = await orgUserRepo.GetByIdAsync(testData.OrganizationUser!.Id);
        Assert.NotNull(refreshedOrgUser);
        Assert.Equal(OrganizationUserStatusType.Staged, refreshedOrgUser.Status);
        Assert.Null(refreshedOrgUser.UserId);
    }

    /*
    * Test to verify /Account/ExternalCallback redirects an existing user (not using
    * Key Connector) with no OrganizationUser row back to the web vault's /login with
    * the OrgMembershipRequired errorCode + context. Covers both the user who clicked
    * an open invite link (client has the invite stashed) and the user with no pending
    * invite at all — the server cannot tell them apart, so this single redirect
    * contract serves both.
    */
    [Fact]
    public async Task ExternalCallback_WithExistingUser_AndNoOrgUser_RedirectsToWebVaultLogin()
    {
        // Arrange
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithUser()
            .BuildAsync();

        var client = testData.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false // Capture the redirect rather than following to the (non-existent) web vault.
        });

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert — 302 to the web vault /login with the expected query-string context.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var location = response.Headers.Location!.ToString();
        Assert.Contains("/login?", location);
        Assert.Contains("error=ssoOrgMembershipRequired", location);

        // The redirect carries the seeded user's email, the org id (stable match key), and
        // the org display name (URL-encoded, for the toast) so the web client can pre-fill
        // /login and dispatch its match/no-match split.
        Assert.Contains($"email={Uri.EscapeDataString(testData.User!.Email)}", location);
        Assert.Contains($"organizationId={testData.Organization!.Id}", location);
        Assert.Contains(
            $"organizationName={Uri.EscapeDataString(testData.Organization!.DisplayName())}",
            location);
    }

    /*
    * Test to verify /Account/ExternalCallback redirects an existing user with an
    * Invited org user back to the web vault's /login with context so the client can
    * surface an actionable toast. Previously this rendered a server error page;
    * the security gate (refusing SSO completion for invited users) is unchanged.
    */
    [Fact]
    public async Task ExternalCallback_WithExistingUser_AndInvitedOrgUserStatus_RedirectsToWebVaultLogin()
    {
        // Arrange
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithUser()
            .WithOrganizationUser(orgUser =>
            {
                orgUser.Status = OrganizationUserStatusType.Invited;
            })
            .BuildAsync();

        var client = testData.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false // Capture the redirect rather than following to the (non-existent) web vault.
        });

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert — 302 to the web vault /login with the expected query-string context.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var location = response.Headers.Location!.ToString();
        Assert.Contains("/login?", location);
        Assert.Contains("error=ssoOrgInviteAcceptanceRequired", location);

        // The redirect carries the seeded user's email, the org id (stable match key), and
        // the org display name (URL-encoded, for the toast) so the web client can pre-fill
        // /login and render a contextual toast.
        Assert.Contains($"email={Uri.EscapeDataString(testData.User!.Email)}", location);
        Assert.Contains($"organizationId={testData.Organization!.Id}", location);
        Assert.Contains(
            $"organizationName={Uri.EscapeDataString(testData.Organization!.DisplayName())}",
            location);
    }

    /*
    * Test to verify /Account/ExternalCallback redirects directly to the client's
    * /sso-login-failed terminal page when the target organization is at its seat cap
    * and cannot autoscale because it's a self-hosted instance. Replaces the pre-fix
    * behavior of surfacing a raw 500 — the user cannot resolve without admin
    * intervention, so we route to the terminal page (no /login layover) instead.
    */
    [Fact]
    public async Task ExternalCallback_WithNoAvailableSeats_OnSelfHosted_RedirectsToSsoLoginFailed()
    {
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithOrganization(org =>
            {
                org.Seats = 5; // Organization has seat limit
            })
            .AsSelfHosted()
            .BuildAsync();

        var client = testData.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        AssertNoSeatsAvailableRedirect(response);
    }

    /*
    * Test to verify /Account/ExternalCallback redirects directly to the client's
    * /sso-login-failed terminal page when the organization is at its seat cap and
    * cloud autoscale cannot grow past MaxAutoscaleSeats. Same contract as the
    * self-hosted variant.
    */
    [Fact]
    public async Task ExternalCallback_WithNoAvailableSeats_AndAutoAddSeatsFails_RedirectsToSsoLoginFailed()
    {
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithOrganization(org =>
            {
                org.Seats = 5;
                org.MaxAutoscaleSeats = 5;
            })
            .BuildAsync();

        var client = testData.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        AssertNoSeatsAvailableRedirect(response);
    }

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
    * Test to verify /Account/ExternalCallback returns error when org user has an unknown/invalid status.
    * This tests defensive code that handles future enum values or data corruption scenarios.
    * We simulate this by casting an invalid integer to OrganizationUserStatusType.
    */
    [Fact]
    public async Task ExternalCallback_WithUnknownOrgUserStatus_ReturnsError()
    {
        // Arrange
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithUser()
            .WithOrganizationUser(orgUser =>
            {
                orgUser.Status = (OrganizationUserStatusType)99; // Invalid enum value - simulates future status or data corruption
            })
            .BuildAsync();

        var client = testData.Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert - Should fail because org user status is unknown/invalid
        var stringResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains("is in an unknown state", stringResponse);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    // Note: "User should be found ln 304" appears to be unreachable defensive code.
    // CreateUserAndOrgUserConditionallyAsync always returns a non-null user or throws an exception,
    // so possibleSsoLinkedUser cannot be null when the feature flag check executes.

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
                        AuthenticationSchemes.BitwardenExternalCookieAuthenticationScheme)
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

    /*
    * Test to verify /Account/ExternalCallback returns error for revoked org user.
    */
    [Fact]
    public async Task ExternalCallback_WithRevokedOrgUser_ReturnsError()
    {
        // Arrange
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithUser()
            .WithOrganizationUser(orgUser =>
            {
                orgUser.Status = OrganizationUserStatusType.Revoked;
            })
            .BuildAsync();

        var client = testData.Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert - Should fail because user state is invalid
        var stringResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains(
            $"Your access to organization {testData.Organization?.DisplayName()} has been revoked. Please contact your administrator for assistance.",
            stringResponse);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

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
    * Test to verify /Account/ExternalCallback returns error when the provider scheme
    * is not a valid GUID (SSOProviderIsNotAnOrgId).
    *
    * NOTE: This test uses the substitute pattern instead of SsoTestDataBuilder because:
    * - Organization.Id is of type Guid and cannot be set to a non-GUID value
    * - The auth mock scheme must be a non-GUID string to trigger this error path
    * - This cannot be tested since ln 438 in AccountController.FindUserFromExternalProviderAsync throws a different exception
    *   before reaching the organization lookup exception.
    */
    [Fact(Skip = "This test cannot be executed because the organization ID must be a GUID. See note in test summary.")]
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
                        AuthenticationSchemes.BitwardenExternalCookieAuthenticationScheme)
                    .Returns(MockSuccessfulAuthResult.Build(invalidScheme, providerUserId, testEmail, testName));
                services.AddSingleton(authService);
            });
        }).CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert - Should fail because provider is not a valid organization GUID
        var stringResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains("Organization not found from identifier.", stringResponse);
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

    /*
    * SUCCESS PATH: Test to verify /Account/ExternalCallback JIT-provisions a brand-new
    * user when the org has an explicit seat cap with headroom. Locks in the "guard fires,
    * seats available" branch of the pre-user-creation seat check — currently exercised
    * only implicitly by the unlimited-seats JIT test.
    */
    [Fact]
    public async Task ExternalCallback_WithSeatCapAndHeadroom_JitSucceeds()
    {
        // Arrange — seat cap of 1, 0 occupied (builder skips auto-fill when Seats == 1),
        // so the seat check should observe 1 available seat and proceed without autoscale.
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithOrganization(org =>
            {
                org.Seats = 1;
            })
            .BuildAsync();

        var client = testData.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert — JIT proceeded and produced the standard SSO-success redirect.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
    }

    /*
    * SUCCESS PATH: Test to verify /Account/ExternalCallback promotes a Staged OrganizationUser
    * row to Invited when a brand-new (no BW User yet) user JIT-provisions against it. Staged
    * rows come from directory-import (UserId=null, Email set); the JIT finish-accept path
    * only handles Invited, so we must promote before letting the standard invite lifecycle
    * pick up. Also asserts RevisionDate is bumped so watermark-driven consumers see the change.
    */
    [Fact]
    public async Task ExternalCallback_WithJitProvisioning_AgainstStagedOrgUser_PromotesToInvited()
    {
        // Arrange — no BW User; a Staged OrganizationUser exists matching the SSO-claimed email.
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithStagedOrganizationUser()
            .WithPM34423StagedStatusFlag()
            .BuildAsync();

        // Capture the seeded row's RevisionDate so we can prove it was bumped by the promotion.
        var stagedRowInitialRevisionDate = testData.OrganizationUser!.RevisionDate;

        var client = testData.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act — JIT-provision the user via SSO.
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert — SSO callback succeeded end-to-end.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        // Assert — the Staged row has been promoted to Invited, back-linked to the
        // newly-provisioned BW user, and RevisionDate has advanced past the seeded value.
        var orgUserRepo = testData.Factory.Services.GetRequiredService<IOrganizationUserRepository>();
        var refreshedOrgUser = await orgUserRepo.GetByIdAsync(testData.OrganizationUser!.Id);
        Assert.NotNull(refreshedOrgUser);
        Assert.Equal(OrganizationUserStatusType.Invited, refreshedOrgUser.Status);
        Assert.NotNull(refreshedOrgUser.UserId);
        Assert.True(refreshedOrgUser.RevisionDate > stagedRowInitialRevisionDate,
            $"RevisionDate should be bumped after promotion. Before: {stagedRowInitialRevisionDate:O}, After: {refreshedOrgUser.RevisionDate:O}.");
    }

    /*
    * SUCCESS PATH: Test to verify /Account/ExternalCallback JIT-provisions successfully
    * when the org is at its seat cap but cloud autoscale succeeds. Locks in the
    * "at cap, cloud, autoscale succeeds" branch of the pre-user-creation seat check.
    * IOrganizationService.AutoAddSeatsAsync is substituted to bypass the real billing
    * gateway (which requires a Stripe-linked org).
    */
    [Fact]
    public async Task ExternalCallback_WithSeatCapAtLimitAndAutoscaleSucceeds_JitSucceeds()
    {
        // Arrange — Seats = 5 causes the builder to auto-fill 5 Confirmed OrgUsers,
        // putting the org at cap. Mocked AutoAddSeatsAsync succeeds silently.
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithOrganization(org =>
            {
                org.Seats = 5;
            })
            .WithAutoscaleSucceeds()
            .BuildAsync();

        var client = testData.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert — SSO callback succeeded and autoscale was actually invoked (proves we
        // went through the "at cap → autoscale → success" branch, not the "seats available"
        // shortcut).
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var orgService = testData.Factory.Services.GetRequiredService<IOrganizationService>();
        await orgService.Received(1).AutoAddSeatsAsync(Arg.Any<Organization>(), 1);
    }

    /*
    * FAILURE PATH: Test to verify /Account/ExternalCallback rolls back a mid-flight
    * seat-count mutation when AutoAddSeatsAsync partially succeeds (bumps Seats) and
    * then throws. The catch-path must call AdjustSeatsAsync with the negated delta
    * before surfacing the NoSeatsAvailable redirect.
    */
    [Fact]
    public async Task ExternalCallback_WithAutoscalePartialFailure_RollsBackSeatsAndRedirects()
    {
        // Arrange — Seats = 5, at cap. Mocked AutoAddSeatsAsync bumps Seats to 6 then throws.
        // The catch block should observe org.Seats (6) != initialSeatCount (5) and call
        // AdjustSeatsAsync(orgId, -1) to roll back before throwing SsoAuthnNoSeatsAvailableException.
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithOrganization(org =>
            {
                org.Seats = 5;
            })
            .WithAutoscalePartialFailure()
            .BuildAsync();

        var client = testData.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        AssertNoSeatsAvailableRedirect(response);

        // Assert — rollback ran with the exact delta before the redirect fired.
        var orgService = testData.Factory.Services.GetRequiredService<IOrganizationService>();
        await orgService.Received(1).AdjustSeatsAsync(testData.Organization!.Id, -1);
    }

    /*
    * FAILURE PATH: Test to verify /Account/ExternalCallback rejects a Staged-against-JIT
    * SSO login when the org is at its seat cap on self-hosted. Staged rows do not count
    * against occupied seats; promoting one to Invited consumes a seat and therefore must
    * run the same "seats available or autoscale" check as a fresh JIT provisioning.
    */
    [Fact]
    public async Task ExternalCallback_WithJitProvisioning_AgainstStagedOrgUser_OnSelfHostedAtSeatCap_RedirectsToSsoLoginFailed()
    {
        // Arrange — Seats = 5 causes the builder to auto-fill 5 Confirmed OrgUsers (occupying
        // all seats). The Staged row is not counted against seats. Self-hosted disables autoscale.
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithStagedOrganizationUser()
            .WithOrganization(org =>
            {
                org.Seats = 5;
            })
            .AsSelfHosted()
            .WithPM34423StagedStatusFlag()
            .BuildAsync();

        var client = testData.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Promoting the Staged row would exceed the seat cap and self-hosted cannot autoscale.
        AssertNoSeatsAvailableRedirect(response);
    }

    /*
    * FAILURE PATH: Test to verify /Account/ExternalCallback rejects a Staged-against-JIT
    * SSO login when the org is at its seat cap on cloud and autoscale cannot grow the cap.
    * Mirrors ExternalCallback_WithNoAvailableSeats_AndAutoAddSeatsFails_RedirectsToSsoLoginFailed
    * but for the Staged-promotion path rather than fresh JIT.
    */
    [Fact]
    public async Task ExternalCallback_WithJitProvisioning_AgainstStagedOrgUser_OnCloudAutoscaleFails_RedirectsToSsoLoginFailed()
    {
        // Arrange — Seats == MaxAutoscaleSeats forces AutoAddSeatsAsync to fail. Builder
        // auto-fills 5 Confirmed OrgUsers, so the org is at cap when SSO arrives.
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithStagedOrganizationUser()
            .WithOrganization(org =>
            {
                org.Seats = 5;
                org.MaxAutoscaleSeats = 5;
            })
            .WithPM34423StagedStatusFlag()
            .BuildAsync();

        var client = testData.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Promoting the Staged row would exceed the seat cap and autoscale cannot grow past
        // MaxAutoscaleSeats.
        AssertNoSeatsAvailableRedirect(response);
    }

    /*
    * SUCCESS PATH: Test to verify /Account/ExternalCallback JIT-provisions a user against a
    * Staged OrganizationUser when the org is at seat cap but cloud autoscale can grow. Locks
    * in the "Staged promotion, at cap, autoscale succeeds" branch — the three sibling Staged
    * failure tests only exercise the throwing paths. IOrganizationService.AutoAddSeatsAsync
    * is substituted to bypass the real billing gateway.
    */
    [Fact]
    public async Task ExternalCallback_WithJitProvisioning_AgainstStagedOrgUser_OnCloudAutoscaleSucceeds_PromotesToInvited()
    {
        // Arrange — Seats = 5 fills 5 Confirmed OrgUsers (at cap). Staged row does not count
        // against the cap; promoting it would push us over, but the mocked autoscale grows.
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithStagedOrganizationUser()
            .WithOrganization(org =>
            {
                org.Seats = 5;
            })
            .WithAutoscaleSucceeds()
            .WithPM34423StagedStatusFlag()
            .BuildAsync();

        var client = testData.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert — SSO callback succeeded end-to-end.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        // Assert — autoscale was actually invoked (proves we went through the "at cap →
        // autoscale → promote" branch, not the "seats available" shortcut).
        var orgService = testData.Factory.Services.GetRequiredService<IOrganizationService>();
        await orgService.Received(1).AutoAddSeatsAsync(Arg.Any<Organization>(), 1);

        // Assert — Staged row promoted to Invited and back-linked to the newly-provisioned user.
        var orgUserRepo = testData.Factory.Services.GetRequiredService<IOrganizationUserRepository>();
        var refreshedOrgUser = await orgUserRepo.GetByIdAsync(testData.OrganizationUser!.Id);
        Assert.NotNull(refreshedOrgUser);
        Assert.Equal(OrganizationUserStatusType.Invited, refreshedOrgUser.Status);
        Assert.NotNull(refreshedOrgUser.UserId);
    }

    /*
    * FALL-THROUGH PATH: Test to verify /Account/ExternalCallback does NOT promote a Staged
    * OrganizationUser when the PM34423StagedStatus feature flag is off, even at seat cap.
    * With the flag off, the Staged branch is skipped, no seat check runs for the promotion,
    * and the request falls through to PreventOrgUserLoginIfStatusInvalidAsync which trips
    * on the still-Staged status (the pre-existing pre-PM-42167 behavior).
    *
    * Locks in the flag-gated boundary: if the flag check is ever forgotten from the promotion
    * condition, this test flips because the row gets promoted. If it is ever forgotten from
    * an earlier gate that fires a seat check, this test flips because the error message
    * becomes "No seats available" instead of the unknown-status message.
    */
    [Fact]
    public async Task ExternalCallback_WithJitProvisioning_AgainstStagedOrgUser_WithFeatureFlagOff_AtSeatCap_FallsThroughToUnknownStatus()
    {
        // Arrange — Seats = 5 + MaxAutoscaleSeats = 5 so any accidental seat check would
        // trip on "No seats available." Feature flag is explicitly off so the promotion
        // branch is skipped.
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithStagedOrganizationUser()
            .WithOrganization(org =>
            {
                org.Seats = 5;
                org.MaxAutoscaleSeats = 5;
            })
            .WithPM34423StagedStatusFlag(enabled: false)
            .BuildAsync();

        var client = testData.Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert — 500 with the unknown-status message (proves fall-through to the status
        // filter throw, NOT the seat-check path — which would produce a 302 redirect).
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var stringResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains("is in an unknown state", stringResponse);

        // Assert — Staged row status is unchanged (promotion did not run).
        var orgUserRepo = testData.Factory.Services.GetRequiredService<IOrganizationUserRepository>();
        var refreshedOrgUser = await orgUserRepo.GetByIdAsync(testData.OrganizationUser!.Id);
        Assert.NotNull(refreshedOrgUser);
        Assert.Equal(OrganizationUserStatusType.Staged, refreshedOrgUser.Status);
    }

    /*
    * REGRESSION GUARD: Two-phase test verifying that when the seat check throws while
    * JIT-provisioning a BW User against a Staged OrganizationUser row, no BW User row
    * is persisted (Phase 1), and that after an admin adds seats the retry proceeds
    * cleanly through the standard JIT flow (Phase 2). Locks in the pre-user-creation
    * seat check ordering — if that ordering ever regresses, Phase 1 flips because a BW
    * User row appears despite the 500.
    */
    [Fact]
    public async Task ExternalCallback_JitAgainstStagedOrgUser_WhenSeatCheckThrows_RejectsCleanlyAndRecoversAfterCapIncrease()
    {
        // Phase 1 arrange — cloud org at cap, autoscale maxed (Seats == MaxAutoscaleSeats).
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithStagedOrganizationUser()
            .WithOrganization(org =>
            {
                org.Seats = 5;
                org.MaxAutoscaleSeats = 5;
            })
            .WithPM34423StagedStatusFlag()
            .WithMockedSendOrganizationInvitesCommand()
            .BuildAsync();

        var client = testData.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var ssoClaimedEmail = testData.OrganizationUser!.Email!;

        // Phase 1 act — first SSO attempt should be rejected.
        var response1 = await client.GetAsync("/Account/ExternalCallback");

        AssertNoSeatsAvailableRedirect(response1);

        // Phase 1 assert — no BW User row was persisted. This is the fix's contract:
        // the seat check runs before RegisterSSOAutoProvisionedUserAsync so a rejection
        // does not leave account state behind for a login that never completed.
        var userRepo = testData.Factory.Services.GetRequiredService<IUserRepository>();
        var userAfterPhase1 = await userRepo.GetByEmailAsync(ssoClaimedEmail);
        Assert.Null(userAfterPhase1);

        // Phase 1 assert — Staged row unchanged and no SsoUser link exists.
        var orgUserRepo = testData.Factory.Services.GetRequiredService<IOrganizationUserRepository>();
        var stagedRowAfterPhase1 = await orgUserRepo.GetByIdAsync(testData.OrganizationUser!.Id);
        Assert.Equal(OrganizationUserStatusType.Staged, stagedRowAfterPhase1!.Status);
        Assert.Null(stagedRowAfterPhase1.UserId);

        // Phase 2 arrange — admin bumps seats and autoscale cap so the retry has headroom.
        var orgRepo = testData.Factory.Services.GetRequiredService<IOrganizationRepository>();
        var org = await orgRepo.GetByIdAsync(testData.Organization!.Id);
        org!.Seats = 6;
        org.MaxAutoscaleSeats = 6;
        await orgRepo.ReplaceAsync(org);

        // Phase 2 act — user retries SSO.
        var response2 = await client.GetAsync("/Account/ExternalCallback");

        // Phase 2 assert — standard successful SSO redirect (not a /login redirect with
        // an error code; the retry runs through fresh-JIT and passes the status filter).
        Assert.Equal(HttpStatusCode.Redirect, response2.StatusCode);
        Assert.NotNull(response2.Headers.Location);

        // Phase 2 assert — BW User row now exists, Staged row promoted to Invited and
        // back-linked to it, SsoUser link established.
        var userAfterPhase2 = await userRepo.GetByEmailAsync(ssoClaimedEmail);
        Assert.NotNull(userAfterPhase2);

        var stagedRowAfterPhase2 = await orgUserRepo.GetByIdAsync(testData.OrganizationUser!.Id);
        Assert.Equal(OrganizationUserStatusType.Invited, stagedRowAfterPhase2!.Status);
        Assert.Equal(userAfterPhase2!.Id, stagedRowAfterPhase2.UserId);

        var ssoUserRepo = testData.Factory.Services.GetRequiredService<ISsoUserRepository>();
        var ssoLinkAfterPhase2 = await ssoUserRepo.GetByUserIdOrganizationIdAsync(testData.Organization.Id, userAfterPhase2.Id);
        Assert.NotNull(ssoLinkAfterPhase2);
    }

    /*
    * SUCCESS PATH: Test to verify /Account/ExternalCallback promotes a Staged OrganizationUser
    * row to Invited, sends a real invite email via ISendOrganizationInvitesCommand, and redirects
    * to /login with the StagedOrgUserInviteAcceptanceRequired error when an existing BW user
    * (matched by email) attempts SSO against a Staged placeholder. Distinct from the standard
    * InviteAcceptanceRequired code so the client can tell the user to check their email for
    * the freshly-sent invite rather than referencing an invite they were expected to have.
    */
    [Fact]
    public async Task ExternalCallback_WithExistingUser_AndStagedOrgUser_PromotesInvitesAndRedirects()
    {
        // Arrange — existing BW user AND a Staged OrganizationUser row sharing the SSO-claimed email.
        // The Staged row has UserId=null (matches the directory-import shape).
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithUser()
            .WithStagedOrganizationUser()
            .WithPM34423StagedStatusFlag()
            .WithMockedSendOrganizationInvitesCommand()
            .BuildAsync();

        var stagedRowInitialRevisionDate = testData.OrganizationUser!.RevisionDate;

        var client = testData.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert — 302 to /login with the StagedOrgUserInviteAcceptanceRequired error code and context.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var location = response.Headers.Location!.ToString();
        Assert.Contains("/login?", location);
        Assert.Contains("error=ssoStagedOrgUserInviteAcceptanceRequired", location);
        Assert.Contains($"email={Uri.EscapeDataString(testData.User!.Email)}", location);
        Assert.Contains($"organizationId={testData.Organization!.Id}", location);

        // Assert — Staged row promoted to Invited, UserId left null (matches standard admin-invite
        // shape; UserId is set at accept time by the accept endpoint), RevisionDate bumped.
        var orgUserRepo = testData.Factory.Services.GetRequiredService<IOrganizationUserRepository>();
        var refreshedOrgUser = await orgUserRepo.GetByIdAsync(testData.OrganizationUser!.Id);
        Assert.NotNull(refreshedOrgUser);
        Assert.Equal(OrganizationUserStatusType.Invited, refreshedOrgUser.Status);
        Assert.Null(refreshedOrgUser.UserId);
        Assert.True(refreshedOrgUser.RevisionDate > stagedRowInitialRevisionDate,
            $"RevisionDate should be bumped after promotion. Before: {stagedRowInitialRevisionDate:O}, After: {refreshedOrgUser.RevisionDate:O}.");

        // Assert — invite email was issued for the promoted OrgUser, with invitingUserId=null
        // (matches the SCIM/automated pattern documented on SendInvitesRequest).
        var inviteCommand = testData.Factory.Services.GetRequiredService<ISendOrganizationInvitesCommand>();
        await inviteCommand.Received(1).SendInvitesAsync(Arg.Is<SendInvitesRequest>(r =>
            r.Users.Length == 1 &&
            r.Users[0].Id == testData.OrganizationUser!.Id &&
            r.Organization.Id == testData.Organization!.Id &&
            r.InvitingUserId == null));
    }

    /*
    * FAILURE PATH: Test to verify /Account/ExternalCallback rejects an existing-user Staged
    * promotion when the org is at its seat cap on self-hosted. Also asserts we do NOT send
    * an invite email (seat check must run before the email step) and do NOT mutate the row.
    */
    [Fact]
    public async Task ExternalCallback_WithExistingUser_AndStagedOrgUser_OnSelfHostedAtSeatCap_RedirectsAndDoesNotInvite()
    {
        // Arrange — Seats = 5, builder fills 5 Confirmed rows to reach cap. Staged row does not
        // count against the cap; promoting it would push us over.
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithUser()
            .WithStagedOrganizationUser()
            .WithOrganization(org =>
            {
                org.Seats = 5;
            })
            .AsSelfHosted()
            .WithPM34423StagedStatusFlag()
            .WithMockedSendOrganizationInvitesCommand()
            .BuildAsync();

        var client = testData.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        AssertNoSeatsAvailableRedirect(response);

        // Assert — no invite was sent (seat check must run before the email step).
        var inviteCommand = testData.Factory.Services.GetRequiredService<ISendOrganizationInvitesCommand>();
        await inviteCommand.DidNotReceive().SendInvitesAsync(Arg.Any<SendInvitesRequest>());

        // Assert — Staged row was not mutated.
        var orgUserRepo = testData.Factory.Services.GetRequiredService<IOrganizationUserRepository>();
        var refreshedOrgUser = await orgUserRepo.GetByIdAsync(testData.OrganizationUser!.Id);
        Assert.NotNull(refreshedOrgUser);
        Assert.Equal(OrganizationUserStatusType.Staged, refreshedOrgUser.Status);
        Assert.Null(refreshedOrgUser.UserId);
    }

    /*
    * FAILURE PATH: Mirror of the self-hosted seat-cap test for the cloud path where autoscale
    * cannot grow beyond MaxAutoscaleSeats. Same expectations: NoSeatsAvailable redirect, no
    * invite, no row mutation.
    */
    [Fact]
    public async Task ExternalCallback_WithExistingUser_AndStagedOrgUser_OnCloudAutoscaleFails_RedirectsAndDoesNotInvite()
    {
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithUser()
            .WithStagedOrganizationUser()
            .WithOrganization(org =>
            {
                org.Seats = 5;
                org.MaxAutoscaleSeats = 5;
            })
            .WithPM34423StagedStatusFlag()
            .WithMockedSendOrganizationInvitesCommand()
            .BuildAsync();

        var client = testData.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        AssertNoSeatsAvailableRedirect(response);

        // Assert — no invite was sent.
        var inviteCommand = testData.Factory.Services.GetRequiredService<ISendOrganizationInvitesCommand>();
        await inviteCommand.DidNotReceive().SendInvitesAsync(Arg.Any<SendInvitesRequest>());

        // Assert — Staged row was not mutated.
        var orgUserRepo = testData.Factory.Services.GetRequiredService<IOrganizationUserRepository>();
        var refreshedOrgUser = await orgUserRepo.GetByIdAsync(testData.OrganizationUser!.Id);
        Assert.NotNull(refreshedOrgUser);
        Assert.Equal(OrganizationUserStatusType.Staged, refreshedOrgUser.Status);
        Assert.Null(refreshedOrgUser.UserId);
    }

    /*
    * FAILURE PATH: When SendInvitesAsync throws after the Staged row has already been
    * flipped to Invited, the promotion must roll back so the row stays Staged. Otherwise
    * a seat is consumed (Invited counts against the cap; Staged does not) for an invite
    * the user never received, and the next SSO attempt would dead-end on the "accept your
    * invite" redirect for an invite that was never delivered.
    */
    [Fact]
    public async Task ExternalCallback_WithExistingUser_AndStagedOrgUser_WhenInviteSendFails_RollsBackPromotion()
    {
        // Arrange — existing BW user AND a Staged OrganizationUser row sharing the SSO-claimed email.
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithUser()
            .WithStagedOrganizationUser()
            .WithPM34423StagedStatusFlag()
            .WithMockedSendOrganizationInvitesCommand()
            .BuildAsync();

        // Configure the mocked invite command to throw a plain Exception, simulating a
        // transient failure downstream of HandlebarsMailService (which is not one of the
        // typed exceptions caught by ExternalCallback).
        var inviteCommand = testData.Factory.Services.GetRequiredService<ISendOrganizationInvitesCommand>();
        inviteCommand
            .When(x => x.SendInvitesAsync(Arg.Any<SendInvitesRequest>()))
            .Do(_ => throw new Exception("simulated invite send failure"));

        var client = testData.Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert — plain Exception is not caught by any typed handler in ExternalCallback,
        // so it surfaces as a 500.
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        // Assert — SendInvitesAsync was reached (proves the promotion cleared the seat
        // check and the ReplaceAsync before failing on the invite send).
        await inviteCommand.Received(1).SendInvitesAsync(Arg.Any<SendInvitesRequest>());

        // Assert — the Staged row was rolled back. Without rollback the row is left as
        // Invited and the next SSO attempt hits SsoAuthnRequiresInviteAcceptanceException,
        // stranding the user on /login for an invite that was never delivered.
        var orgUserRepo = testData.Factory.Services.GetRequiredService<IOrganizationUserRepository>();
        var refreshedOrgUser = await orgUserRepo.GetByIdAsync(testData.OrganizationUser!.Id);
        Assert.NotNull(refreshedOrgUser);
        Assert.Equal(OrganizationUserStatusType.Staged, refreshedOrgUser.Status);
        Assert.Null(refreshedOrgUser.UserId);
    }

    /*
    * SUCCESS PATH: Test to verify /Account/ExternalCallback succeeds when an existing user
    * with a valid (Confirmed) organization user status logs in via SSO for the first time.
    */
    [Fact]
    public async Task ExternalCallback_WithExistingUserAndConfirmedOrgUser_ReturnsSuccess()
    {
        // Arrange - Existing user with confirmed org user status, no SSO link yet
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithUser()
            .WithOrganizationUser(orgUser =>
            {
                orgUser.Status = OrganizationUserStatusType.Confirmed;
            })
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
    * SUCCESS PATH: Test to verify /Account/ExternalCallback succeeds when an existing user
    * with Accepted organization user status logs in via SSO.
    */
    [Fact]
    public async Task ExternalCallback_WithExistingUserAndAcceptedOrgUser_ReturnsSuccess()
    {
        // Arrange - Existing user with accepted org user status
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithUser()
            .WithOrganizationUser(orgUser =>
            {
                orgUser.Status = OrganizationUserStatusType.Accepted;
            })
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

    /// <summary>
    /// Asserts the response is a 302 redirect to the web client's
    /// <c>/sso-login-failed</c> terminal page carrying the
    /// <c>no-seats-available</c> kind. Shared by every SSO no-seats test path
    /// (self-hosted, cloud autoscale-fail, autoscale partial-failure rollback,
    /// fresh-JIT, and Staged-promotion variants).
    /// </summary>
    private static void AssertNoSeatsAvailableRedirect(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var location = response.Headers.Location!.ToString();
        Assert.Contains("/sso-login-failed?", location);
        Assert.Contains("kind=no-seats-available", location);
    }
}
