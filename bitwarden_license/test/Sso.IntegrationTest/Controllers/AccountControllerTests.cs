using System.Net;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
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
    * Test to verify behavior of /Account/ExternalCallback with PM24579 feature flag
    */
    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task ExternalCallback_WithPM24579FeatureFlag_AndNoAuthCookie_ReturnsError
    (
        bool featureFlagEnabled
    )
    {
        // Arrange
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var featureService = Substitute.For<IFeatureService>();
                featureService.IsEnabled(FeatureFlagKeys.PM24579_PreventSsoOnExistingNonCompliantUsers).Returns(featureFlagEnabled);
                services.AddSingleton(featureService);
            });
        }).CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert
        Assert.False(response.IsSuccessStatusCode);
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
    * (not using Key Connector) has no org user record - they were removed from the organization.
    */
    [Fact]
    public async Task ExternalCallback_WithExistingUser_AndNoOrgUser_ReturnsError()
    {
        // Arrange
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithUser()
            .BuildAsync();

        var client = testData.Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert - Should fail because user exists but has no org user record
        var stringResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains("You were removed from the organization managing single sign-on for your account. Contact the organization administrator", stringResponse);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    /*
    * Test to verify /Account/ExternalCallback returns error when an existing user
    * has an org user record with Invited status - they must accept the invite first.
    */
    [Fact]
    public async Task ExternalCallback_WithExistingUser_AndInvitedOrgUserStatus_ReturnsError()
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

        var client = testData.Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert - Should fail because user must accept invite before using SSO
        var stringResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains("you must first log in using your master password", stringResponse);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    /*
    * Test to verify /Account/ExternalCallback returns error when organization has no available seats
    * and cannot auto-scale because it's a self-hosted instance.
    */
    [Fact]
    public async Task ExternalCallback_WithNoAvailableSeats_OnSelfHosted_ReturnsError()
    {
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithOrganization(org =>
            {
                org.Seats = 5; // Organization has seat limit
            })
            .AsSelfHosted()
            .BuildAsync();

        var client = testData.Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert - Should fail because no seats available and cannot auto-scale on self-hosted
        var stringResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains("No seats available for organization", stringResponse);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    /*
    * Test to verify /Account/ExternalCallback returns error when organization has no available seats
    * and auto-scaling fails (e.g., billing issue, max seats reached).
    */
    [Fact]
    public async Task ExternalCallback_WithNoAvailableSeats_AndAutoAddSeatsFails_ReturnsError()
    {
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithOrganization(org =>
            {
                org.Seats = 5;
                org.MaxAutoscaleSeats = 5;
            })
            .BuildAsync();

        var client = testData.Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert - Should fail because auto-adding seats failed
        var stringResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains("No seats available for organization", stringResponse);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
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
                var featureService = Substitute.For<IFeatureService>();
                featureService.IsEnabled(FeatureFlagKeys.PM24579_PreventSsoOnExistingNonCompliantUsers).Returns(true);
                services.AddSingleton(featureService);

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
    * Test to verify /Account/ExternalCallback returns error for revoked org user when PM24579 feature flag is enabled.
    */
    [Fact]
    public async Task ExternalCallback_WithRevokedOrgUser_WithPM24579FeatureFlagEnabled_ReturnsError()
    {
        // Arrange
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithUser()
            .WithOrganizationUser(orgUser =>
            {
                orgUser.Status = OrganizationUserStatusType.Revoked;
            })
            .WithFeatureFlags(factoryService =>
            {
                factoryService.SubstituteService<IFeatureService>(srv =>
                {
                    srv.IsEnabled(FeatureFlagKeys.PM24579_PreventSsoOnExistingNonCompliantUsers).Returns(true);
                });
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
    * Test to verify /Account/ExternalCallback returns error for revoked org user when PM24579 feature flag is disabled.
    */
    [Fact]
    public async Task ExternalCallback_WithRevokedOrgUserStatus_WithPM24579FeatureFlagDisabled_ReturnsError()
    {
        // Arrange
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithUser()
            .WithOrganizationUser(orgUser =>
            {
                orgUser.Status = OrganizationUserStatusType.Revoked;
            })
            .WithFeatureFlags(factoryService =>
            {
                factoryService.SubstituteService<IFeatureService>(srv =>
                {
                    srv.IsEnabled(FeatureFlagKeys.PM24579_PreventSsoOnExistingNonCompliantUsers).Returns(false);
                });
            })
            .BuildAsync();

        var client = testData.Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert - Should fail because user has invalid status
        var stringResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains(
            $"Your access to organization {testData.Organization?.DisplayName()} has been revoked. Please contact your administrator for assistance.",
            stringResponse);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    /*
    * Test to verify /Account/ExternalCallback returns error for invited org user when PM24579 feature flag is disabled.
    */
    [Fact]
    public async Task ExternalCallback_WithInvitedOrgUserStatus_WithPM24579FeatureFlagDisabled_ReturnsError()
    {
        // Arrange
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithUser()
            .WithOrganizationUser(orgUser =>
            {
                orgUser.Status = OrganizationUserStatusType.Invited;
            })
            .WithFeatureFlags(factoryService =>
            {
                factoryService.SubstituteService<IFeatureService>(srv =>
                {
                    srv.IsEnabled(FeatureFlagKeys.PM24579_PreventSsoOnExistingNonCompliantUsers).Returns(false);
                });
            })
            .BuildAsync();

        var client = testData.Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert - Should fail because user has invalid status
        var stringResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains(
        $"To accept your invite to {testData.Organization?.DisplayName()}, you must first log in using your master password. Once your invite has been accepted, you will be able to log in using SSO.",
            stringResponse);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }


    /*
    * Test to verify /Account/ExternalCallback returns error when user is found via SSO
    * but has no organization user record (with feature flag enabled).
    */
    [Fact]
    public async Task ExternalCallback_WithSsoUser_AndNoOrgUser_WithFeatureFlagEnabled_ReturnsError()
    {
        // Arrange
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithUser()
            .WithSsoUser()
            .WithFeatureFlags(factoryService =>
            {
                factoryService.SubstituteService<IFeatureService>(srv =>
                {
                    srv.IsEnabled(FeatureFlagKeys.PM24579_PreventSsoOnExistingNonCompliantUsers).Returns(true);
                });
            })
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
}
