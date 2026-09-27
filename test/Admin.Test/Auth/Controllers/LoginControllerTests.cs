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
    public async Task Logout_LocalOnly_WhenNoIdTokenStored()
    {
        // Passwordless sessions never store an id_token, so Logout should skip RP-initiated
        // logout and just redirect locally.
        var controller = BuildController(oidcEnabled: true);
        var authService = AttachAuthenticationServices(controller);
        StubCurrentAuth(authService, new AuthenticationProperties());
        controller.Url.Action(Arg.Any<UrlActionContext>()).Returns("/login?success=1");

        var result = await controller.Logout();

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/login?success=1", redirect.Url);
    }

    [Fact]
    public async Task Logout_TriggersRpInitiatedLogout_WhenIdTokenPresent()
    {
        // SSO sessions store id_token during SsoSignIn; its presence is what tells Logout
        // to trigger RP-initiated logout, and its value is what gets attached as id_token_hint.
        var controller = BuildController(oidcEnabled: true);
        var authService = AttachAuthenticationServices(controller);
        var props = new AuthenticationProperties();
        props.StoreTokens([new AuthenticationToken { Name = "id_token", Value = "the-id-token" }]);
        StubCurrentAuth(authService, props);
        controller.Url.Action(Arg.Any<UrlActionContext>()).Returns("/login?success=1");

        var result = await controller.Logout();

        var signOut = Assert.IsType<SignOutResult>(result);
        Assert.Contains(AdminAuthenticationSchemes.UpstreamOidc, signOut.AuthenticationSchemes);
        Assert.NotNull(signOut.Properties?.RedirectUri);
        Assert.Equal("the-id-token", signOut.Properties!.GetTokenValue("id_token"));
    }

    [Fact]
    public async Task Logout_LocalOnly_WhenIdTokenPresentButOidcDisabled()
    {
        // Regression: if OIDC config is removed while an SSO-signed-in admin still holds a
        // valid cookie (with a stale id_token), SignOut against the now-unregistered scheme
        // would throw. Falling back to local logout is the safe behavior.
        var controller = BuildController(oidcEnabled: false);
        var authService = AttachAuthenticationServices(controller);
        var props = new AuthenticationProperties();
        props.StoreTokens([new AuthenticationToken { Name = "id_token", Value = "stale-token" }]);
        StubCurrentAuth(authService, props);
        controller.Url.Action(Arg.Any<UrlActionContext>()).Returns("/login?success=1");

        var result = await controller.Logout();

        Assert.IsType<RedirectResult>(result);
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

    private static void StubCurrentAuth(IAuthenticationService authService, AuthenticationProperties properties)
    {
        var identity = new ClaimsIdentity(authenticationType: "TestAuth");
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), properties, "TestAuth");
        authService.AuthenticateAsync(Arg.Any<HttpContext>(), null)
            .Returns(AuthenticateResult.Success(ticket));
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

    private static void StubExternalPrincipalWithTokens(
        IAuthenticationService authService, AuthenticationToken[] tokens, Claim[] claims)
    {
        var enriched = claims.Concat([
            new Claim("auth_time", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture))
        ]).ToArray();
        var identity = new ClaimsIdentity(enriched, authenticationType: "oidc");
        var props = new AuthenticationProperties();
        props.StoreTokens(tokens);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), props, IdentityConstants.ExternalScheme);
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
        var stale = DateTimeOffset.UtcNow
            .AddSeconds(-AdminSettings.OidcSettings.MaxIdpAuthAgeSeconds - AdminSettings.OidcSettings.ClockSkewSeconds - 60)
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
    public async Task SsoSignIn_StoresIdTokenOnAppCookie_OnSuccess()
    {
        // Presence of id_token on the app cookie is both (a) the marker that this is an SSO
        // session, so Logout will trigger RP-initiated logout, and (b) the value used as
        // id_token_hint on the end-session request. Assert it lands on the sign-in props.
        var controller = BuildController(oidcEnabled: true, out var signInManager, out var userManager);
        var authService = AttachAuthenticationServices(controller);
        var user = new IdentityUser { Email = "you@example.com" };
        StubExternalPrincipalWithTokens(authService,
            tokens: [new AuthenticationToken { Name = "id_token", Value = "the-id-token" }],
            claims: [
                new Claim("email", "you@example.com"),
                new Claim("email_verified", "true"),
            ]);
        userManager.FindByEmailAsync("you@example.com").Returns(user);

        var result = await controller.SsoSignIn();

        Assert.IsType<RedirectToActionResult>(result);
        await signInManager.Received(1).SignInWithClaimsAsync(
            user,
            Arg.Is<AuthenticationProperties>(p => p.GetTokenValue("id_token") == "the-id-token"),
            Arg.Any<IEnumerable<Claim>>());
    }

    [Fact]
    public async Task SsoSignIn_RedirectsToLocalReturnUrl_WhenLocal()
    {
        var controller = BuildController(oidcEnabled: true, out _, out var userManager);
        var authService = AttachAuthenticationServices(controller);
        controller.Url.IsLocalUrl("/dashboard").Returns(true);
        var user = new IdentityUser { Email = "you@example.com" };
        StubExternalPrincipal(authService,
            new Claim("email", "you@example.com"),
            new Claim("email_verified", "true"));
        userManager.FindByEmailAsync("you@example.com").Returns(user);

        var result = await controller.SsoSignIn(returnUrl: "/dashboard");

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/dashboard", redirect.Url);
    }

    [Fact]
    public async Task SsoSignIn_AcceptsAuthTimeSlightlyInFuture()
    {
        // Regression: a fast IdP clock should not lock out every SSO login.
        var controller = BuildController(oidcEnabled: true, out _, out var userManager);
        var authService = AttachAuthenticationServices(controller);
        var user = new IdentityUser { Email = "you@example.com" };
        var slightlyFuture = DateTimeOffset.UtcNow.AddSeconds(30).ToUnixTimeSeconds();
        StubExternalPrincipalRaw(authService,
            new Claim("email", "you@example.com"),
            new Claim("email_verified", "true"),
            new Claim("auth_time", slightlyFuture.ToString(CultureInfo.InvariantCulture)));
        userManager.FindByEmailAsync("you@example.com").Returns(user);

        var result = await controller.SsoSignIn();

        Assert.IsType<RedirectToActionResult>(result);
    }

    [Fact]
    public void Sso_ReturnsChallengeResultForUpstreamOidc_WhenEnabled()
    {
        var controller = BuildController(oidcEnabled: true);
        controller.Url = Substitute.For<IUrlHelper>();
        controller.Url.Action(Arg.Any<UrlActionContext>()).Returns("/login/sso-signin");

        var result = controller.Sso();

        var challenge = Assert.IsType<ChallengeResult>(result);
        Assert.Contains(AdminAuthenticationSchemes.UpstreamOidc, challenge.AuthenticationSchemes);
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
