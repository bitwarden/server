using System.Reflection;
using System.Security.Claims;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tokens;
using Bit.Sso.Controllers;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
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

    [Theory, BitAutoData]
    public async Task ExternalCallback_ExistingUser_NoOrgUser_ThrowsCouldNotFindOrganizationUser(
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

        sutProvider.GetDependency<IIdentityServerInteractionService>()
            .GetAuthorizationContextAsync("~/").Returns((AuthorizationRequest?)null);

        // Act + Assert
        var ex = await Assert.ThrowsAsync<Exception>(() => sutProvider.Sut.ExternalCallback());
        Assert.Equal("CouldNotFindOrganizationUser", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task ExternalCallback_ExistingUser_OrgUserInvited_AllowsLogin(
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
    public async Task ExternalCallback_ExistingUser_OrgUserRevoked_ThrowsAccessRevoked(
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

        sutProvider.GetDependency<IIdentityServerInteractionService>()
            .GetAuthorizationContextAsync("~/").Returns((AuthorizationRequest?)null);

        // Act + Assert
        var ex = await Assert.ThrowsAsync<Exception>(() => sutProvider.Sut.ExternalCallback());
        Assert.Equal("OrganizationUserAccessRevoked", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task ExternalCallback_ExistingUser_OrgUserUnknown_ThrowsUnknown(
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
    public async Task ExternalCallback_ExistingSsoLinkedAccepted_MeasureLookups(
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

    [Theory, BitAutoData]
    public async Task ExternalCallback_JitProvision_MeasureLookups(
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

    [Theory, BitAutoData]
    public async Task ExternalCallback_ExistingUser_NoOrgUser_MeasureLookups(
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

    [Theory, BitAutoData]
    public void ExternalChallenge_WithMatchingOrgId_Succeeds(
        SutProvider<AccountController> sutProvider,
        Organization organization)
    {
        // Arrange
        var orgId = organization.Id;
        var scheme = orgId.ToString();
        var returnUrl = "~/vault";
        var state = "test-state";
        var userIdentifier = "user-123";
        var ssoToken = "valid-sso-token";

        // Mock the data protector to return a tokenable with matching org ID
        var dataProtector = sutProvider.GetDependency<IDataProtectorTokenFactory<SsoTokenable>>();
        var tokenable = new SsoTokenable(organization, 3600);
        dataProtector.Unprotect(ssoToken).Returns(tokenable);

        // Mock URL helper for IsLocalUrl check
        var urlHelper = Substitute.For<IUrlHelper>();
        urlHelper.IsLocalUrl(returnUrl).Returns(true);
        sutProvider.Sut.Url = urlHelper;

        // Mock interaction service for IsValidReturnUrl check
        var interactionService = sutProvider.GetDependency<IIdentityServerInteractionService>();
        interactionService.IsValidReturnUrl(returnUrl).Returns(true);

        // Act
        var result = sutProvider.Sut.ExternalChallenge(scheme, returnUrl, state, userIdentifier, ssoToken);

        // Assert
        var challengeResult = Assert.IsType<ChallengeResult>(result);
        Assert.Contains(scheme, challengeResult.AuthenticationSchemes);
        Assert.NotNull(challengeResult.Properties);
        Assert.Equal(scheme, challengeResult.Properties.Items["scheme"]);
        Assert.Equal(returnUrl, challengeResult.Properties.Items["return_url"]);
        Assert.Equal(state, challengeResult.Properties.Items["state"]);
        Assert.Equal(userIdentifier, challengeResult.Properties.Items["user_identifier"]);
    }

    [Theory, BitAutoData]
    public void ExternalChallenge_WithMismatchedOrgId_ThrowsSsoOrganizationIdMismatch(
        SutProvider<AccountController> sutProvider,
        Organization organization)
    {
        // Arrange
        var correctOrgId = organization.Id;
        var wrongOrgId = Guid.NewGuid();
        var scheme = wrongOrgId.ToString(); // Different from tokenable's org ID
        var returnUrl = "~/vault";
        var state = "test-state";
        var userIdentifier = "user-123";
        var ssoToken = "valid-sso-token";

        // Mock the data protector to return a tokenable with different org ID
        var dataProtector = sutProvider.GetDependency<IDataProtectorTokenFactory<SsoTokenable>>();
        var tokenable = new SsoTokenable(organization, 3600); // Contains correctOrgId
        dataProtector.Unprotect(ssoToken).Returns(tokenable);

        // Mock i18n service to return the key
        sutProvider.GetDependency<II18nService>()
            .T(Arg.Any<string>())
            .Returns(ci => (string)ci[0]!);

        // Act & Assert
        var ex = Assert.Throws<Exception>(() =>
            sutProvider.Sut.ExternalChallenge(scheme, returnUrl, state, userIdentifier, ssoToken));
        Assert.Equal("SsoOrganizationIdMismatch", ex.Message);
    }

    [Theory, BitAutoData]
    public void ExternalChallenge_WithInvalidSchemeFormat_ThrowsSsoOrganizationIdMismatch(
        SutProvider<AccountController> sutProvider,
        Organization organization)
    {
        // Arrange
        var scheme = "not-a-valid-guid";
        var returnUrl = "~/vault";
        var state = "test-state";
        var userIdentifier = "user-123";
        var ssoToken = "valid-sso-token";

        // Mock the data protector to return a valid tokenable
        var dataProtector = sutProvider.GetDependency<IDataProtectorTokenFactory<SsoTokenable>>();
        var tokenable = new SsoTokenable(organization, 3600);
        dataProtector.Unprotect(ssoToken).Returns(tokenable);

        // Mock i18n service to return the key
        sutProvider.GetDependency<II18nService>()
            .T(Arg.Any<string>())
            .Returns(ci => (string)ci[0]!);

        // Act & Assert
        var ex = Assert.Throws<Exception>(() =>
            sutProvider.Sut.ExternalChallenge(scheme, returnUrl, state, userIdentifier, ssoToken));
        Assert.Equal("SsoOrganizationIdMismatch", ex.Message);
    }

    [Theory, BitAutoData]
    public void ExternalChallenge_WithInvalidSsoToken_ThrowsInvalidSsoToken(
        SutProvider<AccountController> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var scheme = orgId.ToString();
        var returnUrl = "~/vault";
        var state = "test-state";
        var userIdentifier = "user-123";
        var ssoToken = "invalid-corrupted-token";

        // Mock the data protector to throw when trying to unprotect
        var dataProtector = sutProvider.GetDependency<IDataProtectorTokenFactory<SsoTokenable>>();
        dataProtector.Unprotect(ssoToken).Returns(_ => throw new Exception("Token validation failed"));

        // Mock i18n service to return the key
        sutProvider.GetDependency<II18nService>()
            .T(Arg.Any<string>())
            .Returns(ci => (string)ci[0]!);

        // Act & Assert
        var ex = Assert.Throws<Exception>(() =>
            sutProvider.Sut.ExternalChallenge(scheme, returnUrl, state, userIdentifier, ssoToken));
        Assert.Equal("InvalidSsoToken", ex.Message);
    }
}
