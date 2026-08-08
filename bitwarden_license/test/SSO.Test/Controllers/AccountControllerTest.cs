using System.Reflection;
using System.Security.Claims;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
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
using Bit.Sso.Exceptions;
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
    public async Task ExternalCallback_ExistingUser_OrgUserInvited_NoSsoLink_RedirectsToWebVaultLogin(
        SutProvider<AccountController> sutProvider)
    {
        // Arrange
        // The distinguishing setup from ExternalCallback_ExistingUser_OrgUserInvited_AllowsLogin
        // is that GetBySsoUserAsync returns null below — so the flow falls into
        // CreateUserAndOrgUserConditionallyAsync and trips the invited-status gate that
        // throws SsoAuthnRequiresInviteAcceptanceException. The catch block in
        // ExternalCallback must then sign out the external cookie and return a
        // RedirectResult to the web vault's /login.
        var orgId = Guid.NewGuid();
        var providerUserId = "ext-invited-no-sso-link";
        var email = "invited.user@example.com";
        var existingUser = new User { Id = Guid.NewGuid(), Email = email, UsesKeyConnector = false };
        var organization = new Organization { Id = orgId, Name = "Acme Corp" };
        var orgUser = new OrganizationUser
        {
            OrganizationId = orgId,
            UserId = existingUser.Id,
            Status = OrganizationUserStatusType.Invited,
            Type = OrganizationUserType.User
        };

        var authResult = BuildSuccessfulExternalAuth(orgId, providerUserId, email);
        var authService = SetupHttpContextWithAuth(sutProvider, authResult);

        sutProvider.GetDependency<II18nService>()
            .T(Arg.Any<string>(), Arg.Any<object?[]>())
            .Returns(ci => (string)ci[0]!);

        var ssoConfig = new SsoConfig { OrganizationId = orgId, Enabled = true };
        ssoConfig.SetData(new SsoConfigurationData());
        sutProvider.GetDependency<ISsoConfigRepository>().GetByOrganizationIdAsync(orgId).Returns(ssoConfig);

        // No existing SSO link → flow takes the CreateUserAndOrgUserConditionallyAsync branch.
        sutProvider.GetDependency<IUserRepository>().GetBySsoUserAsync(providerUserId, orgId)
            .Returns((User?)null);
        sutProvider.GetDependency<IUserRepository>().GetByEmailAsync(email).Returns(existingUser);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(orgId).Returns(organization);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyByUserAsync(existingUser.Id)
            .Returns([orgUser]);

        // Stub the redirect target so we can assert against a known URL composition.
        sutProvider.GetDependency<IGlobalSettings>().BaseServiceUri.VaultWithHash
            .Returns("https://vault.bitwarden.com/#");

        // Act
        var result = await sutProvider.Sut.ExternalCallback();

        // Assert — redirect URL is composed exactly as SsoRedirectUrlBuilder produces it.
        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal(
            "https://vault.bitwarden.com/#/login" +
            $"?email={Uri.EscapeDataString(email)}" +
            $"&organizationId={orgId}" +
            $"&organizationName={Uri.EscapeDataString(organization.Name)}" +
            "&error=ssoOrgInviteAcceptanceRequired",
            redirect.Url);

        // External auth cookie is cleared so retry attempts start fresh.
        await authService.Received(1).SignOutAsync(
            Arg.Any<HttpContext>(),
            AuthenticationSchemes.BitwardenExternalCookieAuthenticationScheme,
            Arg.Any<AuthenticationProperties>());

        // Security invariant: no local auth session is established for an invited user.
        await authService.DidNotReceive().SignInAsync(
            Arg.Any<HttpContext>(),
            Arg.Any<string?>(),
            Arg.Any<ClaimsPrincipal>(),
            Arg.Any<AuthenticationProperties>());

        // Security invariant: no SsoUser row is written before invite acceptance.
        await sutProvider.GetDependency<ISsoUserRepository>().DidNotReceive()
            .CreateAsync(Arg.Any<SsoUser>());
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
    public async Task ExternalCallback_JitProvision_NewUserAndOrgUserCreated_DoesNotMarkEmailVerified(
        SutProvider<AccountController> sutProvider)
    {
        // Arrange - Scenario 2: no existing User, no existing OrganizationUser. The JIT
        // flow creates both a new User and a new OrganizationUser. The controller must
        // not flip EmailVerified on the newly-created User: the flag reflects proof of
        // inbox ownership, and JIT does not establish that.
        var orgId = Guid.NewGuid();
        var providerUserId = "ext-jit-new-user";
        var email = "user@example.com";
        var organization = new Organization { Id = orgId, Name = "Org", Seats = null };

        var authResult = BuildSuccessfulExternalAuth(orgId, providerUserId, email);
        SetupHttpContextWithAuth(sutProvider, authResult);

        var ssoConfigRepository = sutProvider.GetDependency<ISsoConfigRepository>();
        var userRepository = sutProvider.GetDependency<IUserRepository>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var registerUserCommand = sutProvider.GetDependency<IRegisterUserCommand>();

        var ssoConfig = new SsoConfig { OrganizationId = orgId, Enabled = true };
        var ssoData = new SsoConfigurationData();
        ssoConfig.SetData(ssoData);
        ssoConfigRepository.GetByOrganizationIdAsync(orgId).Returns(ssoConfig);

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
            // ExternalCallback's post-provision sign-in path may throw in the SutProvider
            // harness; the RegisterSSOAutoProvisionedUserAsync call under test fires
            // before that point.
        }

        // Assert
        await registerUserCommand.Received(1).RegisterSSOAutoProvisionedUserAsync(
            Arg.Is<User>(u => u.Email == email && u.EmailVerified == false),
            Arg.Is<Organization>(o => o.Id == orgId));
    }

    [Theory, BitAutoData]
    public async Task ExternalCallback_JitProvision_ExistingInvitedOrgUser_DoesNotMarkEmailVerified(
        SutProvider<AccountController> sutProvider)
    {
        // Arrange - Scenario 3: no existing User, but there IS an existing OrganizationUser
        // row invited by email. The JIT flow creates a new User and links it. The controller
        // must not flip EmailVerified on the newly-created User: the flag reflects proof of
        // inbox ownership, and JIT does not establish that.
        var orgId = Guid.NewGuid();
        var providerUserId = "ext-jit-invited-orguser";
        var email = "invited@example.com";
        var organization = new Organization { Id = orgId, Name = "Org", Seats = null };
        var existingOrgUser = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            Email = email,
            Status = OrganizationUserStatusType.Invited,
            Type = OrganizationUserType.User
        };

        var authResult = BuildSuccessfulExternalAuth(orgId, providerUserId, email);
        SetupHttpContextWithAuth(sutProvider, authResult);

        var ssoConfigRepository = sutProvider.GetDependency<ISsoConfigRepository>();
        var userRepository = sutProvider.GetDependency<IUserRepository>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var registerUserCommand = sutProvider.GetDependency<IRegisterUserCommand>();

        var ssoConfig = new SsoConfig { OrganizationId = orgId, Enabled = true };
        var ssoData = new SsoConfigurationData();
        ssoConfig.SetData(ssoData);
        ssoConfigRepository.GetByOrganizationIdAsync(orgId).Returns(ssoConfig);

        userRepository.GetBySsoUserAsync(providerUserId, orgId).Returns((User?)null);
        userRepository.GetByEmailAsync(email).Returns((User?)null);
        organizationRepository.GetByIdAsync(orgId).Returns(organization);
        organizationUserRepository.GetByOrganizationEmailAsync(orgId, email).Returns(existingOrgUser);

        sutProvider.GetDependency<IIdentityServerInteractionService>()
            .GetAuthorizationContextAsync("~/").Returns((AuthorizationRequest?)null);

        // Act
        try
        {
            _ = await sutProvider.Sut.ExternalCallback();
        }
        catch
        {
            // ExternalCallback's post-provision sign-in path may throw in the SutProvider
            // harness; the RegisterSSOAutoProvisionedUserAsync call under test fires
            // before that point.
        }

        // Assert
        await registerUserCommand.Received(1).RegisterSSOAutoProvisionedUserAsync(
            Arg.Is<User>(u => u.Email == email && u.EmailVerified == false),
            Arg.Is<Organization>(o => o.Id == orgId));
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
    public async Task CreateUserAndOrgUserConditionallyAsync_WithExistingInvitedUser_ThrowsSsoAuthnRequiresInviteAcceptanceException(
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

        // The invited-status gate now throws a typed exception so ExternalCallback can
        // catch it and redirect the user back to the web client's /login. The security
        // gate itself (refusing SSO completion for invited users) is unchanged.
        var ex = await Assert.ThrowsAsync<SsoAuthnRequiresInviteAcceptanceException>(async () => await task);
        Assert.Equal(orgId, ex.OrganizationId);
        Assert.Equal("Org", ex.OrganizationDisplayName);
        Assert.Equal(email, ex.UserEmail);
    }

    [Theory, BitAutoData]
    public async Task CreateUserAndOrgUserConditionallyAsync_WithExistingUserButNoOrgUserRow_ThrowsSsoAuthnRequiresOrgMembershipException(
        SutProvider<AccountController> sutProvider)
    {
        // Arrange — the existing-user / no-OrganizationUser branch:
        // existing BW user exists, but no OrganizationUser row in the target org
        // (neither by UserId+OrgId nor by OrgId+Email). Covers both the user who
        // clicked an open invite link (client has the invite stashed) and the user
        // with no pending invite at all — the server cannot tell them apart at this gate.
        var orgId = Guid.NewGuid();
        var providerUserId = "provider-user-id";
        var email = "user@example.com";
        var existingUser = new User { Id = Guid.NewGuid(), Email = email, UsesKeyConnector = false };
        var organization = new Organization { Id = orgId, Name = "Org" };

        sutProvider.GetDependency<II18nService>()
            .T(Arg.Any<string>(), Arg.Any<object?[]>())
            .Returns(ci => (string)ci[0]!);

        sutProvider.GetDependency<IUserRepository>().GetByEmailAsync(email).Returns(existingUser);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(orgId).Returns(organization);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyByUserAsync(existingUser.Id)
            .Returns(new List<OrganizationUser>());
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationEmailAsync(orgId, email).Returns((OrganizationUser?)null);

        var claims = new[]
        {
            new Claim(JwtClaimTypes.Email, email),
            new Claim(JwtClaimTypes.Name, "Existing User")
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

        // The gate throws a typed exception so ExternalCallback can catch it and
        // redirect the user back to the web client's /login with the
        // OrgMembershipRequired errorCode. No SsoUser link is written and no auth
        // session is established.
        var ex = await Assert.ThrowsAsync<SsoAuthnRequiresOrgMembershipException>(async () => await task);
        Assert.Equal(orgId, ex.OrganizationId);
        Assert.Equal("Org", ex.OrganizationDisplayName);
        Assert.Equal(email, ex.UserEmail);
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
