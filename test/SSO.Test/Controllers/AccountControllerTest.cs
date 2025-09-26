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
using AuthenticationOptions = Duende.IdentityServer.Configuration.AuthenticationOptions;

namespace Bit.SSO.Test.Controllers;

public class AccountControllerTest
{
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

    private static void SetPreventNonCompliant(IFeatureService featureService, bool enabled)
        => featureService.IsEnabled(Arg.Any<string>()).Returns(enabled);

    private static void SetDefaultReturnContext(IIdentityServerInteractionService interactionService)
        => interactionService.GetAuthorizationContextAsync("~/").Returns((AuthorizationRequest?)null);

    [Fact]
    public void EnsureOrgUserStatusAllowed_AllowsAcceptedAndConfirmed()
    {
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

        InvokeEnsureOrgUserStatusAllowed(controller, OrganizationUserStatusType.Accepted,
            OrganizationUserStatusType.Accepted, OrganizationUserStatusType.Confirmed);
        InvokeEnsureOrgUserStatusAllowed(controller, OrganizationUserStatusType.Confirmed,
            OrganizationUserStatusType.Accepted, OrganizationUserStatusType.Confirmed);
    }

    [Fact]
    public void EnsureOrgUserStatusAllowed_Invited_ThrowsAcceptInvite()
    {
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

        var ex = Assert.Throws<TargetInvocationException>(() =>
            InvokeEnsureOrgUserStatusAllowed(controller, OrganizationUserStatusType.Invited,
                OrganizationUserStatusType.Accepted, OrganizationUserStatusType.Confirmed));

        Assert.IsType<Exception>(ex.InnerException);
        Assert.Equal("AcceptInviteBeforeUsingSSO", ex.InnerException!.Message);
    }

    [Fact]
    public void EnsureOrgUserStatusAllowed_Revoked_ThrowsAccessRevoked()
    {
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

        var ex = Assert.Throws<TargetInvocationException>(() =>
            InvokeEnsureOrgUserStatusAllowed(controller, OrganizationUserStatusType.Revoked,
                OrganizationUserStatusType.Accepted, OrganizationUserStatusType.Confirmed));

        Assert.IsType<Exception>(ex.InnerException);
        Assert.Equal("OrganizationUserAccessRevoked", ex.InnerException!.Message);
    }

    [Fact]
    public void EnsureOrgUserStatusAllowed_UnknownStatus_ThrowsUnknown()
    {
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
        var ex = Assert.Throws<TargetInvocationException>(() =>
            InvokeEnsureOrgUserStatusAllowed(controller, unknown,
                OrganizationUserStatusType.Accepted, OrganizationUserStatusType.Confirmed));

        Assert.IsType<Exception>(ex.InnerException);
        Assert.Equal("OrganizationUserUnknownStatus", ex.InnerException!.Message);
    }

    [Fact]
    public async Task ExternalCallback_WithExistingUserAndAcceptedMembership_RedirectsToReturnUrl()
    {
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

        SetPreventNonCompliant(featureService, true);
        SetDefaultReturnContext(interactionService);

        var result = await controller.ExternalCallback();

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

    [Fact]
    public async Task ExternalCallback_PreventNonCompliantFalse_SkipsOrgLookupAndSignsIn()
    {
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

        SetPreventNonCompliant(featureService, false);
        SetDefaultReturnContext(interactionService);

        var result = await controller.ExternalCallback();

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

    [Fact]
    public async Task AutoProvisionUserAsync_WithExistingAcceptedUser_CreatesSsoLinkAndReturnsUser()
    {
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

        var task = (Task<User>)method.Invoke(controller, [
            orgId.ToString(),
            providerUserId,
            claims,
            null!,
            config,
            organization,
            orgUser
        ])!;

        var returnedUser = await task;
        Assert.Equal(existingUser.Id, returnedUser.Id);

        await ssoUserRepository.Received().CreateAsync(Arg.Is<SsoUser>(s =>
            s.OrganizationId == orgId && s.UserId == existingUser.Id && s.ExternalId == providerUserId));

        await eventService.Received().LogOrganizationUserEventAsync(
            orgUser,
            EventType.OrganizationUser_FirstSsoLogin);
    }
}
