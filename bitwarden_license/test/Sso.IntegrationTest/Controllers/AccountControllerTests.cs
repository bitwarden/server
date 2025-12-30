using System.Net;
using System.Security.Claims;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
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
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var featureService = Substitute.For<IFeatureService>();
                featureService.IsEnabled(FeatureFlagKeys.PM24579_PreventSsoOnExistingNonCompliantUsers).Returns(true);
                services.AddSingleton(featureService);

                // Mock authentication service to return failed result
                var authService = Substitute.For<IAuthenticationService>();
                authService.AuthenticateAsync(
                        Arg.Any<HttpContext>(),
                        AuthenticationSchemes.BitwardenExternalCookieAuthenticationScheme)
                    .Returns(AuthenticateResult.Fail("External authentication error"));
                services.AddSingleton(authService);
            });
        }).CreateClient();

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
        var organizationId = Guid.NewGuid();
        var providerUserId = Guid.NewGuid().ToString();
        var testEmail = "test_user@integration.test";
        var testName = "Test User";

        var organization = new Organization
        {
            Id = organizationId,
            Enabled = true,
            UseSso = true
        };

        // SSO config exists but is disabled
        var ssoConfig = new SsoConfig
        {
            OrganizationId = organizationId,
            Enabled = false
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
                services.AddSingleton(orgRepo);

                // Mock SSO config repository - returns config that is disabled
                var ssoConfigRepo = Substitute.For<ISsoConfigRepository>();
                ssoConfigRepo.GetByOrganizationIdAsync(organizationId).Returns(ssoConfig);
                services.AddSingleton(ssoConfigRepo);

                // Mock authentication service with successful external auth
                var authService = Substitute.For<IAuthenticationService>();
                authService.AuthenticateAsync(
                        Arg.Any<HttpContext>(),
                        AuthenticationSchemes.BitwardenExternalCookieAuthenticationScheme)
                    .Returns(BuildSuccessfulAuthResult(organizationId, providerUserId, testEmail, testName));
                services.AddSingleton(authService);
            });
        }).CreateClient();

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
        var organizationId = Guid.NewGuid();
        var providerUserId = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid();
        var testEmail = "invited_user@integration.test";
        var testName = "Invited User";

        var existingUser = new User
        {
            Id = userId,
            Email = testEmail,
            Name = testName
        };

        var organization = new Organization
        {
            Id = organizationId,
            Enabled = true,
            UseSso = true
        };

        var orgUser = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = userId,
            Status = OrganizationUserStatusType.Confirmed
        };

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
                services.AddSingleton(orgRepo);

                // Mock SSO config repository
                var ssoConfigRepo = Substitute.For<ISsoConfigRepository>();
                ssoConfigRepo.GetByOrganizationIdAsync(organizationId).Returns(null as SsoConfig);
                services.AddSingleton(ssoConfigRepo);

                // Mock user repository
                var userRepo = Substitute.For<IUserRepository>();
                userRepo.GetBySsoUserAsync(providerUserId, organizationId).Returns((User?)null);
                userRepo.GetByEmailAsync(testEmail).Returns(existingUser);
                services.AddSingleton(userRepo);

                // Mock organization user repository with invited user
                var orgUserRepo = Substitute.For<IOrganizationUserRepository>();
                orgUserRepo.GetManyByUserAsync(userId).Returns([orgUser]);
                services.AddSingleton(orgUserRepo);

                // Mock authentication service with successful external auth
                var authService = Substitute.For<IAuthenticationService>();
                authService.AuthenticateAsync(
                        Arg.Any<HttpContext>(),
                        AuthenticationSchemes.BitwardenExternalCookieAuthenticationScheme)
                    .Returns(BuildSuccessfulAuthResult(organizationId, providerUserId, testEmail, testName));
                services.AddSingleton(authService);
            });
        }).CreateClient();

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
        var organizationId = Guid.NewGuid();
        var providerUserId = Guid.NewGuid().ToString();
        var testEmail = "test_user@integration.test";
        var testName = "Test User";
        var expectedAcrValue = "urn:expected:acr:value";
        var invalidAcrValue = "wrong-acr-value";

        var organization = new Organization
        {
            Id = organizationId,
            Enabled = true,
            UseSso = true
        };

        // SSO config with expected ACR value
        var ssoConfigData = new SsoConfigurationData
        {
            ExpectedReturnAcrValue = expectedAcrValue
        };
        var ssoConfig = new SsoConfig
        {
            OrganizationId = organizationId,
            Enabled = true
        };
        ssoConfig.SetData(ssoConfigData);

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
                services.AddSingleton(orgRepo);

                // Mock SSO config repository with expected ACR value
                var ssoConfigRepo = Substitute.For<ISsoConfigRepository>();
                ssoConfigRepo.GetByOrganizationIdAsync(organizationId).Returns(ssoConfig);
                services.AddSingleton(ssoConfigRepo);

                // Mock authentication service with external auth that has missing/invalid ACR
                var authService = Substitute.For<IAuthenticationService>();
                authService.AuthenticateAsync(
                        Arg.Any<HttpContext>(),
                        AuthenticationSchemes.BitwardenExternalCookieAuthenticationScheme)
                    .Returns(BuildSuccessfulAuthResult(organizationId, providerUserId, testEmail, testName, invalidAcrValue));
                services.AddSingleton(authService);
            });
        }).CreateClient();

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
        var organizationId = Guid.NewGuid();
        var testEmail = "test_user@integration.test";
        var testName = "Test User";

        var organization = new Organization
        {
            Id = organizationId,
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
                services.AddSingleton(orgRepo);

                // Mock SSO config repository
                var ssoConfigRepo = Substitute.For<ISsoConfigRepository>();
                ssoConfigRepo.GetByOrganizationIdAsync(organizationId).Returns(ssoConfig);
                services.AddSingleton(ssoConfigRepo);

                // Mock authentication service with auth result that has NO user ID claims
                var authService = Substitute.For<IAuthenticationService>();
                authService.AuthenticateAsync(
                        Arg.Any<HttpContext>(),
                        AuthenticationSchemes.BitwardenExternalCookieAuthenticationScheme)
                    .Returns(BuildSuccessfulAuthResult(organizationId, null, testEmail, testName));
                services.AddSingleton(authService);
            });
        }).CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

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
        var organizationId = Guid.NewGuid();
        // Use a providerUserId WITHOUT @ so it can't be used as fallback email
        var providerUserId = Guid.NewGuid().ToString();
        var testName = "Test User";

        var organization = new Organization
        {
            Id = organizationId,
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

                // Mock user repository - no existing user found via SSO
                var userRepo = Substitute.For<IUserRepository>();
                userRepo.GetBySsoUserAsync(providerUserId, organizationId).Returns((User?)null);
                services.AddSingleton(userRepo);

                // Mock authentication service with auth result that has NO email claim
                var authService = Substitute.For<IAuthenticationService>();
                authService.AuthenticateAsync(
                        Arg.Any<HttpContext>(),
                        AuthenticationSchemes.BitwardenExternalCookieAuthenticationScheme)
                    .Returns(BuildSuccessfulAuthResult(organizationId, providerUserId, null, testName));
                services.AddSingleton(authService);
            });
        }).CreateClient();

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
        var organizationId = Guid.NewGuid();
        var providerUserId = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid();
        var testEmail = "keyconnector_user@integration.test";
        var testName = "Key Connector User";

        // Existing user that uses Key Connector
        var existingUser = new User
        {
            Id = userId,
            Email = testEmail,
            Name = testName,
            UsesKeyConnector = true
        };

        var organization = new Organization
        {
            Id = organizationId,
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

                // Mock user repository - no SSO user, but existing user found by email
                var userRepo = Substitute.For<IUserRepository>();
                userRepo.GetBySsoUserAsync(providerUserId, organizationId).Returns((User?)null);
                userRepo.GetByEmailAsync(testEmail).Returns(existingUser);
                services.AddSingleton(userRepo);

                // Mock organization user repository - NO org user record (user was removed)
                var orgUserRepo = Substitute.For<IOrganizationUserRepository>();
                orgUserRepo.GetManyByUserAsync(userId).Returns(new List<OrganizationUser>());
                orgUserRepo.GetByOrganizationEmailAsync(organizationId, testEmail).Returns((OrganizationUser?)null);
                services.AddSingleton(orgUserRepo);

                // Mock authentication service
                var authService = Substitute.For<IAuthenticationService>();
                authService.AuthenticateAsync(
                        Arg.Any<HttpContext>(),
                        AuthenticationSchemes.BitwardenExternalCookieAuthenticationScheme)
                    .Returns(BuildSuccessfulAuthResult(organizationId, providerUserId, testEmail, testName));
                services.AddSingleton(authService);
            });
        }).CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert - Should fail because user uses Key Connector but has no org user record
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
        var organizationId = Guid.NewGuid();
        var providerUserId = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid();
        var testEmail = "existing_user@integration.test";
        var testName = "Existing User";

        // Existing user that does NOT use Key Connector
        var existingUser = new User
        {
            Id = userId,
            Email = testEmail,
            Name = testName,
            UsesKeyConnector = false
        };

        var organization = new Organization
        {
            Id = organizationId,
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

                // Mock user repository - no SSO user, but existing user found by email
                var userRepo = Substitute.For<IUserRepository>();
                userRepo.GetBySsoUserAsync(providerUserId, organizationId).Returns((User?)null);
                userRepo.GetByEmailAsync(testEmail).Returns(existingUser);
                services.AddSingleton(userRepo);

                // Mock organization user repository - NO org user record (user was removed)
                var orgUserRepo = Substitute.For<IOrganizationUserRepository>();
                orgUserRepo.GetManyByUserAsync(userId).Returns(new List<OrganizationUser>());
                orgUserRepo.GetByOrganizationEmailAsync(organizationId, testEmail).Returns((OrganizationUser?)null);
                services.AddSingleton(orgUserRepo);

                // Mock authentication service
                var authService = Substitute.For<IAuthenticationService>();
                authService.AuthenticateAsync(
                        Arg.Any<HttpContext>(),
                        AuthenticationSchemes.BitwardenExternalCookieAuthenticationScheme)
                    .Returns(BuildSuccessfulAuthResult(organizationId, providerUserId, testEmail, testName));
                services.AddSingleton(authService);
            });
        }).CreateClient();

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
        var organizationId = Guid.NewGuid();
        var providerUserId = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid();
        var testEmail = "invited_existing_user@integration.test";
        var testName = "Invited Existing User";

        var existingUser = new User
        {
            Id = userId,
            Email = testEmail,
            Name = testName,
            UsesKeyConnector = false
        };

        var organization = new Organization
        {
            Id = organizationId,
            Name = "Test Organization",
            Enabled = true,
            UseSso = true
        };

        // Org user exists but with Invited status
        var orgUser = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = userId,
            Status = OrganizationUserStatusType.Invited
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

                // Mock user repository - no SSO user, but existing user found by email
                var userRepo = Substitute.For<IUserRepository>();
                userRepo.GetBySsoUserAsync(providerUserId, organizationId).Returns((User?)null);
                userRepo.GetByEmailAsync(testEmail).Returns(existingUser);
                services.AddSingleton(userRepo);

                // Mock organization user repository - org user exists with Invited status
                var orgUserRepo = Substitute.For<IOrganizationUserRepository>();
                orgUserRepo.GetManyByUserAsync(userId).Returns(new List<OrganizationUser> { orgUser });
                services.AddSingleton(orgUserRepo);

                // Mock authentication service
                var authService = Substitute.For<IAuthenticationService>();
                authService.AuthenticateAsync(
                        Arg.Any<HttpContext>(),
                        AuthenticationSchemes.BitwardenExternalCookieAuthenticationScheme)
                    .Returns(BuildSuccessfulAuthResult(organizationId, providerUserId, testEmail, testName));
                services.AddSingleton(authService);
            });
        }).CreateClient();

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
        // Arrange
        var organizationId = Guid.NewGuid();
        var providerUserId = Guid.NewGuid().ToString();
        var testEmail = "new_user@integration.test";
        var testName = "New User";

        var organization = new Organization
        {
            Id = organizationId,
            Name = "Test Organization",
            Enabled = true,
            UseSso = true,
            Seats = 5 // Organization has seat limit
        };

        var ssoConfig = new SsoConfig
        {
            OrganizationId = organizationId,
            Enabled = true
        };
        ssoConfig.SetData(new SsoConfigurationData());

        // All seats are occupied
        var seatCounts = new OrganizationSeatCounts { Users = 5, Sponsored = 0 };

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var featureService = Substitute.For<IFeatureService>();
                featureService.IsEnabled(FeatureFlagKeys.PM24579_PreventSsoOnExistingNonCompliantUsers).Returns(true);
                services.AddSingleton(featureService);

                // Mock GlobalSettings as self-hosted
                var globalSettings = Substitute.For<IGlobalSettings>();
                globalSettings.SelfHosted.Returns(true);
                services.AddSingleton(globalSettings);

                // Mock organization repository
                var orgRepo = Substitute.For<IOrganizationRepository>();
                orgRepo.GetByIdAsync(organizationId).Returns(organization);
                orgRepo.GetByIdentifierAsync(organizationId.ToString()).Returns(organization);
                orgRepo.GetOccupiedSeatCountByOrganizationIdAsync(organizationId).Returns(seatCounts);
                services.AddSingleton(orgRepo);

                // Mock SSO config repository
                var ssoConfigRepo = Substitute.For<ISsoConfigRepository>();
                ssoConfigRepo.GetByOrganizationIdAsync(organizationId).Returns(ssoConfig);
                services.AddSingleton(ssoConfigRepo);

                // Mock user repository - no existing user
                var userRepo = Substitute.For<IUserRepository>();
                userRepo.GetBySsoUserAsync(providerUserId, organizationId).Returns((User?)null);
                userRepo.GetByEmailAsync(testEmail).Returns((User?)null);
                services.AddSingleton(userRepo);

                // Mock organization user repository - no org user
                var orgUserRepo = Substitute.For<IOrganizationUserRepository>();
                orgUserRepo.GetByOrganizationEmailAsync(organizationId, testEmail).Returns((OrganizationUser?)null);
                services.AddSingleton(orgUserRepo);

                // Mock authentication service
                var authService = Substitute.For<IAuthenticationService>();
                authService.AuthenticateAsync(
                        Arg.Any<HttpContext>(),
                        AuthenticationSchemes.BitwardenExternalCookieAuthenticationScheme)
                    .Returns(BuildSuccessfulAuthResult(organizationId, providerUserId, testEmail, testName));
                services.AddSingleton(authService);
            });
        }).CreateClient();

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
        // Arrange
        var organizationId = Guid.NewGuid();
        var providerUserId = Guid.NewGuid().ToString();
        var testEmail = "new_user@integration.test";
        var testName = "New User";

        var organization = new Organization
        {
            Id = organizationId,
            Name = "Test Organization",
            Enabled = true,
            UseSso = true,
            Seats = 5 // Organization has seat limit
        };

        var ssoConfig = new SsoConfig
        {
            OrganizationId = organizationId,
            Enabled = true
        };
        ssoConfig.SetData(new SsoConfigurationData());

        // All seats are occupied
        var seatCounts = new OrganizationSeatCounts { Users = 5, Sponsored = 0 };

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var featureService = Substitute.For<IFeatureService>();
                featureService.IsEnabled(FeatureFlagKeys.PM24579_PreventSsoOnExistingNonCompliantUsers).Returns(true);
                services.AddSingleton(featureService);

                // Mock GlobalSettings as cloud-hosted (not self-hosted)
                var globalSettings = Substitute.For<IGlobalSettings>();
                globalSettings.SelfHosted.Returns(false);
                services.AddSingleton(globalSettings);

                // Mock organization service - AutoAddSeatsAsync fails
                var orgService = Substitute.For<IOrganizationService>();
                orgService.AutoAddSeatsAsync(organization, 1)
                    .Returns(Task.FromException(new Exception("Cannot add seats: payment method required")));
                services.AddSingleton(orgService);

                // Mock organization repository
                var orgRepo = Substitute.For<IOrganizationRepository>();
                orgRepo.GetByIdAsync(organizationId).Returns(organization);
                orgRepo.GetByIdentifierAsync(organizationId.ToString()).Returns(organization);
                orgRepo.GetOccupiedSeatCountByOrganizationIdAsync(organizationId).Returns(seatCounts);
                services.AddSingleton(orgRepo);

                // Mock SSO config repository
                var ssoConfigRepo = Substitute.For<ISsoConfigRepository>();
                ssoConfigRepo.GetByOrganizationIdAsync(organizationId).Returns(ssoConfig);
                services.AddSingleton(ssoConfigRepo);

                // Mock user repository - no existing user
                var userRepo = Substitute.For<IUserRepository>();
                userRepo.GetBySsoUserAsync(providerUserId, organizationId).Returns((User?)null);
                userRepo.GetByEmailAsync(testEmail).Returns((User?)null);
                services.AddSingleton(userRepo);

                // Mock organization user repository - no org user
                var orgUserRepo = Substitute.For<IOrganizationUserRepository>();
                orgUserRepo.GetByOrganizationEmailAsync(organizationId, testEmail).Returns((OrganizationUser?)null);
                services.AddSingleton(orgUserRepo);

                // Mock authentication service
                var authService = Substitute.For<IAuthenticationService>();
                authService.AuthenticateAsync(
                        Arg.Any<HttpContext>(),
                        AuthenticationSchemes.BitwardenExternalCookieAuthenticationScheme)
                    .Returns(BuildSuccessfulAuthResult(organizationId, providerUserId, testEmail, testName));
                services.AddSingleton(authService);
            });
        }).CreateClient();

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
        var organizationId = Guid.NewGuid();
        var providerUserId = Guid.NewGuid().ToString();
        var testName = "New User";
        var userIdentifier = "manual-link,token-123"; // Non-empty to bypass first email check

        var organization = new Organization
        {
            Id = organizationId,
            Name = "Test Organization",
            Enabled = true,
            UseSso = true,
            Seats = null // No seat limit to skip seat check
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

                // Mock user repository - no existing user via SSO or manual linking
                var userRepo = Substitute.For<IUserRepository>();
                userRepo.GetBySsoUserAsync(providerUserId, organizationId).Returns((User?)null);
                userRepo.GetByIdAsync(Arg.Any<Guid>()).Returns((User?)null);
                services.AddSingleton(userRepo);

                // Mock organization user repository - no org user
                var orgUserRepo = Substitute.For<IOrganizationUserRepository>();
                orgUserRepo.GetByOrganizationEmailAsync(organizationId, Arg.Any<string>()).Returns((OrganizationUser?)null);
                services.AddSingleton(orgUserRepo);

                // Mock authentication service - NO email claim, but with userIdentifier
                var authService = Substitute.For<IAuthenticationService>();
                authService.AuthenticateAsync(
                        Arg.Any<HttpContext>(),
                        AuthenticationSchemes.BitwardenExternalCookieAuthenticationScheme)
                    .Returns(BuildSuccessfulAuthResult(organizationId, providerUserId, null, testName, null, userIdentifier));
                services.AddSingleton(authService);
            });
        }).CreateClient();

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
        var organizationId = Guid.NewGuid();
        var providerUserId = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid();
        var testEmail = "unknown_status_user@integration.test";
        var testName = "Unknown Status User";

        var existingSsoUser = new User
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

        // Org user with an invalid/unknown status (casting invalid integer to enum)
        var orgUser = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = userId,
            Status = (OrganizationUserStatusType)99 // Invalid enum value - simulates future status or data corruption
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
                // Feature flag enabled to trigger PreventOrgUserLoginIfStatusInvalidAsync
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

                // Mock user repository - user IS found via SSO lookup
                var userRepo = Substitute.For<IUserRepository>();
                userRepo.GetBySsoUserAsync(providerUserId, organizationId).Returns(existingSsoUser);
                services.AddSingleton(userRepo);

                // Mock organization user repository - returns org user with invalid status
                var orgUserRepo = Substitute.For<IOrganizationUserRepository>();
                orgUserRepo.GetManyByUserAsync(userId).Returns(new List<OrganizationUser> { orgUser });
                services.AddSingleton(orgUserRepo);

                // Mock authentication service
                var authService = Substitute.For<IAuthenticationService>();
                authService.AuthenticateAsync(
                        Arg.Any<HttpContext>(),
                        AuthenticationSchemes.BitwardenExternalCookieAuthenticationScheme)
                    .Returns(BuildSuccessfulAuthResult(organizationId, providerUserId, testEmail, testName));
                services.AddSingleton(authService);
            });
        }).CreateClient();

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
    */
    [Fact]
    public async Task ExternalCallback_WithInvalidUserIdentifierFormat_ReturnsError()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var providerUserId = Guid.NewGuid().ToString();
        var testEmail = "test_user@integration.test";
        var testName = "Test User";
        // Invalid format - missing comma separator (should be "userId,token")
        var userIdentifier = "invalid-identifier-no-comma";

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

                // Mock authentication service with invalid userIdentifier format
                var authService = Substitute.For<IAuthenticationService>();
                authService.AuthenticateAsync(
                        Arg.Any<HttpContext>(),
                        AuthenticationSchemes.BitwardenExternalCookieAuthenticationScheme)
                    .Returns(BuildSuccessfulAuthResult(organizationId, providerUserId, testEmail, testName, null, userIdentifier));
                services.AddSingleton(authService);
            });
        }).CreateClient();

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
                    .Returns(BuildSuccessfulAuthResult(organizationId, providerUserId, testEmail, testName, null, userIdentifier));
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
        var organizationId = Guid.NewGuid();
        var providerUserId = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid();
        var testEmail = "invalid_status_user@integration.test";
        var testName = "Invalid Status User";

        var existingUser = new User
        {
            Id = userId,
            Email = testEmail,
            Name = testName
        };

        var organization = new Organization
        {
            Id = organizationId,
            Enabled = true,
            UseSso = true
        };

        var orgUser = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = userId,
            Status = OrganizationUserStatusType.Revoked
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
                services.AddSingleton(orgRepo);

                // Mock SSO config repository
                var ssoConfigRepo = Substitute.For<ISsoConfigRepository>();
                ssoConfigRepo.GetByOrganizationIdAsync(organizationId).Returns(ssoConfig);
                services.AddSingleton(ssoConfigRepo);

                // Mock user repository - existing user via SSO
                var userRepo = Substitute.For<IUserRepository>();
                userRepo.GetBySsoUserAsync(providerUserId, organizationId).Returns(existingUser);
                services.AddSingleton(userRepo);

                // Mock organization user repository with org user
                var orgUserRepo = Substitute.For<IOrganizationUserRepository>();
                orgUserRepo.GetManyByUserAsync(userId).Returns([orgUser]);
                services.AddSingleton(orgUserRepo);

                // Mock authentication service with successful external auth
                var authService = Substitute.For<IAuthenticationService>();
                authService.AuthenticateAsync(
                        Arg.Any<HttpContext>(),
                        AuthenticationSchemes.BitwardenExternalCookieAuthenticationScheme)
                    .Returns(BuildSuccessfulAuthResult(organizationId, providerUserId, testEmail, testName));
                services.AddSingleton(authService);
            });
        }).CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert - Should fail because user state is invalid
        var stringResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains(
            $"Your access to organization {organization.DisplayName()} has been revoked. Please contact your administrator for assistance.",
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
        var organizationId = Guid.NewGuid();
        var providerUserId = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid();
        var testEmail = "revoked_user@integration.test";
        var testName = "Revoked User";

        var existingUser = new User
        {
            Id = userId,
            Email = testEmail,
            Name = testName
        };

        var organization = new Organization
        {
            Id = organizationId,
            Enabled = true,
            UseSso = true
        };

        var orgUser = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = userId,
            Status = OrganizationUserStatusType.Revoked
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
                featureService.IsEnabled(FeatureFlagKeys.PM24579_PreventSsoOnExistingNonCompliantUsers).Returns(false);
                services.AddSingleton(featureService);

                // Mock organization repository
                var orgRepo = Substitute.For<IOrganizationRepository>();
                orgRepo.GetByIdAsync(organizationId).Returns(organization);
                services.AddSingleton(orgRepo);

                // Mock SSO config repository
                var ssoConfigRepo = Substitute.For<ISsoConfigRepository>();
                ssoConfigRepo.GetByOrganizationIdAsync(organizationId).Returns(ssoConfig);
                services.AddSingleton(ssoConfigRepo);

                // Mock user repository
                var userRepo = Substitute.For<IUserRepository>();
                userRepo.GetBySsoUserAsync(providerUserId, organizationId).Returns((User?)null);
                userRepo.GetByEmailAsync(testEmail).Returns(existingUser);
                services.AddSingleton(userRepo);

                // Mock organization user repository with invited user
                var orgUserRepo = Substitute.For<IOrganizationUserRepository>();
                orgUserRepo.GetManyByUserAsync(userId).Returns([orgUser]);
                services.AddSingleton(orgUserRepo);

                // Mock authentication service with successful external auth
                var authService = Substitute.For<IAuthenticationService>();
                authService.AuthenticateAsync(
                        Arg.Any<HttpContext>(),
                        AuthenticationSchemes.BitwardenExternalCookieAuthenticationScheme)
                    .Returns(BuildSuccessfulAuthResult(organizationId, providerUserId, testEmail, testName));
                services.AddSingleton(authService);
            });
        }).CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert - Should fail because user has invalid status
        var stringResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains(
            $"Your access to organization {organization.DisplayName()} has been revoked. Please contact your administrator for assistance.",
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
        var organizationId = Guid.NewGuid();
        var providerUserId = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid();
        var testEmail = "invited_user@integration.test";
        var testName = "Invited User";

        var existingUser = new User
        {
            Id = userId,
            Email = testEmail,
            Name = testName
        };

        var organization = new Organization
        {
            Id = organizationId,
            Enabled = true,
            UseSso = true
        };

        var orgUser = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = userId,
            Status = OrganizationUserStatusType.Invited
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
                featureService.IsEnabled(FeatureFlagKeys.PM24579_PreventSsoOnExistingNonCompliantUsers).Returns(false);
                services.AddSingleton(featureService);

                // Mock organization repository
                var orgRepo = Substitute.For<IOrganizationRepository>();
                orgRepo.GetByIdAsync(organizationId).Returns(organization);
                services.AddSingleton(orgRepo);

                // Mock SSO config repository
                var ssoConfigRepo = Substitute.For<ISsoConfigRepository>();
                ssoConfigRepo.GetByOrganizationIdAsync(organizationId).Returns(ssoConfig);
                services.AddSingleton(ssoConfigRepo);

                // Mock user repository
                var userRepo = Substitute.For<IUserRepository>();
                userRepo.GetBySsoUserAsync(providerUserId, organizationId).Returns((User?)null);
                userRepo.GetByEmailAsync(testEmail).Returns(existingUser);
                services.AddSingleton(userRepo);

                // Mock organization user repository with invited user
                var orgUserRepo = Substitute.For<IOrganizationUserRepository>();
                orgUserRepo.GetManyByUserAsync(userId).Returns([orgUser]);
                services.AddSingleton(orgUserRepo);

                // Mock authentication service with successful external auth
                var authService = Substitute.For<IAuthenticationService>();
                authService.AuthenticateAsync(
                        Arg.Any<HttpContext>(),
                        AuthenticationSchemes.BitwardenExternalCookieAuthenticationScheme)
                    .Returns(BuildSuccessfulAuthResult(organizationId, providerUserId, testEmail, testName));
                services.AddSingleton(authService);
            });
        }).CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert - Should fail because user has invalid status
        var stringResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains(
        $"To accept your invite to {organization.DisplayName()}, you must first log in using your master password. Once your invite has been accepted, you will be able to log in using SSO.",
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
        var organizationId = Guid.NewGuid();
        var providerUserId = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid();
        var testEmail = "sso_user@integration.test";
        var testName = "SSO User";

        // User exists and was previously linked via SSO
        var existingSsoUser = new User
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
                // Feature flag enabled - this triggers PreventOrgUserLoginIfStatusInvalidAsync
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

                // Mock user repository - user IS found via SSO lookup (previously linked)
                var userRepo = Substitute.For<IUserRepository>();
                userRepo.GetBySsoUserAsync(providerUserId, organizationId).Returns(existingSsoUser);
                services.AddSingleton(userRepo);

                // Mock organization user repository - NO org user record
                var orgUserRepo = Substitute.For<IOrganizationUserRepository>();
                orgUserRepo.GetManyByUserAsync(userId).Returns(new List<OrganizationUser>());
                orgUserRepo.GetByOrganizationEmailAsync(organizationId, testEmail).Returns((OrganizationUser?)null);
                services.AddSingleton(orgUserRepo);

                // Mock authentication service
                var authService = Substitute.For<IAuthenticationService>();
                authService.AuthenticateAsync(
                        Arg.Any<HttpContext>(),
                        AuthenticationSchemes.BitwardenExternalCookieAuthenticationScheme)
                    .Returns(BuildSuccessfulAuthResult(organizationId, providerUserId, testEmail, testName));
                services.AddSingleton(authService);
            });
        }).CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert - Should fail because org user cannot be found
        var stringResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains("Could not find organization user", stringResponse);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    #region Helper Methods

    private static AuthenticateResult BuildSuccessfulAuthResult(
        Guid organizationId,
        string? providerUserId,
        string? email,
        string? name = null,
        string? acrValue = null,
        string? userIdentifier = null)
    {
        var claims = new List<Claim>();

        if (!string.IsNullOrEmpty(email))
        {
            claims.Add(new Claim(JwtClaimTypes.Email, email));
        }

        if (!string.IsNullOrEmpty(providerUserId))
        {
            claims.Add(new Claim(JwtClaimTypes.Subject, providerUserId));
        }

        if (!string.IsNullOrEmpty(name))
        {
            claims.Add(new Claim(JwtClaimTypes.Name, name));
        }

        if (!string.IsNullOrEmpty(acrValue))
        {
            claims.Add(new Claim(JwtClaimTypes.AuthenticationContextClassReference, acrValue));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "External"));
        var properties = new AuthenticationProperties
        {
            Items =
            {
                ["scheme"] = organizationId.ToString(),
                ["return_url"] = "~/",
                ["state"] = "test-state",
                ["user_identifier"] = userIdentifier ?? string.Empty
            }
        };

        var ticket = new AuthenticationTicket(
            principal,
            properties,
            AuthenticationSchemes.BitwardenExternalCookieAuthenticationScheme);

        return AuthenticateResult.Success(ticket);
    }

    #endregion
}
