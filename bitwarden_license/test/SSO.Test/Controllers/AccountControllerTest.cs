using System.Reflection;
using System.Security.Claims;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.UserFeatures.Registration;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Sso.Controllers;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit.Abstractions;
using AuthenticationOptions = Duende.IdentityServer.Configuration.AuthenticationOptions;

namespace Bit.SSO.Test.Controllers;

[ControllerCustomize(typeof(AccountController)), SutProviderCustomize]
public class AccountControllerTest
{
    private readonly ITestOutputHelper _output;

    public AccountControllerTest(ITestOutputHelper output)
    {
        _output = output;
    }

    private static IAuthenticationService SetupHttpContextWithAuth(
        SutProvider<AccountController> sutProvider,
        AuthenticateResult authResult,
        IAuthenticationService? authService = null)
    {
        var schemeProvider = Substitute.For<IAuthenticationSchemeProvider>();
        schemeProvider.GetDefaultAuthenticateSchemeAsync()
            .Returns(new AuthenticationScheme("idsrv", "idsrv", typeof(IAuthenticationHandler)));

        var resolvedAuthService = authService ?? Substitute.For<IAuthenticationService>();
        resolvedAuthService.AuthenticateAsync(
                Arg.Any<HttpContext>(),
                AuthenticationSchemes.BitwardenExternalCookieAuthenticationScheme)
            .Returns(authResult);

        var services = new ServiceCollection();
        services.AddSingleton(resolvedAuthService);
        services.AddSingleton<IAuthenticationSchemeProvider>(schemeProvider);
        services.AddSingleton(new IdentityServerOptions
        {
            Authentication = new AuthenticationOptions
            {
                CookieAuthenticationScheme = "idsrv"
            }
        });
        var sp = services.BuildServiceProvider();

        sutProvider.Sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                RequestServices = sp
            }
        };

        return resolvedAuthService;
    }

    private static AuthenticateResult BuildSuccessfulExternalAuth(Guid orgId, string providerUserId, string email)
    {
        var claims = new[]
        {
            new Claim(JwtClaimTypes.Subject, providerUserId),
            new Claim(JwtClaimTypes.Email, email)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "External"));
        var properties = new AuthenticationProperties
        {
            Items =
            {
                ["scheme"] = orgId.ToString(),
                ["return_url"] = "~/",
                ["state"] = "state",
                ["user_identifier"] = string.Empty
            }
        };
        var ticket = new AuthenticationTicket(principal, properties, AuthenticationSchemes.BitwardenExternalCookieAuthenticationScheme);
        return AuthenticateResult.Success(ticket);
    }

    private static void ConfigureSsoAndUser(
        SutProvider<AccountController> sutProvider,
        Guid orgId,
        string providerUserId,
        User user,
        Organization? organization = null,
        OrganizationUser? orgUser = null)
    {
        var ssoConfigRepository = sutProvider.GetDependency<ISsoConfigRepository>();
        var userRepository = sutProvider.GetDependency<IUserRepository>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

        var ssoConfig = new SsoConfig { OrganizationId = orgId, Enabled = true };
        var ssoData = new SsoConfigurationData();
        ssoConfig.SetData(ssoData);
        ssoConfigRepository.GetByOrganizationIdAsync(orgId).Returns(ssoConfig);

        userRepository.GetBySsoUserAsync(providerUserId, orgId).Returns(user);

        if (organization != null)
        {
            organizationRepository.GetByIdAsync(orgId).Returns(organization);
        }
        if (organization != null && orgUser != null)
        {
            organizationUserRepository.GetByOrganizationAsync(organization.Id, user.Id).Returns(orgUser);
            organizationUserRepository.GetManyByUserAsync(user.Id).Returns([orgUser]);
        }
    }

    private enum MeasurementScenario
    {
        ExistingSsoLinkedAccepted,
        ExistingUserNoOrgUser,
        JitProvision
    }

    private sealed class LookupCounts
    {
        public int UserGetBySso { get; init; }
        public int UserGetByEmail { get; init; }
        public int OrgGetById { get; init; }
        public int OrgUserGetByOrg { get; init; }
        public int OrgUserGetByEmail { get; init; }
    }

    private async Task<LookupCounts> MeasureCountsForScenarioAsync(
        SutProvider<AccountController> sutProvider,
        MeasurementScenario scenario,
        bool preventNonCompliant)
    {
        var orgId = Guid.NewGuid();
        var providerUserId = $"meas-{scenario}-{(preventNonCompliant ? "on" : "off")}";
        var email = scenario == MeasurementScenario.JitProvision
            ? "jit.compare@example.com"
            : "existing.compare@example.com";

        var organization = new Organization { Id = orgId, Name = "Org" };
        var user = new User { Id = Guid.NewGuid(), Email = email };

        var authResult = BuildSuccessfulExternalAuth(orgId, providerUserId, email);
        SetupHttpContextWithAuth(sutProvider, authResult);

        // SSO config present
        var ssoConfigRepository = sutProvider.GetDependency<ISsoConfigRepository>();
        var userRepository = sutProvider.GetDependency<IUserRepository>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var featureService = sutProvider.GetDependency<IFeatureService>();
        var interactionService = sutProvider.GetDependency<IIdentityServerInteractionService>();

        var ssoConfig = new SsoConfig { OrganizationId = orgId, Enabled = true };
        var ssoData = new SsoConfigurationData();
        ssoConfig.SetData(ssoData);
        ssoConfigRepository.GetByOrganizationIdAsync(orgId).Returns(ssoConfig);

        switch (scenario)
        {
            case MeasurementScenario.ExistingSsoLinkedAccepted:
                userRepository.GetBySsoUserAsync(providerUserId, orgId).Returns(user);
                organizationRepository.GetByIdAsync(orgId).Returns(organization);
                organizationUserRepository.GetByOrganizationAsync(organization.Id, user.Id)
                    .Returns(new OrganizationUser
                    {
                        OrganizationId = orgId,
                        UserId = user.Id,
                        Status = OrganizationUserStatusType.Accepted,
                        Type = OrganizationUserType.User
                    });
                break;
            case MeasurementScenario.ExistingUserNoOrgUser:
                userRepository.GetBySsoUserAsync(providerUserId, orgId).Returns(user);
                organizationRepository.GetByIdAsync(orgId).Returns(organization);
                organizationUserRepository.GetByOrganizationAsync(organization.Id, user.Id)
                    .Returns((OrganizationUser?)null);
                break;
            case MeasurementScenario.JitProvision:
                userRepository.GetBySsoUserAsync(providerUserId, orgId).Returns((User?)null);
                userRepository.GetByEmailAsync(email).Returns((User?)null);
                organizationRepository.GetByIdAsync(orgId).Returns(organization);
                organizationUserRepository.GetByOrganizationEmailAsync(orgId, email)
                    .Returns((OrganizationUser?)null);
                break;
        }

        featureService.IsEnabled(Arg.Any<string>()).Returns(preventNonCompliant);
        interactionService.GetAuthorizationContextAsync("~/").Returns((AuthorizationRequest?)null);

        try
        {
            _ = await sutProvider.Sut.ExternalCallback();
        }
        catch
        {
            // Ignore exceptions for measurement; some flows can throw based on status enforcement
        }

        var counts = new LookupCounts
        {
            UserGetBySso = userRepository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IUserRepository.GetBySsoUserAsync)),
            UserGetByEmail = userRepository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IUserRepository.GetByEmailAsync)),
            OrgGetById = organizationRepository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IOrganizationRepository.GetByIdAsync)),
            OrgUserGetByOrg = organizationUserRepository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IOrganizationUserRepository.GetByOrganizationAsync)),
            OrgUserGetByEmail = organizationUserRepository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IOrganizationUserRepository.GetByOrganizationEmailAsync)),
        };

        userRepository.ClearReceivedCalls();
        organizationRepository.ClearReceivedCalls();
        organizationUserRepository.ClearReceivedCalls();

        return counts;
    }

    [Theory, BitAutoData]
    public async Task ExternalCallback_PreventNonCompliantTrue_ExistingUser_NoOrgUser_ThrowsCouldNotFindOrganizationUser(
        SutProvider<AccountController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var providerUserId = "ext-missing-orguser";
        var user = new User { Id = Guid.NewGuid(), Email = "missing.orguser@example.com" };
        var organization = new Organization { Id = orgId, Name = "Org" };

        var authResult = BuildSuccessfulExternalAuth(orgId, providerUserId, user.Email!);
        SetupHttpContextWithAuth(sutProvider, authResult);

        // i18n returns the key so we can assert on message contents
        sutProvider.GetDependency<II18nService>()
            .T(Arg.Any<string>(), Arg.Any<object?[]>())
            .Returns(ci => (string)ci[0]!);

        // SSO config + user link exists, but no org user membership
        ConfigureSsoAndUser(
            sutProvider,
            orgId,
            providerUserId,
            user,
            organization,
            orgUser: null);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organization.Id, user.Id).Returns((OrganizationUser?)null);

        sutProvider.GetDependency<IFeatureService>().IsEnabled(Arg.Any<string>()).Returns(true);
        sutProvider.GetDependency<IIdentityServerInteractionService>()
            .GetAuthorizationContextAsync("~/").Returns((AuthorizationRequest?)null);

        // Act + Assert
        var ex = await Assert.ThrowsAsync<Exception>(() => sutProvider.Sut.ExternalCallback());
        Assert.Equal("CouldNotFindOrganizationUser", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task ExternalCallback_PreventNonCompliantTrue_ExistingUser_OrgUserInvited_AllowsLogin(
        SutProvider<AccountController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var providerUserId = "ext-invited-orguser";
        var user = new User { Id = Guid.NewGuid(), Email = "invited.orguser@example.com" };
        var organization = new Organization { Id = orgId, Name = "Org" };
        var orgUser = new OrganizationUser
        {
            OrganizationId = orgId,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Invited,
            Type = OrganizationUserType.User
        };

        var authResult = BuildSuccessfulExternalAuth(orgId, providerUserId, user.Email!);
        var authService = SetupHttpContextWithAuth(sutProvider, authResult);

        sutProvider.GetDependency<II18nService>()
            .T(Arg.Any<string>(), Arg.Any<object?[]>())
            .Returns(ci => (string)ci[0]!);

        ConfigureSsoAndUser(
            sutProvider,
            orgId,
            providerUserId,
            user,
            organization,
            orgUser);

        sutProvider.GetDependency<IFeatureService>().IsEnabled(Arg.Any<string>()).Returns(true);
        sutProvider.GetDependency<IIdentityServerInteractionService>()
            .GetAuthorizationContextAsync("~/").Returns((AuthorizationRequest?)null);

        // Act
        var result = await sutProvider.Sut.ExternalCallback();

        // Assert
        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("~/", redirect.Url);

        await authService.Received().SignInAsync(
            Arg.Any<HttpContext>(),
            Arg.Any<string?>(),
            Arg.Any<ClaimsPrincipal>(),
            Arg.Any<AuthenticationProperties>());

        await authService.Received().SignOutAsync(
            Arg.Any<HttpContext>(),
            AuthenticationSchemes.BitwardenExternalCookieAuthenticationScheme,
            Arg.Any<AuthenticationProperties>());
    }

    [Theory, BitAutoData]
    public async Task ExternalCallback_PreventNonCompliantTrue_ExistingUser_OrgUserRevoked_ThrowsAccessRevoked(
        SutProvider<AccountController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var providerUserId = "ext-revoked-orguser";
        var user = new User { Id = Guid.NewGuid(), Email = "revoked.orguser@example.com" };
        var organization = new Organization { Id = orgId, Name = "Org" };
        var orgUser = new OrganizationUser
        {
            OrganizationId = orgId,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Revoked,
            Type = OrganizationUserType.User
        };

        var authResult = BuildSuccessfulExternalAuth(orgId, providerUserId, user.Email!);
        SetupHttpContextWithAuth(sutProvider, authResult);

        sutProvider.GetDependency<II18nService>()
            .T(Arg.Any<string>(), Arg.Any<object?[]>())
            .Returns(ci => (string)ci[0]!);

        ConfigureSsoAndUser(
            sutProvider,
            orgId,
            providerUserId,
            user,
            organization,
            orgUser);

        sutProvider.GetDependency<IFeatureService>().IsEnabled(Arg.Any<string>()).Returns(true);
        sutProvider.GetDependency<IIdentityServerInteractionService>()
            .GetAuthorizationContextAsync("~/").Returns((AuthorizationRequest?)null);

        // Act + Assert
        var ex = await Assert.ThrowsAsync<Exception>(() => sutProvider.Sut.ExternalCallback());
        Assert.Equal("OrganizationUserAccessRevoked", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task ExternalCallback_PreventNonCompliantTrue_ExistingUser_OrgUserUnknown_ThrowsUnknown(
        SutProvider<AccountController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var providerUserId = "ext-unknown-orguser";
        var user = new User { Id = Guid.NewGuid(), Email = "unknown.orguser@example.com" };
        var organization = new Organization { Id = orgId, Name = "Org" };
        var unknownStatus = (OrganizationUserStatusType)999;
        var orgUser = new OrganizationUser
        {
            OrganizationId = orgId,
            UserId = user.Id,
            Status = unknownStatus,
            Type = OrganizationUserType.User
        };

        var authResult = BuildSuccessfulExternalAuth(orgId, providerUserId, user.Email!);
        SetupHttpContextWithAuth(sutProvider, authResult);

        sutProvider.GetDependency<II18nService>()
            .T(Arg.Any<string>(), Arg.Any<object?[]>())
            .Returns(ci => (string)ci[0]!);

        ConfigureSsoAndUser(
            sutProvider,
            orgId,
            providerUserId,
            user,
            organization,
            orgUser);

        sutProvider.GetDependency<IFeatureService>().IsEnabled(Arg.Any<string>()).Returns(true);
        sutProvider.GetDependency<IIdentityServerInteractionService>()
            .GetAuthorizationContextAsync("~/").Returns((AuthorizationRequest?)null);

        // Act + Assert
        var ex = await Assert.ThrowsAsync<Exception>(() => sutProvider.Sut.ExternalCallback());
        Assert.Equal("OrganizationUserUnknownStatus", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task ExternalCallback_WithExistingUserAndAcceptedMembership_RedirectsToReturnUrl(
        SutProvider<AccountController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var providerUserId = "ext-123";
        var user = new User { Id = Guid.NewGuid(), Email = "user@example.com" };
        var organization = new Organization { Id = orgId, Name = "Test Org" };
        var orgUser = new OrganizationUser
        {
            OrganizationId = orgId,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Accepted,
            Type = OrganizationUserType.User
        };

        var authResult = BuildSuccessfulExternalAuth(orgId, providerUserId, user.Email!);
        var authService = SetupHttpContextWithAuth(sutProvider, authResult);

        ConfigureSsoAndUser(
            sutProvider,
            orgId,
            providerUserId,
            user,
            organization,
            orgUser);

        sutProvider.GetDependency<IFeatureService>().IsEnabled(Arg.Any<string>()).Returns(true);
        sutProvider.GetDependency<IIdentityServerInteractionService>()
            .GetAuthorizationContextAsync("~/").Returns((AuthorizationRequest?)null);

        // Act
        var result = await sutProvider.Sut.ExternalCallback();

        // Assert
        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("~/", redirect.Url);

        await authService.Received().SignInAsync(
            Arg.Any<HttpContext>(),
            Arg.Any<string?>(),
            Arg.Any<ClaimsPrincipal>(),
            Arg.Any<AuthenticationProperties>());

        await authService.Received().SignOutAsync(
            Arg.Any<HttpContext>(),
            AuthenticationSchemes.BitwardenExternalCookieAuthenticationScheme,
            Arg.Any<AuthenticationProperties>());
    }

    /// <summary>
    /// PM-24579: Temporary test, remove with feature flag.
    /// </summary>
    [Theory, BitAutoData]
    public async Task ExternalCallback_PreventNonCompliantFalse_SkipsOrgLookupAndSignsIn(
        SutProvider<AccountController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var providerUserId = "ext-flag-off";
        var user = new User { Id = Guid.NewGuid(), Email = "flagoff@example.com" };

        var authResult = BuildSuccessfulExternalAuth(orgId, providerUserId, user.Email!);
        var authService = SetupHttpContextWithAuth(sutProvider, authResult);

        ConfigureSsoAndUser(
            sutProvider,
            orgId,
            providerUserId,
            user);

        sutProvider.GetDependency<IFeatureService>().IsEnabled(Arg.Any<string>()).Returns(false);
        sutProvider.GetDependency<IIdentityServerInteractionService>()
            .GetAuthorizationContextAsync("~/").Returns((AuthorizationRequest?)null);

        // Act
        var result = await sutProvider.Sut.ExternalCallback();

        // Assert
        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("~/", redirect.Url);

        await authService.Received().SignInAsync(
            Arg.Any<HttpContext>(),
            Arg.Any<string?>(),
            Arg.Any<ClaimsPrincipal>(),
            Arg.Any<AuthenticationProperties>());

        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .GetByOrganizationAsync(Guid.Empty, Guid.Empty);
    }

    /// <summary>
    /// PM-24579: Permanent test, remove the True in PreventNonCompliantTrue and remove the configure for the feature
    /// flag.
    /// </summary>
    [Theory, BitAutoData]
    public async Task ExternalCallback_PreventNonCompliantTrue_ExistingSsoLinkedAccepted_MeasureLookups(
        SutProvider<AccountController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var providerUserId = "ext-measure-existing";
        var user = new User { Id = Guid.NewGuid(), Email = "existing@example.com" };
        var organization = new Organization { Id = orgId, Name = "Org" };
        var orgUser = new OrganizationUser
        {
            OrganizationId = orgId,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Accepted,
            Type = OrganizationUserType.User
        };

        var authResult = BuildSuccessfulExternalAuth(orgId, providerUserId, user.Email);
        SetupHttpContextWithAuth(sutProvider, authResult);

        ConfigureSsoAndUser(
            sutProvider,
            orgId,
            providerUserId,
            user,
            organization,
            orgUser);

        sutProvider.GetDependency<IFeatureService>().IsEnabled(Arg.Any<string>()).Returns(true);
        sutProvider.GetDependency<IIdentityServerInteractionService>()
            .GetAuthorizationContextAsync("~/").Returns((AuthorizationRequest?)null);

        // Act
        try
        {
            _ = await sutProvider.Sut.ExternalCallback();
        }
        catch
        {
            // ignore for measurement only
        }

        // Assert (measurement only - no asserts on counts)
        var userRepository = sutProvider.GetDependency<IUserRepository>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

        var userGetBySso = userRepository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IUserRepository.GetBySsoUserAsync));
        var userGetByEmail = userRepository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IUserRepository.GetByEmailAsync));
        var orgGet = organizationRepository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IOrganizationRepository.GetByIdAsync));
        var orgUserGetByOrg = organizationUserRepository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IOrganizationUserRepository.GetByOrganizationAsync))
            + organizationUserRepository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IOrganizationUserRepository.GetManyByUserAsync));
        var orgUserGetByEmail = organizationUserRepository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IOrganizationUserRepository.GetByOrganizationEmailAsync));

        _output.WriteLine($"GetBySsoUserAsync: {userGetBySso}");
        _output.WriteLine($"GetByEmailAsync: {userGetByEmail}");
        _output.WriteLine($"GetByIdAsync (Org): {orgGet}");
        _output.WriteLine($"GetByOrganizationAsync (OrgUser): {orgUserGetByOrg}");
        _output.WriteLine($"GetByOrganizationEmailAsync (OrgUser): {orgUserGetByEmail}");

        // Snapshot assertions
        Assert.Equal(1, userGetBySso);
        Assert.Equal(0, userGetByEmail);
        Assert.Equal(1, orgGet);
        Assert.Equal(1, orgUserGetByOrg);
        Assert.Equal(0, orgUserGetByEmail);
    }

    /// <summary>
    /// PM-24579: Permanent test, remove the True in PreventNonCompliantTrue and remove the configure for the feature
    /// flag.
    /// </summary>
    [Theory, BitAutoData]
    public async Task ExternalCallback_PreventNonCompliantTrue_JitProvision_MeasureLookups(
        SutProvider<AccountController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var providerUserId = "ext-measure-jit";
        var email = "jit.measure@example.com";
        var organization = new Organization { Id = orgId, Name = "Org", Seats = null };

        var authResult = BuildSuccessfulExternalAuth(orgId, providerUserId, email);
        SetupHttpContextWithAuth(sutProvider, authResult);

        var ssoConfigRepository = sutProvider.GetDependency<ISsoConfigRepository>();
        var userRepository = sutProvider.GetDependency<IUserRepository>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

        var ssoConfig = new SsoConfig { OrganizationId = orgId, Enabled = true };
        var ssoData = new SsoConfigurationData();
        ssoConfig.SetData(ssoData);
        ssoConfigRepository.GetByOrganizationIdAsync(orgId).Returns(ssoConfig);

        // JIT (no existing user or sso link)
        userRepository.GetBySsoUserAsync(providerUserId, orgId).Returns((User?)null);
        userRepository.GetByEmailAsync(email).Returns((User?)null);
        organizationRepository.GetByIdAsync(orgId).Returns(organization);
        organizationUserRepository.GetByOrganizationEmailAsync(orgId, email).Returns((OrganizationUser?)null);

        sutProvider.GetDependency<IFeatureService>().IsEnabled(Arg.Any<string>()).Returns(true);
        sutProvider.GetDependency<IIdentityServerInteractionService>()
            .GetAuthorizationContextAsync("~/").Returns((AuthorizationRequest?)null);

        // Act
        try
        {
            _ = await sutProvider.Sut.ExternalCallback();
        }
        catch
        {
            // JIT path may throw due to Invited status under enforcement; ignore for measurement
        }

        // Assert (measurement only - no asserts on counts)
        var userGetBySso = userRepository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IUserRepository.GetBySsoUserAsync));
        var userGetByEmail = userRepository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IUserRepository.GetByEmailAsync));
        var orgGet = organizationRepository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IOrganizationRepository.GetByIdAsync));
        var orgUserGetByOrg = organizationUserRepository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IOrganizationUserRepository.GetByOrganizationAsync));
        var orgUserGetByEmail = organizationUserRepository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IOrganizationUserRepository.GetByOrganizationEmailAsync));

        _output.WriteLine($"GetBySsoUserAsync: {userGetBySso}");
        _output.WriteLine($"GetByEmailAsync: {userGetByEmail}");
        _output.WriteLine($"GetByIdAsync (Org): {orgGet}");
        _output.WriteLine($"GetByOrganizationAsync (OrgUser): {orgUserGetByOrg}");
        _output.WriteLine($"GetByOrganizationEmailAsync (OrgUser): {orgUserGetByEmail}");

        // Snapshot assertions
        Assert.Equal(1, userGetBySso);
        Assert.Equal(1, userGetByEmail);
        Assert.Equal(1, orgGet);
        Assert.Equal(0, orgUserGetByOrg);
        Assert.Equal(1, orgUserGetByEmail);
    }

    /// <summary>
    /// PM-24579: Permanent test, remove the True in PreventNonCompliantTrue and remove the configure for the feature
    /// flag.
    ///
    /// This test will trigger both the GetByOrganizationAsync and the fallback attempt to get by email
    /// GetByOrganizationEmailAsync.
    /// </summary>
    [Theory, BitAutoData]
    public async Task ExternalCallback_PreventNonCompliantTrue_ExistingUser_NoOrgUser_MeasureLookups(
        SutProvider<AccountController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var providerUserId = "ext-measure-existing-no-orguser";
        var user = new User { Id = Guid.NewGuid(), Email = "existing2@example.com" };
        var organization = new Organization { Id = orgId, Name = "Org" };

        var authResult = BuildSuccessfulExternalAuth(orgId, providerUserId, user.Email!);
        SetupHttpContextWithAuth(sutProvider, authResult);

        ConfigureSsoAndUser(
            sutProvider,
            orgId,
            providerUserId,
            user,
            organization,
            orgUser: null);

        // Ensure orgUser lookup returns null
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organization.Id, user.Id).Returns((OrganizationUser?)null);

        sutProvider.GetDependency<IFeatureService>().IsEnabled(Arg.Any<string>()).Returns(true);
        sutProvider.GetDependency<IIdentityServerInteractionService>()
            .GetAuthorizationContextAsync("~/").Returns((AuthorizationRequest?)null);

        // Act
        try
        {
            _ = await sutProvider.Sut.ExternalCallback();
        }
        catch
        {
            // ignore for measurement only
        }

        // Assert (measurement only - no asserts on counts)
        var userRepository = sutProvider.GetDependency<IUserRepository>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

        var userGetBySso = userRepository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IUserRepository.GetBySsoUserAsync));
        var userGetByEmail = userRepository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IUserRepository.GetByEmailAsync));
        var orgGet = organizationRepository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IOrganizationRepository.GetByIdAsync));
        var orgUserGetByOrg = organizationUserRepository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IOrganizationUserRepository.GetByOrganizationAsync))
            + organizationUserRepository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IOrganizationUserRepository.GetManyByUserAsync));
        var orgUserGetByEmail = organizationUserRepository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IOrganizationUserRepository.GetByOrganizationEmailAsync));

        _output.WriteLine($"GetBySsoUserAsync: {userGetBySso}");
        _output.WriteLine($"GetByEmailAsync: {userGetByEmail}");
        _output.WriteLine($"GetByIdAsync (Org): {orgGet}");
        _output.WriteLine($"GetByOrganizationAsync (OrgUser): {orgUserGetByOrg}");
        _output.WriteLine($"GetByOrganizationEmailAsync (OrgUser): {orgUserGetByEmail}");

        // Snapshot assertions
        Assert.Equal(1, userGetBySso);
        Assert.Equal(0, userGetByEmail);
        Assert.Equal(1, orgGet);
        Assert.Equal(1, orgUserGetByOrg);
        Assert.Equal(1, orgUserGetByEmail);
    }

    /// <summary>
    /// PM-24579: Temporary test, remove with feature flag.
    /// </summary>
    [Theory, BitAutoData]
    public async Task ExternalCallback_PreventNonCompliantFalse_ExistingSsoLinkedAccepted_MeasureLookups(
        SutProvider<AccountController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var providerUserId = "ext-measure-existing-flagoff";
        var user = new User { Id = Guid.NewGuid(), Email = "existing.flagoff@example.com" };

        var authResult = BuildSuccessfulExternalAuth(orgId, providerUserId, user.Email!);
        SetupHttpContextWithAuth(sutProvider, authResult);

        var ssoConfig = new SsoConfig { OrganizationId = orgId, Enabled = true };
        var ssoData = new SsoConfigurationData();
        ssoConfig.SetData(ssoData);
        sutProvider.GetDependency<ISsoConfigRepository>().GetByOrganizationIdAsync(orgId).Returns(ssoConfig);
        sutProvider.GetDependency<IUserRepository>().GetBySsoUserAsync(providerUserId, orgId).Returns(user);

        sutProvider.GetDependency<IFeatureService>().IsEnabled(Arg.Any<string>()).Returns(false);
        sutProvider.GetDependency<IIdentityServerInteractionService>()
            .GetAuthorizationContextAsync("~/").Returns((AuthorizationRequest?)null);

        // Act
        try { _ = await sutProvider.Sut.ExternalCallback(); } catch { }

        // Assert (measurement)
        var userRepository = sutProvider.GetDependency<IUserRepository>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

        var userGetBySso = userRepository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IUserRepository.GetBySsoUserAsync));
        var userGetByEmail = userRepository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IUserRepository.GetByEmailAsync));
        var orgGet = organizationRepository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IOrganizationRepository.GetByIdAsync));
        var orgUserGetByOrg = organizationUserRepository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IOrganizationUserRepository.GetByOrganizationAsync));
        var orgUserGetByEmail = organizationUserRepository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IOrganizationUserRepository.GetByOrganizationEmailAsync));

        _output.WriteLine($"[flag off] GetBySsoUserAsync: {userGetBySso}");
        _output.WriteLine($"[flag off] GetByEmailAsync: {userGetByEmail}");
        _output.WriteLine($"[flag off] GetByIdAsync (Org): {orgGet}");
        _output.WriteLine($"[flag off] GetByOrganizationAsync (OrgUser): {orgUserGetByOrg}");
        _output.WriteLine($"[flag off] GetByOrganizationEmailAsync (OrgUser): {orgUserGetByEmail}");
    }

    /// <summary>
    /// PM-24579: Temporary test, remove with feature flag.
    /// </summary>
    [Theory, BitAutoData]
    public async Task ExternalCallback_PreventNonCompliantFalse_ExistingUser_NoOrgUser_MeasureLookups(
        SutProvider<AccountController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var providerUserId = "ext-measure-existing-no-orguser-flagoff";
        var user = new User { Id = Guid.NewGuid(), Email = "existing2.flagoff@example.com" };

        var authResult = BuildSuccessfulExternalAuth(orgId, providerUserId, user.Email!);
        SetupHttpContextWithAuth(sutProvider, authResult);

        var ssoConfig = new SsoConfig { OrganizationId = orgId, Enabled = true };
        var ssoData = new SsoConfigurationData();
        ssoConfig.SetData(ssoData);
        sutProvider.GetDependency<ISsoConfigRepository>().GetByOrganizationIdAsync(orgId).Returns(ssoConfig);
        sutProvider.GetDependency<IUserRepository>().GetBySsoUserAsync(providerUserId, orgId).Returns(user);

        sutProvider.GetDependency<IFeatureService>().IsEnabled(Arg.Any<string>()).Returns(false);
        sutProvider.GetDependency<IIdentityServerInteractionService>()
            .GetAuthorizationContextAsync("~/").Returns((AuthorizationRequest?)null);

        // Act
        try { _ = await sutProvider.Sut.ExternalCallback(); } catch { }

        // Assert (measurement)
        var userRepository = sutProvider.GetDependency<IUserRepository>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

        var userGetBySso = userRepository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IUserRepository.GetBySsoUserAsync));
        var userGetByEmail = userRepository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IUserRepository.GetByEmailAsync));
        var orgGet = organizationRepository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IOrganizationRepository.GetByIdAsync));
        var orgUserGetByOrg = organizationUserRepository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IOrganizationUserRepository.GetByOrganizationAsync));
        var orgUserGetByEmail = organizationUserRepository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IOrganizationUserRepository.GetByOrganizationEmailAsync));

        _output.WriteLine($"[flag off] GetBySsoUserAsync: {userGetBySso}");
        _output.WriteLine($"[flag off] GetByEmailAsync: {userGetByEmail}");
        _output.WriteLine($"[flag off] GetByIdAsync (Org): {orgGet}");
        _output.WriteLine($"[flag off] GetByOrganizationAsync (OrgUser): {orgUserGetByOrg}");
        _output.WriteLine($"[flag off] GetByOrganizationEmailAsync (OrgUser): {orgUserGetByEmail}");
    }

    /// <summary>
    /// PM-24579: Temporary test, remove with feature flag.
    /// </summary>
    [Theory, BitAutoData]
    public async Task ExternalCallback_PreventNonCompliantFalse_JitProvision_MeasureLookups(
        SutProvider<AccountController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var providerUserId = "ext-measure-jit-flagoff";
        var email = "jit.flagoff@example.com";
        var organization = new Organization { Id = orgId, Name = "Org", Seats = null };

        var authResult = BuildSuccessfulExternalAuth(orgId, providerUserId, email);
        SetupHttpContextWithAuth(sutProvider, authResult);

        var ssoConfig = new SsoConfig { OrganizationId = orgId, Enabled = true };
        var ssoData = new SsoConfigurationData();
        ssoConfig.SetData(ssoData);
        sutProvider.GetDependency<ISsoConfigRepository>().GetByOrganizationIdAsync(orgId).Returns(ssoConfig);

        // JIT (no existing user or sso link)
        sutProvider.GetDependency<IUserRepository>().GetBySsoUserAsync(providerUserId, orgId).Returns((User?)null);
        sutProvider.GetDependency<IUserRepository>().GetByEmailAsync(email).Returns((User?)null);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(orgId).Returns(organization);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByOrganizationEmailAsync(orgId, email).Returns((OrganizationUser?)null);

        sutProvider.GetDependency<IFeatureService>().IsEnabled(Arg.Any<string>()).Returns(false);
        sutProvider.GetDependency<IIdentityServerInteractionService>()
            .GetAuthorizationContextAsync("~/").Returns((AuthorizationRequest?)null);

        // Act
        try { _ = await sutProvider.Sut.ExternalCallback(); } catch { }

        // Assert (measurement)
        var userRepository = sutProvider.GetDependency<IUserRepository>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

        var userGetBySso = userRepository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IUserRepository.GetBySsoUserAsync));
        var userGetByEmail = userRepository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IUserRepository.GetByEmailAsync));
        var orgGet = organizationRepository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IOrganizationRepository.GetByIdAsync));
        var orgUserGetByOrg = organizationUserRepository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IOrganizationUserRepository.GetByOrganizationAsync));
        var orgUserGetByEmail = organizationUserRepository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IOrganizationUserRepository.GetByOrganizationEmailAsync));

        _output.WriteLine($"[flag off] GetBySsoUserAsync: {userGetBySso}");
        _output.WriteLine($"[flag off] GetByEmailAsync: {userGetByEmail}");
        _output.WriteLine($"[flag off] GetByIdAsync (Org): {orgGet}");
        _output.WriteLine($"[flag off] GetByOrganizationAsync (OrgUser): {orgUserGetByOrg}");
        _output.WriteLine($"[flag off] GetByOrganizationEmailAsync (OrgUser): {orgUserGetByEmail}");
    }

    [Theory, BitAutoData]
    public async Task CreateUserAndOrgUserConditionallyAsync_WithExistingAcceptedUser_CreatesSsoLinkAndReturnsUser(
        SutProvider<AccountController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var providerUserId = "provider-user-id";
        var email = "user@example.com";
        var existingUser = new User { Id = Guid.NewGuid(), Email = email };
        var organization = new Organization { Id = orgId, Name = "Org" };
        var orgUser = new OrganizationUser
        {
            OrganizationId = orgId,
            UserId = existingUser.Id,
            Status = OrganizationUserStatusType.Accepted,
            Type = OrganizationUserType.User
        };

        // Arrange repository expectations for the flow
        sutProvider.GetDependency<IUserRepository>().GetByEmailAsync(email).Returns(existingUser);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(orgId).Returns(organization);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyByUserAsync(existingUser.Id)
            .Returns(new List<OrganizationUser> { orgUser });
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByOrganizationEmailAsync(orgId, email).Returns(orgUser);

        // No existing SSO link so first SSO login event is logged
        sutProvider.GetDependency<ISsoUserRepository>().GetByUserIdOrganizationIdAsync(orgId, existingUser.Id).Returns((SsoUser?)null);

        var claims = new[]
        {
            new Claim(JwtClaimTypes.Email, email),
            new Claim(JwtClaimTypes.Name, "Jit User")
        } as IEnumerable<Claim>;
        var config = new SsoConfigurationData();

        var method = typeof(AccountController).GetMethod(
            "CreateUserAndOrgUserConditionallyAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        // Act
        var task = (Task<(User user, Organization organization, OrganizationUser orgUser)>)method.Invoke(sutProvider.Sut, new object[]
        {
            orgId.ToString(),
            providerUserId,
            claims,
            null!,
            config
        })!;

        var returned = await task;

        // Assert
        Assert.Equal(existingUser.Id, returned.user.Id);

        await sutProvider.GetDependency<ISsoUserRepository>().Received().CreateAsync(Arg.Is<SsoUser>(s =>
            s.OrganizationId == orgId && s.UserId == existingUser.Id && s.ExternalId == providerUserId));

        await sutProvider.GetDependency<Core.Services.IEventService>().Received().LogOrganizationUserEventAsync(
            orgUser,
            EventType.OrganizationUser_FirstSsoLogin);
    }

    [Theory, BitAutoData]
    public async Task CreateUserAndOrgUserConditionallyAsync_WithExistingInvitedUser_ThrowsAcceptInviteBeforeUsingSSO(
        SutProvider<AccountController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var providerUserId = "provider-user-id";
        var email = "user@example.com";
        var existingUser = new User { Id = Guid.NewGuid(), Email = email, UsesKeyConnector = false };
        var organization = new Organization { Id = orgId, Name = "Org" };
        var orgUser = new OrganizationUser
        {
            OrganizationId = orgId,
            UserId = existingUser.Id,
            Status = OrganizationUserStatusType.Invited,
            Type = OrganizationUserType.User
        };

        // i18n returns the key so we can assert on message contents
        sutProvider.GetDependency<II18nService>()
            .T(Arg.Any<string>(), Arg.Any<object?[]>())
            .Returns(ci => (string)ci[0]!);

        // Arrange repository expectations for the flow
        sutProvider.GetDependency<IUserRepository>().GetByEmailAsync(email).Returns(existingUser);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(orgId).Returns(organization);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyByUserAsync(existingUser.Id)
            .Returns(new List<OrganizationUser> { orgUser });

        var claims = new[]
        {
            new Claim(JwtClaimTypes.Email, email),
            new Claim(JwtClaimTypes.Name, "Invited User")
        } as IEnumerable<Claim>;
        var config = new SsoConfigurationData();

        var method = typeof(AccountController).GetMethod(
            "CreateUserAndOrgUserConditionallyAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        // Act + Assert
        var task = (Task<(User user, Organization organization, OrganizationUser orgUser)>)method.Invoke(sutProvider.Sut, new object[]
        {
            orgId.ToString(),
            providerUserId,
            claims,
            null!,
            config
        })!;

        var ex = await Assert.ThrowsAsync<Exception>(async () => await task);
        Assert.Equal("AcceptInviteBeforeUsingSSO", ex.Message);
    }

    /// <summary>
    /// PM-24579: Temporary comparison test to ensure the feature flag ON does not
    /// regress lookup counts compared to OFF. When removing the flag, delete this
    /// comparison test and keep the specific scenario snapshot tests if desired.
    /// </summary>
    [Theory, BitAutoData]
    public async Task ExternalCallback_Measurements_FlagOnVsOff_Comparisons(
        SutProvider<AccountController> sutProvider)
    {
        // Arrange
        var scenarios = new[]
        {
            MeasurementScenario.ExistingSsoLinkedAccepted,
            MeasurementScenario.ExistingUserNoOrgUser,
            MeasurementScenario.JitProvision
        };

        foreach (var scenario in scenarios)
        {
            // Act
            var onCounts = await MeasureCountsForScenarioAsync(sutProvider, scenario, preventNonCompliant: true);
            var offCounts = await MeasureCountsForScenarioAsync(sutProvider, scenario, preventNonCompliant: false);

            // Assert: off should not exceed on in any measured lookup type
            Assert.True(offCounts.UserGetBySso <= onCounts.UserGetBySso, $"{scenario}: off UserGetBySso={offCounts.UserGetBySso} > on {onCounts.UserGetBySso}");
            Assert.True(offCounts.UserGetByEmail <= onCounts.UserGetByEmail, $"{scenario}: off UserGetByEmail={offCounts.UserGetByEmail} > on {onCounts.UserGetByEmail}");
            Assert.True(offCounts.OrgGetById <= onCounts.OrgGetById, $"{scenario}: off OrgGetById={offCounts.OrgGetById} > on {onCounts.OrgGetById}");
            Assert.True(offCounts.OrgUserGetByOrg <= onCounts.OrgUserGetByOrg, $"{scenario}: off OrgUserGetByOrg={offCounts.OrgUserGetByOrg} > on {onCounts.OrgUserGetByOrg}");
            Assert.True(offCounts.OrgUserGetByEmail <= onCounts.OrgUserGetByEmail, $"{scenario}: off OrgUserGetByEmail={offCounts.OrgUserGetByEmail} > on {onCounts.OrgUserGetByEmail}");

            _output.WriteLine($"Scenario={scenario} | ON: SSO={onCounts.UserGetBySso}, Email={onCounts.UserGetByEmail}, Org={onCounts.OrgGetById}, OrgUserByOrg={onCounts.OrgUserGetByOrg}, OrgUserByEmail={onCounts.OrgUserGetByEmail}");
            _output.WriteLine($"Scenario={scenario} | OFF: SSO={offCounts.UserGetBySso}, Email={offCounts.UserGetByEmail}, Org={offCounts.OrgGetById}, OrgUserByOrg={offCounts.OrgUserGetByOrg}, OrgUserByEmail={offCounts.OrgUserGetByEmail}");
        }
    }

    [Theory, BitAutoData]
    public async Task AutoProvisionUserAsync_WithFeatureFlagEnabled_CallsRegisterSSOAutoProvisionedUser(
        SutProvider<AccountController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var providerUserId = "ext-new-user";
        var email = "newuser@example.com";
        var organization = new Organization { Id = orgId, Name = "Test Org", Seats = null };

        // No existing user (JIT provisioning scenario)
        sutProvider.GetDependency<IUserRepository>().GetByEmailAsync(email).Returns((User?)null);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(orgId).Returns(organization);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByOrganizationEmailAsync(orgId, email)
            .Returns((OrganizationUser?)null);

        // Feature flag enabled
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.MjmlWelcomeEmailTemplates)
            .Returns(true);

        // Mock the RegisterSSOAutoProvisionedUserAsync to return success
        sutProvider.GetDependency<IRegisterUserCommand>()
            .RegisterSSOAutoProvisionedUserAsync(Arg.Any<User>(), Arg.Any<Organization>())
            .Returns(IdentityResult.Success);

        var claims = new[]
        {
            new Claim(JwtClaimTypes.Email, email),
            new Claim(JwtClaimTypes.Name, "New User")
        } as IEnumerable<Claim>;
        var config = new SsoConfigurationData();

        var method = typeof(AccountController).GetMethod(
            "CreateUserAndOrgUserConditionallyAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        // Act
        var task = (Task<(User user, Organization organization, OrganizationUser orgUser)>)method!.Invoke(
            sutProvider.Sut,
            new object[]
            {
                orgId.ToString(),
                providerUserId,
                claims,
                null!,
                config
            })!;

        var result = await task;

        // Assert
        await sutProvider.GetDependency<IRegisterUserCommand>().Received(1)
            .RegisterSSOAutoProvisionedUserAsync(
                Arg.Is<User>(u => u.Email == email && u.Name == "New User"),
                Arg.Is<Organization>(o => o.Id == orgId && o.Name == "Test Org"));

        Assert.NotNull(result.user);
        Assert.Equal(email, result.user.Email);
        Assert.Equal(organization.Id, result.organization.Id);
    }

    [Theory, BitAutoData]
    public async Task AutoProvisionUserAsync_WithFeatureFlagDisabled_CallsRegisterUserInstead(
        SutProvider<AccountController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var providerUserId = "ext-legacy-user";
        var email = "legacyuser@example.com";
        var organization = new Organization { Id = orgId, Name = "Test Org", Seats = null };

        // No existing user (JIT provisioning scenario)
        sutProvider.GetDependency<IUserRepository>().GetByEmailAsync(email).Returns((User?)null);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(orgId).Returns(organization);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByOrganizationEmailAsync(orgId, email)
            .Returns((OrganizationUser?)null);

        // Feature flag disabled
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.MjmlWelcomeEmailTemplates)
            .Returns(false);

        // Mock the RegisterUser to return success
        sutProvider.GetDependency<IRegisterUserCommand>()
            .RegisterUser(Arg.Any<User>())
            .Returns(IdentityResult.Success);

        var claims = new[]
        {
            new Claim(JwtClaimTypes.Email, email),
            new Claim(JwtClaimTypes.Name, "Legacy User")
        } as IEnumerable<Claim>;
        var config = new SsoConfigurationData();

        var method = typeof(AccountController).GetMethod(
            "CreateUserAndOrgUserConditionallyAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        // Act
        var task = (Task<(User user, Organization organization, OrganizationUser orgUser)>)method!.Invoke(
            sutProvider.Sut,
            new object[]
            {
                orgId.ToString(),
                providerUserId,
                claims,
                null!,
                config
            })!;

        var result = await task;

        // Assert
        await sutProvider.GetDependency<IRegisterUserCommand>().Received(1)
            .RegisterUser(Arg.Is<User>(u => u.Email == email && u.Name == "Legacy User"));

        // Verify the new method was NOT called
        await sutProvider.GetDependency<IRegisterUserCommand>().DidNotReceive()
            .RegisterSSOAutoProvisionedUserAsync(Arg.Any<User>(), Arg.Any<Organization>());

        Assert.NotNull(result.user);
        Assert.Equal(email, result.user.Email);
    }
}
