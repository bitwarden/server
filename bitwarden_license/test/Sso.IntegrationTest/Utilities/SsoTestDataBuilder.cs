using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bitwarden.License.Test.Sso.IntegrationTest.Utilities;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authentication;
using NSubstitute;
using AuthenticationSchemes = Bit.Core.AuthenticationSchemes;

namespace Bit.Sso.IntegrationTest.Utilities;

/// <summary>
/// Contains the factory and all entities created by <see cref="SsoTestDataBuilder"/> for use in integration tests.
/// </summary>
public record SsoTestData(
    SsoApplicationFactory Factory,
    Organization? Organization,
    User? User,
    OrganizationUser? OrganizationUser,
    SsoConfig? SsoConfig,
    SsoUser? SsoUser);

/// <summary>
/// Builder for creating SSO test data with seeded database entities.
/// </summary>
public class SsoTestDataBuilder
{
    /// <summary>
    /// This UserIdentifier is a mock for the UserIdentifier we get from the External Identity Provider.
    /// </summary>
    private string? _userIdentifier;
    private Action<Organization>? _organizationConfig;
    private Action<User>? _userConfig;
    private Action<OrganizationUser>? _orgUserConfig;
    private Action<OrganizationUser>? _stagedOrgUserConfig;
    private Action<SsoConfig>? _ssoConfigConfig;
    private Action<SsoUser>? _ssoUserConfig;
    private Action<SsoApplicationFactory>? _featureFlagConfig;

    private bool _includeUser = false;
    private bool _includeSsoUser = false;
    private bool _includeOrganizationUser = false;
    private bool _includeStagedOrganizationUser = false;
    private bool _includeSsoConfig = false;
    private bool _successfulAuth = true;
    private bool _withNullEmail = false;
    private bool _isSelfHosted = false;
    private bool _includeProviderUserId = true;
    private bool _useNonExistentOrgInAuth = false;
    private bool _isNativeClient = false;
    private bool _mockAutoscaleSuccess = false;
    private bool _mockAutoscalePartialFailure = false;
    private bool _mockSendOrganizationInvitesCommand = false;

    public SsoTestDataBuilder WithOrganization(Action<Organization> configure)
    {
        _organizationConfig = configure;
        return this;
    }

    public SsoTestDataBuilder WithUser(Action<User>? configure = null)
    {
        _includeUser = true;
        _userConfig = configure;
        return this;
    }

    public SsoTestDataBuilder WithOrganizationUser(Action<OrganizationUser>? configure = null)
    {
        _includeOrganizationUser = true;
        _orgUserConfig = configure;
        return this;
    }

    /// <summary>
    /// Seeds an OrganizationUser in <see cref="OrganizationUserStatusType.Staged"/> with
    /// <c>UserId = null</c> and <c>Email</c> matching the SSO-claimed email, mirroring
    /// the directory-import placeholder pattern. Does not seed a BW User — use in
    /// combination with an omitted <see cref="WithUser(Action{User}?)"/> to exercise
    /// the JIT-against-Staged path.
    /// </summary>
    public SsoTestDataBuilder WithStagedOrganizationUser(Action<OrganizationUser>? configure = null)
    {
        _includeStagedOrganizationUser = true;
        _stagedOrgUserConfig = configure;
        return this;
    }

    public SsoTestDataBuilder WithSsoConfig(Action<SsoConfig>? configure = null)
    {
        _includeSsoConfig = true;
        _ssoConfigConfig = configure;
        return this;
    }

    public SsoTestDataBuilder WithSsoUser(Action<SsoUser>? configure = null)
    {
        _includeSsoUser = true;
        _ssoUserConfig = configure;
        return this;
    }

    public SsoTestDataBuilder WithFeatureFlags(Action<SsoApplicationFactory> configure)
    {
        _featureFlagConfig = configure;
        return this;
    }

    public SsoTestDataBuilder WithFailedAuthentication()
    {
        _successfulAuth = false;
        return this;
    }

    public SsoTestDataBuilder WithNullEmail()
    {
        _withNullEmail = true;
        return this;
    }

    public SsoTestDataBuilder WithUserIdentifier(string userIdentifier)
    {
        _userIdentifier = userIdentifier;
        return this;
    }

    public SsoTestDataBuilder OmitProviderUserId()
    {
        _includeProviderUserId = false;
        return this;
    }

    public SsoTestDataBuilder AsSelfHosted()
    {
        _isSelfHosted = true;
        return this;
    }

    /// <summary>
    /// Causes the auth result to use a different (non-existent) organization ID than what is seeded
    /// in the database. This simulates the "organization not found" scenario.
    /// </summary>
    public SsoTestDataBuilder WithNonExistentOrganizationInAuth()
    {
        _useNonExistentOrgInAuth = true;
        return this;
    }

    /// <summary>
    /// Configures the test to simulate a native client (non-browser) OIDC flow.
    /// Native clients use custom URI schemes (e.g., "bitwarden://callback") instead of http/https.
    /// This causes ExternalCallback to return a View with 200 status instead of a redirect.
    /// </summary>
    public SsoTestDataBuilder AsNativeClient()
    {
        _isNativeClient = true;
        return this;
    }

    /// <summary>
    /// Enables the <see cref="FeatureFlagKeys.PM34423StagedStatus"/> feature flag for the test.
    /// SSO Staged-row promotion (Scenario 3 in AutoProvisionUserAsync) is gated behind this
    /// flag, so tests exercising that branch must opt in.
    /// </summary>
    public SsoTestDataBuilder WithPM34423StagedStatusFlag(bool enabled = true)
    {
        return WithFeatureFlags(factory =>
        {
            factory.SubstituteService<Bitwarden.Server.Sdk.Features.IFeatureService>(svc =>
            {
                svc.IsEnabled(FeatureFlagKeys.PM34423StagedStatus).Returns(enabled);
            });
        });
    }

    /// <summary>
    /// Substitutes <see cref="ISendOrganizationInvitesCommand"/> so <c>SendInvitesAsync</c>
    /// no-ops instead of exercising the real mail pipeline. Use in tests that want to
    /// assert an invite was (or was not) issued, and to keep integration runs from
    /// touching the real email path.
    /// </summary>
    public SsoTestDataBuilder WithMockedSendOrganizationInvitesCommand()
    {
        _mockSendOrganizationInvitesCommand = true;
        return this;
    }

    /// <summary>
    /// Substitutes <see cref="IOrganizationService"/> so <c>AutoAddSeatsAsync</c> succeeds
    /// without hitting the real billing gateway. Use to exercise the "at cap, autoscale
    /// succeeds" branch of the seat-availability check in integration.
    /// </summary>
    public SsoTestDataBuilder WithAutoscaleSucceeds()
    {
        _mockAutoscaleSuccess = true;
        return this;
    }

    /// <summary>
    /// Substitutes <see cref="IOrganizationService"/> so <c>AutoAddSeatsAsync</c> bumps
    /// <c>organization.Seats</c> by one and then throws. Exercises the catch-path
    /// mid-flight rollback branch that calls <c>AdjustSeatsAsync</c> to revert the
    /// partial mutation before rethrowing <c>NoSeatsAvailable</c>.
    /// </summary>
    public SsoTestDataBuilder WithAutoscalePartialFailure()
    {
        _mockAutoscalePartialFailure = true;
        return this;
    }

    public async Task<SsoTestData> BuildAsync()
    {
        // Create factory
        var factory = new SsoApplicationFactory();

        // Pre-generate IDs and values needed for auth mock (before accessing Services)
        var organizationId = Guid.NewGuid();
        // Use a different org ID in auth if testing "organization not found" scenario
        var authOrganizationId = _useNonExistentOrgInAuth ? Guid.NewGuid() : organizationId;
        var providerUserId = _includeProviderUserId ? Guid.NewGuid().ToString() : "";
        var userEmail = _withNullEmail ? null : $"user_{Guid.NewGuid()}@test.com";
        var userName = "TestUser";

        // 1. Configure mocked authentication service BEFORE accessing Services
        factory.SubstituteService<IAuthenticationService>(authService =>
        {
            if (_successfulAuth)
            {
                authService.AuthenticateAsync(
                        Arg.Any<HttpContext>(),
                        AuthenticationSchemes.BitwardenExternalCookieAuthenticationScheme)
                    .Returns(MockSuccessfulAuthResult.Build(
                        authOrganizationId,
                        providerUserId,
                        userEmail,
                        userName,
                        acrValue: null,
                        _userIdentifier));
            }
            else
            {
                authService.AuthenticateAsync(
                        Arg.Any<HttpContext>(),
                        AuthenticationSchemes.BitwardenExternalCookieAuthenticationScheme)
                    .Returns(AuthenticateResult.Fail("External authentication error"));
            }
        });

        // 1.a Configure GlobalSettings for Self-Hosted and seat limit
        factory.SubstituteService<IGlobalSettings>(globalSettings =>
        {
            globalSettings.SelfHosted.Returns(_isSelfHosted);
        });

        // 1.b configure setting feature flags
        _featureFlagConfig?.Invoke(factory);

        // 1.c Configure IIdentityServerInteractionService for native client flow
        if (_isNativeClient)
        {
            factory.SubstituteService<IIdentityServerInteractionService>(interaction =>
            {
                // Native clients have redirect URIs that don't start with http/https
                // e.g., "bitwarden://callback" or "com.bitwarden.app://callback"
                var authorizationRequest = new AuthorizationRequest
                {
                    RedirectUri = "bitwarden://sso-callback"
                };
                interaction.GetAuthorizationContextAsync(Arg.Any<string>())
                    .Returns(authorizationRequest);
            });
        }

        // 1.d Configure ISendOrganizationInvitesCommand for tests that exercise the invite path
        if (_mockSendOrganizationInvitesCommand)
        {
            factory.SubstituteService<ISendOrganizationInvitesCommand>(_ => { });
        }

        // 1.e Configure IOrganizationService for autoscale-branch tests
        if (_mockAutoscaleSuccess)
        {
            factory.SubstituteService<IOrganizationService>(orgService =>
            {
                orgService.AutoAddSeatsAsync(Arg.Any<Organization>(), Arg.Any<int>())
                    .Returns(Task.CompletedTask);
            });
        }
        else if (_mockAutoscalePartialFailure)
        {
            factory.SubstituteService<IOrganizationService>(orgService =>
            {
                orgService
                    .When(x => x.AutoAddSeatsAsync(Arg.Any<Organization>(), Arg.Any<int>()))
                    .Do(callInfo =>
                    {
                        var org = callInfo.Arg<Organization>();
                        org.Seats = org.Seats!.Value + 1;
                        throw new Exception("simulated partial-autoscale failure");
                    });
            });
        }

        if (!_successfulAuth)
        {
            return new SsoTestData(factory, null!, null!, null!, null!, null!);
        }

        // 2. Create Organization with defaults (using pre-generated ID)
        var organization = new Organization
        {
            Id = organizationId,
            Name = "Test Organization",
            BillingEmail = "billing@test.com",
            Plan = "Enterprise",
            Enabled = true,
            UseSso = true
        };
        _organizationConfig?.Invoke(organization);

        var orgRepo = factory.Services.GetRequiredService<IOrganizationRepository>();
        organization = await orgRepo.CreateAsync(organization);

        // 3. Create User with defaults (using pre-generated values)
        User? user = null;
        if (_includeUser)
        {
            user = new User
            {
                Email = userEmail ?? $"email_{Guid.NewGuid()}@test.dev",
                Name = userName,
                ApiKey = Guid.NewGuid().ToString(),
                SecurityStamp = Guid.NewGuid().ToString()
            };
            _userConfig?.Invoke(user);

            var userRepo = factory.Services.GetRequiredService<IUserRepository>();
            user = await userRepo.CreateAsync(user);
        }

        // 4. Create OrganizationUser linking them
        OrganizationUser? orgUser = null;
        if (_includeOrganizationUser)
        {
            orgUser = new OrganizationUser
            {
                OrganizationId = organization.Id,
                UserId = user!.Id,
                Status = OrganizationUserStatusType.Confirmed,
                Type = OrganizationUserType.User
            };
            _orgUserConfig?.Invoke(orgUser);

            var orgUserRepo = factory.Services.GetRequiredService<IOrganizationUserRepository>();
            orgUser = await orgUserRepo.CreateAsync(orgUser);
        }

        // 4.a Create Staged OrganizationUser (directory-import placeholder shape).
        if (_includeStagedOrganizationUser)
        {
            orgUser = new OrganizationUser
            {
                OrganizationId = organization.Id,
                UserId = null,
                Email = userEmail,
                Status = OrganizationUserStatusType.Staged,
                Type = OrganizationUserType.User
            };
            _stagedOrgUserConfig?.Invoke(orgUser);

            var orgUserRepo = factory.Services.GetRequiredService<IOrganizationUserRepository>();
            orgUser = await orgUserRepo.CreateAsync(orgUser);
        }

        // 4.a Create many OrganizationUser to test seat count logic
        if (organization.Seats > 1)
        {
            var orgUserRepo = factory.Services.GetRequiredService<IOrganizationUserRepository>();
            var userRepo = factory.Services.GetRequiredService<IUserRepository>();
            var additionalOrgUsers = new List<OrganizationUser>();
            for (var i = 1; i <= organization.Seats; i++)
            {
                var additionalUser = new User
                {
                    Email = $"additional_user_{i}_{Guid.NewGuid()}@test.dev",
                    Name = $"AdditionalUser{i}",
                    ApiKey = Guid.NewGuid().ToString(),
                    SecurityStamp = Guid.NewGuid().ToString()
                };
                var createdAdditionalUser = await userRepo.CreateAsync(additionalUser);

                var additionalOrgUser = new OrganizationUser
                {
                    OrganizationId = organization.Id,
                    UserId = createdAdditionalUser.Id,
                    Status = OrganizationUserStatusType.Confirmed,
                    Type = OrganizationUserType.User
                };
                additionalOrgUsers.Add(additionalOrgUser);
            }
            await orgUserRepo.CreateManyAsync(additionalOrgUsers);
        }

        // 5. Create SsoConfig, if ssoConfigConfig is not null
        SsoConfig? ssoConfig = null;
        if (_includeSsoConfig)
        {
            ssoConfig = new SsoConfig
            {
                OrganizationId = authOrganizationId,
                Enabled = true
            };
            ssoConfig.SetData(new SsoConfigurationData());
            _ssoConfigConfig?.Invoke(ssoConfig);

            var ssoConfigRepo = factory.Services.GetRequiredService<ISsoConfigRepository>();
            ssoConfig = await ssoConfigRepo.CreateAsync(ssoConfig);
        }

        // 6. Optionally create SsoUser (using pre-generated providerUserId as ExternalId)
        SsoUser? ssoUser = null;
        if (_includeSsoUser)
        {
            ssoUser = new SsoUser
            {
                OrganizationId = organization.Id,
                UserId = user!.Id,
                ExternalId = providerUserId
            };
            _ssoUserConfig?.Invoke(ssoUser);

            var ssoUserRepo = factory.Services.GetRequiredService<ISsoUserRepository>();
            ssoUser = await ssoUserRepo.CreateAsync(ssoUser);
        }

        return new SsoTestData(factory, organization, user, orgUser, ssoConfig, ssoUser);
    }
}
