using System.Globalization;
using System.Security.Claims;
using Bit.Admin;
using Bit.Admin.Auth.Controllers;
using Bit.Admin.Auth.IdentityServer;
using Bit.Admin.Auth.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Admin.Test.Auth.Controllers;

public class LoginControllerTests
{
    [Fact]
    public void Sso_ReturnsNotFound_WhenOidcDisabled()
    {
        var controller = BuildController(oidcEnabled: false);

        var result = controller.Sso(returnUrl: "/home");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task SsoSignIn_ReturnsNotFound_WhenOidcDisabled()
    {
        var controller = BuildController(oidcEnabled: false);

        var result = await controller.SsoSignIn(returnUrl: "/home");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task SsoSignIn_RedirectsWithError_WhenRemoteErrorProvided()
    {
        var controller = BuildController(oidcEnabled: true);
        AttachAuthenticationServices(controller);

        var result = await controller.SsoSignIn(returnUrl: "/home", remoteError: "access_denied");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal(5, redirect.RouteValues!["error"]);
    }

    [Fact]
    public void Index_Get_SetsSsoEnabledFromSettings()
    {
        var controller = BuildController(oidcEnabled: true);

        var result = controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<LoginModel>(view.Model);
        Assert.True(model.SsoEnabled);
        Assert.Equal("SSO", model.SsoDisplayName);
    }

    [Fact]
    public void Index_Get_SetsSsoDisabled_WhenOidcMissing()
    {
        var controller = BuildController(oidcEnabled: false);

        var result = controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<LoginModel>(view.Model);
        Assert.False(model.SsoEnabled);
    }

    [Fact]
    public async Task Logout_LocalOnly_WhenNoSsoMarkerClaim()
    {
        var controller = BuildController(oidcEnabled: true);
        SetUser(controller, new Claim(ClaimTypes.Email, "you@example.com"));

        var result = await controller.Logout();

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/login?success=1", redirect.Url);
    }

    [Fact]
    public async Task Logout_TriggersRpInitiatedLogout_WhenSsoMarkerClaimPresent()
    {
        var controller = BuildController(oidcEnabled: true);
        SetUser(controller,
            new Claim(ClaimTypes.Email, "you@example.com"),
            new Claim(AdminAuthenticationSchemes.AuthMethodClaimType, AdminAuthenticationSchemes.AuthMethodSso));

        var result = await controller.Logout();

        var signOut = Assert.IsType<SignOutResult>(result);
        Assert.Contains(AdminAuthenticationSchemes.UpstreamOidc, signOut.AuthenticationSchemes);
        Assert.NotNull(signOut.Properties?.RedirectUri);
    }

    private static void SetUser(LoginController controller, params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, authenticationType: "TestAuth");
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.Url = Substitute.For<IUrlHelper>();
        controller.Url.Action(Arg.Any<UrlActionContext>()).Returns("/login?success=1");
    }

    private static IAuthenticationService AttachAuthenticationServices(LoginController controller)
    {
        var authService = Substitute.For<IAuthenticationService>();
        var services = new ServiceCollection();
        services.AddSingleton(authService);
        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.Url = Substitute.For<IUrlHelper>();
        return authService;
    }

    private static void StubExternalPrincipal(IAuthenticationService authService, params Claim[] claims)
    {
        // Default auth_time to "now" so tests exercise the happy path unless overridden.
        var enriched = claims.Concat([
            new Claim("auth_time", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture))
        ]).ToArray();
        var identity = new ClaimsIdentity(enriched, authenticationType: "oidc");
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), IdentityConstants.ExternalScheme);
        authService.AuthenticateAsync(Arg.Any<HttpContext>(), IdentityConstants.ExternalScheme)
            .Returns(AuthenticateResult.Success(ticket));
    }

    private static void StubExternalPrincipalRaw(IAuthenticationService authService, params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, authenticationType: "oidc");
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), IdentityConstants.ExternalScheme);
        authService.AuthenticateAsync(Arg.Any<HttpContext>(), IdentityConstants.ExternalScheme)
            .Returns(AuthenticateResult.Success(ticket));
    }

    [Fact]
    public async Task SsoSignIn_RedirectsError5_WhenExternalAuthFails()
    {
        var controller = BuildController(oidcEnabled: true, out _, out _);
        var authService = AttachAuthenticationServices(controller);
        authService.AuthenticateAsync(Arg.Any<HttpContext>(), IdentityConstants.ExternalScheme)
            .Returns(AuthenticateResult.NoResult());

        var result = await controller.SsoSignIn();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(5, redirect.RouteValues!["error"]);
    }

    [Fact]
    public async Task SsoSignIn_RedirectsError5_WhenEmailClaimMissing()
    {
        var controller = BuildController(oidcEnabled: true, out _, out _);
        var authService = AttachAuthenticationServices(controller);
        StubExternalPrincipal(authService, new Claim("email_verified", "true"));

        var result = await controller.SsoSignIn();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(5, redirect.RouteValues!["error"]);
        await authService.Received(1).SignOutAsync(
            Arg.Any<HttpContext>(), IdentityConstants.ExternalScheme, Arg.Any<AuthenticationProperties>());
    }

    [Fact]
    public async Task SsoSignIn_RedirectsError5_WhenEmailVerifiedIsFalse()
    {
        var controller = BuildController(oidcEnabled: true, out _, out _);
        var authService = AttachAuthenticationServices(controller);
        StubExternalPrincipal(authService,
            new Claim("email", "you@example.com"),
            new Claim("email_verified", "false"));

        var result = await controller.SsoSignIn();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(5, redirect.RouteValues!["error"]);
    }

    [Fact]
    public async Task SsoSignIn_RedirectsError5_WhenEmailVerifiedMissingAndRequired()
    {
        var controller = BuildController(oidcEnabled: true, out _, out _);
        var authService = AttachAuthenticationServices(controller);
        StubExternalPrincipal(authService, new Claim("email", "you@example.com"));

        var result = await controller.SsoSignIn();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(5, redirect.RouteValues!["error"]);
    }

    [Fact]
    public async Task SsoSignIn_RedirectsError5_WhenEmailNotOnAllowlist()
    {
        var controller = BuildController(oidcEnabled: true, out var signInManager, out var userManager);
        var authService = AttachAuthenticationServices(controller);
        StubExternalPrincipal(authService,
            new Claim("email", "stranger@example.com"),
            new Claim("email_verified", "true"));
        userManager.FindByEmailAsync("stranger@example.com").Returns((IdentityUser)null);

        var result = await controller.SsoSignIn();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(5, redirect.RouteValues!["error"]);
        await signInManager.DidNotReceive().SignInWithClaimsAsync(
            Arg.Any<IdentityUser>(), Arg.Any<AuthenticationProperties>(), Arg.Any<IEnumerable<Claim>>());
    }

    [Theory]
    [InlineData("1")]
    [InlineData("0")]
    [InlineData("yes")]
    [InlineData("")]
    public async Task SsoSignIn_RedirectsError5_WhenEmailVerifiedIsNonBoolean(string value)
    {
        var controller = BuildController(oidcEnabled: true, out _, out _);
        var authService = AttachAuthenticationServices(controller);
        StubExternalPrincipal(authService,
            new Claim("email", "you@example.com"),
            new Claim("email_verified", value));

        var result = await controller.SsoSignIn();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(5, redirect.RouteValues!["error"]);
    }

    [Fact]
    public async Task SsoSignIn_RedirectsError5_WhenAuthTimeMissing()
    {
        var controller = BuildController(oidcEnabled: true, out _, out _);
        var authService = AttachAuthenticationServices(controller);
        StubExternalPrincipalRaw(authService,
            new Claim("email", "you@example.com"),
            new Claim("email_verified", "true"));

        var result = await controller.SsoSignIn();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(5, redirect.RouteValues!["error"]);
    }

    [Fact]
    public async Task SsoSignIn_RedirectsError5_WhenAuthTimeExceedsMaxAge()
    {
        var controller = BuildController(oidcEnabled: true, out _, out _);
        var authService = AttachAuthenticationServices(controller);
        var stale = DateTimeOffset.UtcNow.AddSeconds(-AdminAuthenticationSchemes.MaxIdpAuthAgeSeconds - 60)
            .ToUnixTimeSeconds();
        StubExternalPrincipalRaw(authService,
            new Claim("email", "you@example.com"),
            new Claim("email_verified", "true"),
            new Claim("auth_time", stale.ToString(CultureInfo.InvariantCulture)));

        var result = await controller.SsoSignIn();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(5, redirect.RouteValues!["error"]);
    }

    [Fact]
    public async Task SsoSignIn_SignsInWithSsoMarker_OnSuccess()
    {
        var controller = BuildController(oidcEnabled: true, out var signInManager, out var userManager);
        var authService = AttachAuthenticationServices(controller);
        var user = new IdentityUser { Email = "you@example.com" };
        StubExternalPrincipal(authService,
            new Claim("email", "you@example.com"),
            new Claim("email_verified", "true"));
        userManager.FindByEmailAsync("you@example.com").Returns(user);

        var result = await controller.SsoSignIn();

        Assert.IsType<RedirectToActionResult>(result);
        await signInManager.Received(1).SignInWithClaimsAsync(
            user,
            Arg.Any<AuthenticationProperties>(),
            Arg.Is<IEnumerable<Claim>>(claims => claims.Any(c =>
                c.Type == AdminAuthenticationSchemes.AuthMethodClaimType &&
                c.Value == AdminAuthenticationSchemes.AuthMethodSso)));
    }

    [Fact]
    public async Task SsoSignIn_RedirectsToHome_WhenReturnUrlNotLocal()
    {
        var controller = BuildController(oidcEnabled: true, out _, out var userManager);
        var authService = AttachAuthenticationServices(controller);
        controller.Url.IsLocalUrl("https://evil.example.com").Returns(false);
        var user = new IdentityUser { Email = "you@example.com" };
        StubExternalPrincipal(authService,
            new Claim("email", "you@example.com"),
            new Claim("email_verified", "true"));
        userManager.FindByEmailAsync("you@example.com").Returns(user);

        var result = await controller.SsoSignIn(returnUrl: "https://evil.example.com");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Home", redirect.ControllerName);
    }

    private static LoginController BuildController(bool oidcEnabled) =>
        BuildController(oidcEnabled, out _, out _);

    private static LoginController BuildController(
        bool oidcEnabled,
        out PasswordlessSignInManager<IdentityUser> signInManager,
        out UserManager<IdentityUser> userManager)
    {
        var settings = new AdminSettings();
        if (oidcEnabled)
        {
            settings.Oidc = new AdminSettings.OidcSettings
            {
                Authority = "https://idp.example.com",
                ClientId = "admin-portal",
                ClientSecret = "supersecret"
            };
        }
        else
        {
            settings.Oidc = null;
        }

        var userStore = Substitute.For<IUserStore<IdentityUser>>();
        userManager = Substitute.For<UserManager<IdentityUser>>(
            userStore,
            Substitute.For<IOptions<IdentityOptions>>(),
            Substitute.For<IPasswordHasher<IdentityUser>>(),
            Enumerable.Empty<IUserValidator<IdentityUser>>(),
            Enumerable.Empty<IPasswordValidator<IdentityUser>>(),
            Substitute.For<ILookupNormalizer>(),
            Substitute.For<IdentityErrorDescriber>(),
            Substitute.For<IServiceProvider>(),
            Substitute.For<ILogger<UserManager<IdentityUser>>>());

        signInManager = Substitute.For<PasswordlessSignInManager<IdentityUser>>(
            userManager,
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<IdentityUser>>(),
            Substitute.For<IOptions<IdentityOptions>>(),
            Substitute.For<ILogger<SignInManager<IdentityUser>>>(),
            Substitute.For<IAuthenticationSchemeProvider>(),
            Substitute.For<IUserConfirmation<IdentityUser>>(),
            Substitute.For<Bit.Core.Services.IMailService>());

        return new LoginController(signInManager, userManager, Options.Create(settings),
            Substitute.For<ILogger<LoginController>>());
    }
}
