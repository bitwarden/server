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

        // Prepare authenticate result that AccountController expects to read
        var claims = new[]
        {
            new Claim(JwtClaimTypes.Subject, providerUserId),
            new Claim(JwtClaimTypes.Email, user.Email!)
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
        var authenticateResult = AuthenticateResult.Success(ticket);

        var authService = Substitute.For<IAuthenticationService>();
        authService.AuthenticateAsync(
            Arg.Any<HttpContext>(),
            AuthenticationSchemes.BitwardenExternalCookieAuthenticationScheme)
            .Returns(authenticateResult);

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

        // Configure dependencies used by FindUserFromExternalProviderAsync and enforcement
        var ssoConfig = new SsoConfig { OrganizationId = orgId, Enabled = true };
        var ssoData = new SsoConfigurationData();
        ssoConfig.SetData(ssoData);
        ssoConfigRepository.GetByOrganizationIdAsync(orgId).Returns(ssoConfig);

        userRepository.GetBySsoUserAsync(providerUserId, orgId).Returns(user);
        organizationRepository.GetByIdAsync(orgId).Returns(organization);
        organizationUserRepository.GetByOrganizationAsync(organization.Id, user.Id).Returns(orgUser);

        featureService.IsEnabled(Arg.Any<string>()).Returns(true);
        interactionService.GetAuthorizationContextAsync("~/").Returns((AuthorizationRequest?)null);

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

        var task = (Task<User>)method!.Invoke(controller, new object[]
        {
            orgId.ToString(),
            providerUserId,
            claims,
            null!,
            config,
            organization,
            orgUser
        })!;

        var returnedUser = await task;
        Assert.Equal(existingUser.Id, returnedUser.Id);

        await ssoUserRepository.Received().CreateAsync(Arg.Is<SsoUser>(s =>
            s.OrganizationId == orgId && s.UserId == existingUser.Id && s.ExternalId == providerUserId));

        await eventService.Received().LogOrganizationUserEventAsync(
            orgUser,
            EventType.OrganizationUser_FirstSsoLogin);
    }
}
