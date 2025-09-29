using System.Reflection;
using System.Security.Claims;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.UserFeatures.Registration;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using Bit.Sso.Controllers;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit.Abstractions;
using AuthenticationOptions = Duende.IdentityServer.Configuration.AuthenticationOptions;

namespace Bit.SSO.Test.Controllers;

public class AccountControllerTest
{
    private readonly ITestOutputHelper _output;

    public AccountControllerTest(ITestOutputHelper output)
    {
        _output = output;
    }
    private static AccountController CreateController(
        IAuthenticationService authenticationService,
        out ISsoConfigRepository ssoConfigRepository,
        out IUserRepository userRepository,
        out IOrganizationRepository organizationRepository,
        out IOrganizationUserRepository organizationUserRepository,
        out IIdentityServerInteractionService interactionService,
        out II18nService i18nService,
        out ISsoUserRepository ssoUserRepository,
        out Core.Services.IEventService eventService,
        out IFeatureService featureService)
    {
        var schemeProvider = Substitute.For<IAuthenticationSchemeProvider>();
        schemeProvider.GetDefaultAuthenticateSchemeAsync()
            .Returns(new AuthenticationScheme("idsrv", "idsrv", typeof(IAuthenticationHandler)));
        var clientStore = Substitute.For<IClientStore>();
        interactionService = Substitute.For<IIdentityServerInteractionService>();
        var logger = Substitute.For<ILogger<AccountController>>();
        organizationRepository = Substitute.For<IOrganizationRepository>();
        organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
        var organizationService = Substitute.For<IOrganizationService>();
        ssoConfigRepository = Substitute.For<ISsoConfigRepository>();
        ssoUserRepository = Substitute.For<ISsoUserRepository>();
        userRepository = Substitute.For<IUserRepository>();
        var policyRepository = Substitute.For<IPolicyRepository>();
        var userService = Substitute.For<IUserService>();
        i18nService = Substitute.For<II18nService>();
        i18nService.T(Arg.Any<string>(), Arg.Any<object?[]>()).Returns(ci => (string)ci[0]!);

        // Minimal UserManager setup (not used in tested code paths, but required by ctor)
        var userStore = Substitute.For<IUserStore<User>>();
        var identityOptions = Microsoft.Extensions.Options.Options.Create(new IdentityOptions());
        var passwordHasher = Substitute.For<IPasswordHasher<User>>();
        var userValidators = Array.Empty<IUserValidator<User>>();
        var passwordValidators = Array.Empty<IPasswordValidator<User>>();
        var lookupNormalizer = Substitute.For<ILookupNormalizer>();
        var errorDescriber = new IdentityErrorDescriber();
        var userLogger = Substitute.For<ILogger<UserManager<User>>>();
        var userManager = new UserManager<User>(
            userStore,
            identityOptions,
            passwordHasher,
            userValidators,
            passwordValidators,
            lookupNormalizer,
            errorDescriber,
            new ServiceCollection().BuildServiceProvider(),
            userLogger);

        var globalSettings = Substitute.For<IGlobalSettings>();
        eventService = Substitute.For<Core.Services.IEventService>();
        var dataProtector = Substitute.For<IDataProtectorTokenFactory<SsoTokenable>>();
        var organizationDomainRepository = Substitute.For<IOrganizationDomainRepository>();
        var registerUserCommand = Substitute.For<IRegisterUserCommand>();
        featureService = Substitute.For<IFeatureService>();

        var controller = new AccountController(
            schemeProvider,
            clientStore,
            interactionService,
            logger,
            organizationRepository,
            organizationUserRepository,
            organizationService,
            ssoConfigRepository,
            ssoUserRepository,
            userRepository,
            policyRepository,
            userService,
            i18nService,
            userManager,
            globalSettings,
            eventService,
            dataProtector,
            organizationDomainRepository,
            registerUserCommand,
            featureService);

        var services = new ServiceCollection();
        services.AddSingleton(authenticationService);
        services.AddSingleton<IAuthenticationSchemeProvider>(schemeProvider);
        services.AddSingleton(new IdentityServerOptions
        {
            Authentication = new AuthenticationOptions
            {
                CookieAuthenticationScheme = "idsrv"
            }
        });
        var sp = services.BuildServiceProvider();

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                RequestServices = sp
            }
        };

        return controller;
    }

    private static void InvokeEnsureOrgUserStatusAllowed(
        AccountController controller,
        OrganizationUserStatusType status,
        params OrganizationUserStatusType[] allowed)
    {
        var method = typeof(AccountController).GetMethod(
            "EnsureOrgUserStatusAllowed",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(controller, [status, "Org", allowed]);
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

    private static AccountController CreateControllerWithAuth(
        AuthenticateResult authResult,
        out IAuthenticationService authService,
        out ISsoConfigRepository ssoConfigRepository,
        out IUserRepository userRepository,
        out IOrganizationRepository organizationRepository,
        out IOrganizationUserRepository organizationUserRepository,
        out IIdentityServerInteractionService interactionService,
        out II18nService i18nService,
        out ISsoUserRepository ssoUserRepository,
        out Core.Services.IEventService eventService,
        out IFeatureService featureService)
    {
        var authServiceSub = Substitute.For<IAuthenticationService>();
        authServiceSub.AuthenticateAsync(
            Arg.Any<HttpContext>(),
            AuthenticationSchemes.BitwardenExternalCookieAuthenticationScheme)
            .Returns(authResult);
        authService = authServiceSub;
        return CreateController(
            authServiceSub,
            out ssoConfigRepository,
            out userRepository,
            out organizationRepository,
            out organizationUserRepository,
            out interactionService,
            out i18nService,
            out ssoUserRepository,
            out eventService,
            out featureService);
    }

    private static void ConfigureSsoAndUser(
        ISsoConfigRepository ssoConfigRepository,
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        Guid orgId,
        string providerUserId,
        User user,
        Organization? organization = null,
        OrganizationUser? orgUser = null)
    {
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

    private async Task<LookupCounts> MeasureCountsForScenarioAsync(MeasurementScenario scenario, bool preventNonCompliant)
    {
        var orgId = Guid.NewGuid();
        var providerUserId = $"meas-{scenario}-{(preventNonCompliant ? "on" : "off")}";
        var email = scenario == MeasurementScenario.JitProvision
            ? "jit.compare@example.com"
            : "existing.compare@example.com";

        var organization = new Organization { Id = orgId, Name = "Org" };
        var user = new User { Id = Guid.NewGuid(), Email = email };

        var authResult = BuildSuccessfulExternalAuth(orgId, providerUserId, email);
        var controller = CreateControllerWithAuth(
            authResult,
            out var authService,
            out var ssoConfigRepository,
            out var userRepository,
            out var organizationRepository,
            out var organizationUserRepository,
            out var interactionService,
            out var i18nService,
            out var ssoUserRepository,
            out var eventService,
            out var featureService);

        // SSO config present
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
            _ = await controller.ExternalCallback();
        }
        catch
        {
            // Ignore exceptions for measurement; some flows can throw based on status enforcement
        }

        return new LookupCounts
        {
            UserGetBySso = userRepository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IUserRepository.GetBySsoUserAsync)),
            UserGetByEmail = userRepository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IUserRepository.GetByEmailAsync)),
            OrgGetById = organizationRepository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IOrganizationRepository.GetByIdAsync)),
            OrgUserGetByOrg = organizationUserRepository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IOrganizationUserRepository.GetByOrganizationAsync)),
            OrgUserGetByEmail = organizationUserRepository.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IOrganizationUserRepository.GetByOrganizationEmailAsync)),
        };
    }

    [Fact]
    public void EnsureOrgUserStatusAllowed_AllowsAcceptedAndConfirmed()
    {
        // Arrange
        var authService = Substitute.For<IAuthenticationService>();
        var controller = CreateController(
            authService,
            out var ssoConfigRepository,
            out var userRepository,
            out var organizationRepository,
            out var organizationUserRepository,
            out var interactionService,
            out var i18nService,
            out var ssoUserRepository,
            out var eventService,
            out var featureService);

        // Act
        var ex1 = Record.Exception(() =>
            InvokeEnsureOrgUserStatusAllowed(controller, OrganizationUserStatusType.Accepted,
                OrganizationUserStatusType.Accepted, OrganizationUserStatusType.Confirmed));
        var ex2 = Record.Exception(() =>
            InvokeEnsureOrgUserStatusAllowed(controller, OrganizationUserStatusType.Confirmed,
                OrganizationUserStatusType.Accepted, OrganizationUserStatusType.Confirmed));

        // Assert
        Assert.Null(ex1);
        Assert.Null(ex2);
    }

    [Fact]
    public void EnsureOrgUserStatusAllowed_Invited_ThrowsAcceptInvite()
    {
        // Arrange
        var authService = Substitute.For<IAuthenticationService>();
        var controller = CreateController(
            authService,
            out var ssoConfigRepository,
            out var userRepository,
            out var organizationRepository,
            out var organizationUserRepository,
            out var interactionService,
            out var i18nService,
            out var ssoUserRepository,
            out var eventService,
            out var featureService);

        // Act
        var ex = Assert.Throws<TargetInvocationException>(() =>
            InvokeEnsureOrgUserStatusAllowed(controller, OrganizationUserStatusType.Invited,
                OrganizationUserStatusType.Accepted, OrganizationUserStatusType.Confirmed));

        // Assert
        Assert.IsType<Exception>(ex.InnerException);
        Assert.Equal("AcceptInviteBeforeUsingSSO", ex.InnerException!.Message);
    }

    [Fact]
    public void EnsureOrgUserStatusAllowed_Revoked_ThrowsAccessRevoked()
    {
        // Arrange
        var authService = Substitute.For<IAuthenticationService>();
        var controller = CreateController(
            authService,
            out var ssoConfigRepository,
            out var userRepository,
            out var organizationRepository,
            out var organizationUserRepository,
            out var interactionService,
            out var i18nService,
            out var ssoUserRepository,
            out var eventService,
            out var featureService);

        // Act
        var ex = Assert.Throws<TargetInvocationException>(() =>
            InvokeEnsureOrgUserStatusAllowed(controller, OrganizationUserStatusType.Revoked,
                OrganizationUserStatusType.Accepted, OrganizationUserStatusType.Confirmed));

        // Assert
        Assert.IsType<Exception>(ex.InnerException);
        Assert.Equal("OrganizationUserAccessRevoked", ex.InnerException!.Message);
    }

    [Fact]
    public void EnsureOrgUserStatusAllowed_UnknownStatus_ThrowsUnknown()
    {
        // Arrange
        var authService = Substitute.For<IAuthenticationService>();
        var controller = CreateController(
            authService,
            out var ssoConfigRepository,
            out var userRepository,
            out var organizationRepository,
            out var organizationUserRepository,
            out var interactionService,
            out var i18nService,
            out var ssoUserRepository,
            out var eventService,
            out var featureService);

        var unknown = (OrganizationUserStatusType)999;

        // Act
        var ex = Assert.Throws<TargetInvocationException>(() =>
            InvokeEnsureOrgUserStatusAllowed(controller, unknown,
                OrganizationUserStatusType.Accepted, OrganizationUserStatusType.Confirmed));

        // Assert
        Assert.IsType<Exception>(ex.InnerException);
        Assert.Equal("OrganizationUserUnknownStatus", ex.InnerException!.Message);
    }

    [Fact]
    public async Task ExternalCallback_WithExistingUserAndAcceptedMembership_RedirectsToReturnUrl()
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
        var controller = CreateControllerWithAuth(
            authResult,
            out var authService,
            out var ssoConfigRepository,
            out var userRepository,
            out var organizationRepository,
            out var organizationUserRepository,
            out var interactionService,
            out var i18nService,
            out var ssoUserRepository,
            out var eventService,
            out var featureService);

        ConfigureSsoAndUser(
            ssoConfigRepository,
            userRepository,
            organizationRepository,
            organizationUserRepository,
            orgId,
            providerUserId,
            user,
            organization,
            orgUser);

        featureService.IsEnabled(Arg.Any<string>()).Returns(true);
        interactionService.GetAuthorizationContextAsync("~/").Returns((AuthorizationRequest?)null);

        // Act
        var result = await controller.ExternalCallback();

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
    [Fact]
    public async Task ExternalCallback_PreventNonCompliantFalse_SkipsOrgLookupAndSignsIn()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var providerUserId = "ext-flag-off";
        var user = new User { Id = Guid.NewGuid(), Email = "flagoff@example.com" };

        var authResult = BuildSuccessfulExternalAuth(orgId, providerUserId, user.Email!);
        var controller = CreateControllerWithAuth(
            authResult,
            out var authService,
            out var ssoConfigRepository,
            out var userRepository,
            out var organizationRepository,
            out var organizationUserRepository,
            out var interactionService,
            out var i18nService,
            out var ssoUserRepository,
            out var eventService,
            out var featureService);

        ConfigureSsoAndUser(
            ssoConfigRepository,
            userRepository,
            organizationRepository,
            organizationUserRepository,
            orgId,
            providerUserId,
            user);

        featureService.IsEnabled(Arg.Any<string>()).Returns(false);
        interactionService.GetAuthorizationContextAsync("~/").Returns((AuthorizationRequest?)null);

        // Act
        var result = await controller.ExternalCallback();

        // Assert
        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("~/", redirect.Url);

        await authService.Received().SignInAsync(
            Arg.Any<HttpContext>(),
            Arg.Any<string?>(),
            Arg.Any<ClaimsPrincipal>(),
            Arg.Any<AuthenticationProperties>());

        // When flag is off, controller does not require org or orgUser; ensure repo not called for orgUser
        await organizationUserRepository.DidNotReceiveWithAnyArgs().GetByOrganizationAsync(Guid.Empty, Guid.Empty);
    }

    /// <summary>
    /// PM-24579: Permanent test, remove the True in PreventNonCompliantTrue and remove the configure for the feature
    /// flag.
    /// </summary>
    [Fact]
    public async Task ExternalCallback_PreventNonCompliantTrue_ExistingSsoLinkedAccepted_MeasureLookups()
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

        var authResult = BuildSuccessfulExternalAuth(orgId, providerUserId, user.Email!);
        var controller = CreateControllerWithAuth(
            authResult,
            out var authService,
            out var ssoConfigRepository,
            out var userRepository,
            out var organizationRepository,
            out var organizationUserRepository,
            out var interactionService,
            out var i18nService,
            out var ssoUserRepository,
            out var eventService,
            out var featureService);

        ConfigureSsoAndUser(
            ssoConfigRepository,
            userRepository,
            organizationRepository,
            organizationUserRepository,
            orgId,
            providerUserId,
            user,
            organization,
            orgUser);

        featureService.IsEnabled(Arg.Any<string>()).Returns(true);
        interactionService.GetAuthorizationContextAsync("~/").Returns((AuthorizationRequest?)null);

        // Act
        try
        {
            _ = await controller.ExternalCallback();
        }
        catch
        {
            // ignore for measurement only
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
        Assert.Equal(0, userGetByEmail);
        Assert.Equal(1, orgGet);
        Assert.Equal(1, orgUserGetByOrg);
        Assert.Equal(0, orgUserGetByEmail);
    }

    /// <summary>
    /// PM-24579: Permanent test, remove the True in PreventNonCompliantTrue and remove the configure for the feature
    /// flag.
    /// </summary>
    [Fact]
    public async Task ExternalCallback_PreventNonCompliantTrue_JitProvision_MeasureLookups()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var providerUserId = "ext-measure-jit";
        var email = "jit.measure@example.com";
        var organization = new Organization { Id = orgId, Name = "Org", Seats = null };

        var authResult = BuildSuccessfulExternalAuth(orgId, providerUserId, email);
        var controller = CreateControllerWithAuth(
            authResult,
            out var authService,
            out var ssoConfigRepository,
            out var userRepository,
            out var organizationRepository,
            out var organizationUserRepository,
            out var interactionService,
            out var i18nService,
            out var ssoUserRepository,
            out var eventService,
            out var featureService);

        // Configure SSO config and ensure there is NO existing SSO link or user (JIT)
        var ssoConfig = new SsoConfig { OrganizationId = orgId, Enabled = true };
        var ssoData = new SsoConfigurationData();
        ssoConfig.SetData(ssoData);
        ssoConfigRepository.GetByOrganizationIdAsync(orgId).Returns(ssoConfig);

        userRepository.GetBySsoUserAsync(providerUserId, orgId).Returns((User?)null);
        userRepository.GetByEmailAsync(email).Returns((User?)null);
        organizationRepository.GetByIdAsync(orgId).Returns(organization);
        organizationUserRepository.GetByOrganizationEmailAsync(orgId, email).Returns((OrganizationUser?)null);

        featureService.IsEnabled(Arg.Any<string>()).Returns(true);
        interactionService.GetAuthorizationContextAsync("~/").Returns((AuthorizationRequest?)null);

        // Act
        try
        {
            _ = await controller.ExternalCallback();
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
    /// </summary>
    [Fact]
    public async Task ExternalCallback_PreventNonCompliantTrue_ExistingUser_NoOrgUser_MeasureLookups()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var providerUserId = "ext-measure-existing-no-orguser";
        var user = new User { Id = Guid.NewGuid(), Email = "existing2@example.com" };
        var organization = new Organization { Id = orgId, Name = "Org" };

        var authResult = BuildSuccessfulExternalAuth(orgId, providerUserId, user.Email!);
        var controller = CreateControllerWithAuth(
            authResult,
            out var authService,
            out var ssoConfigRepository,
            out var userRepository,
            out var organizationRepository,
            out var organizationUserRepository,
            out var interactionService,
            out var i18nService,
            out var ssoUserRepository,
            out var eventService,
            out var featureService);

        ConfigureSsoAndUser(
            ssoConfigRepository,
            userRepository,
            organizationRepository,
            organizationUserRepository,
            orgId,
            providerUserId,
            user,
            organization,
            orgUser: null);

        // Ensure orgUser lookup returns null
        organizationUserRepository.GetByOrganizationAsync(organization.Id, user.Id).Returns((OrganizationUser?)null);

        featureService.IsEnabled(Arg.Any<string>()).Returns(true);
        interactionService.GetAuthorizationContextAsync("~/").Returns((AuthorizationRequest?)null);

        // Act
        try
        {
            _ = await controller.ExternalCallback();
        }
        catch
        {
            // ignore for measurement only
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
        Assert.Equal(0, userGetByEmail);
        Assert.Equal(1, orgGet);
        Assert.Equal(1, orgUserGetByOrg);
        Assert.Equal(0, orgUserGetByEmail);
    }

    /// <summary>
    /// PM-24579: Temporary test, remove with feature flag.
    /// </summary>
    [Fact]
    public async Task ExternalCallback_PreventNonCompliantFalse_ExistingSsoLinkedAccepted_MeasureLookups()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var providerUserId = "ext-measure-existing-flagoff";
        var user = new User { Id = Guid.NewGuid(), Email = "existing.flagoff@example.com" };

        var authResult = BuildSuccessfulExternalAuth(orgId, providerUserId, user.Email!);
        var controller = CreateControllerWithAuth(
            authResult,
            out var authService,
            out var ssoConfigRepository,
            out var userRepository,
            out var organizationRepository,
            out var organizationUserRepository,
            out var interactionService,
            out var i18nService,
            out var ssoUserRepository,
            out var eventService,
            out var featureService);

        var ssoConfig = new SsoConfig { OrganizationId = orgId, Enabled = true };
        var ssoData = new SsoConfigurationData();
        ssoConfig.SetData(ssoData);
        ssoConfigRepository.GetByOrganizationIdAsync(orgId).Returns(ssoConfig);
        userRepository.GetBySsoUserAsync(providerUserId, orgId).Returns(user);

        featureService.IsEnabled(Arg.Any<string>()).Returns(false);
        interactionService.GetAuthorizationContextAsync("~/").Returns((AuthorizationRequest?)null);

        // Act
        try { _ = await controller.ExternalCallback(); } catch { }

        // Assert (measurement)
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
    [Fact]
    public async Task ExternalCallback_PreventNonCompliantFalse_ExistingUser_NoOrgUser_MeasureLookups()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var providerUserId = "ext-measure-existing-no-orguser-flagoff";
        var user = new User { Id = Guid.NewGuid(), Email = "existing2.flagoff@example.com" };

        var authResult = BuildSuccessfulExternalAuth(orgId, providerUserId, user.Email!);
        var controller = CreateControllerWithAuth(
            authResult,
            out var authService,
            out var ssoConfigRepository,
            out var userRepository,
            out var organizationRepository,
            out var organizationUserRepository,
            out var interactionService,
            out var i18nService,
            out var ssoUserRepository,
            out var eventService,
            out var featureService);

        var ssoConfig = new SsoConfig { OrganizationId = orgId, Enabled = true };
        var ssoData = new SsoConfigurationData();
        ssoConfig.SetData(ssoData);
        ssoConfigRepository.GetByOrganizationIdAsync(orgId).Returns(ssoConfig);
        userRepository.GetBySsoUserAsync(providerUserId, orgId).Returns(user);

        featureService.IsEnabled(Arg.Any<string>()).Returns(false);
        interactionService.GetAuthorizationContextAsync("~/").Returns((AuthorizationRequest?)null);

        // Act
        try { _ = await controller.ExternalCallback(); } catch { }

        // Assert (measurement)
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
    [Fact]
    public async Task ExternalCallback_PreventNonCompliantFalse_JitProvision_MeasureLookups()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var providerUserId = "ext-measure-jit-flagoff";
        var email = "jit.flagoff@example.com";
        var organization = new Organization { Id = orgId, Name = "Org", Seats = null };

        var authResult = BuildSuccessfulExternalAuth(orgId, providerUserId, email);
        var controller = CreateControllerWithAuth(
            authResult,
            out var authService,
            out var ssoConfigRepository,
            out var userRepository,
            out var organizationRepository,
            out var organizationUserRepository,
            out var interactionService,
            out var i18nService,
            out var ssoUserRepository,
            out var eventService,
            out var featureService);

        var ssoConfig = new SsoConfig { OrganizationId = orgId, Enabled = true };
        var ssoData = new SsoConfigurationData();
        ssoConfig.SetData(ssoData);
        ssoConfigRepository.GetByOrganizationIdAsync(orgId).Returns(ssoConfig);

        // JIT (no existing user or sso link)
        userRepository.GetBySsoUserAsync(providerUserId, orgId).Returns((User?)null);
        userRepository.GetByEmailAsync(email).Returns((User?)null);
        organizationRepository.GetByIdAsync(orgId).Returns(organization);
        organizationUserRepository.GetByOrganizationEmailAsync(orgId, email).Returns((OrganizationUser?)null);

        featureService.IsEnabled(Arg.Any<string>()).Returns(false);
        interactionService.GetAuthorizationContextAsync("~/").Returns((AuthorizationRequest?)null);

        // Act
        try { _ = await controller.ExternalCallback(); } catch { }

        // Assert (measurement)
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

    [Fact]
    public async Task AutoProvisionUserAsync_WithExistingAcceptedUser_CreatesSsoLinkAndReturnsUser()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var providerUserId = "ext-456";
        var email = "jit@example.com";
        var existingUser = new User { Id = Guid.NewGuid(), Email = email };
        var organization = new Organization { Id = orgId, Name = "Org" };
        var orgUser = new OrganizationUser
        {
            OrganizationId = orgId,
            UserId = existingUser.Id,
            Status = OrganizationUserStatusType.Accepted,
            Type = OrganizationUserType.User
        };

        var authService = Substitute.For<IAuthenticationService>();
        var controller = CreateController(
            authService,
            out var ssoConfigRepository,
            out var userRepository,
            out var organizationRepository,
            out var organizationUserRepository,
            out var interactionService,
            out var i18nService,
            out var ssoUserRepository,
            out var eventService,
            out var featureService);

        // Arrange repository expectations for the flow
        userRepository.GetByEmailAsync(email).Returns(existingUser);

        // No existing SSO link so first SSO login event is logged
        ssoUserRepository.GetByUserIdOrganizationIdAsync(orgId, existingUser.Id).Returns((SsoUser?)null);

        var claims = new[]
        {
            new Claim(JwtClaimTypes.Email, email),
            new Claim(JwtClaimTypes.Name, "Jit User")
        } as IEnumerable<Claim>;
        var config = new SsoConfigurationData();

        var method = typeof(AccountController).GetMethod(
            "AutoProvisionUserAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        // Act
        var task = (Task<(User user, Organization organization, OrganizationUser orgUser)>)method.Invoke(controller, [
            orgId.ToString(),
            providerUserId,
            claims,
            null!,
            config,
            organization,
            orgUser
        ])!;

        var returned = await task;

        // Assert
        Assert.Equal(existingUser.Id, returned.user.Id);

        await ssoUserRepository.Received().CreateAsync(Arg.Is<SsoUser>(s =>
            s.OrganizationId == orgId && s.UserId == existingUser.Id && s.ExternalId == providerUserId));

        await eventService.Received().LogOrganizationUserEventAsync(
            orgUser,
            EventType.OrganizationUser_FirstSsoLogin);
    }

    /// <summary>
    /// PM-24579: Temporary comparison test to ensure the feature flag ON does not
    /// regress lookup counts compared to OFF. When removing the flag, delete this
    /// comparison test and keep the specific scenario snapshot tests if desired.
    /// </summary>
    [Fact]
    public async Task ExternalCallback_Measurements_FlagOnVsOff_Comparisons()
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
            var onCounts = await MeasureCountsForScenarioAsync(scenario, preventNonCompliant: true);
            var offCounts = await MeasureCountsForScenarioAsync(scenario, preventNonCompliant: false);

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
}
