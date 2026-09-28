using Bit.Admin;
using Bit.Admin.IdentityServer;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Admin.Test.IdentityServer;

public class ServiceCollectionExtensionsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddPasswordlessIdentityServices_ThrowsOnNonPositiveSessionTimeout(int minutes)
    {
        var services = new ServiceCollection();
        var globalSettings = new GlobalSettings { ProjectName = "Admin" };
        var adminSettings = new AdminSettings { SessionTimeoutMinutes = minutes };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddPasswordlessIdentityServices<ReadOnlyEnvIdentityUserStore>(globalSettings, adminSettings));

        Assert.Contains("SessionTimeoutMinutes", ex.Message);
    }

    [Fact]
    public async Task ApplicationCookie_OnRedirectToLogin_AppendsError6_WhenAuthCookiePresent()
    {
        var options = BuildConfiguredCookieOptions();
        var ctx = BuildRedirectContext(options, includeAuthCookie: true);

        await options.Events.OnRedirectToLogin(ctx);

        Assert.Contains("error=6", ctx.RedirectUri);
    }

    [Fact]
    public async Task ApplicationCookie_OnRedirectToLogin_DoesNotAppendError6_ForFirstTimeVisitor()
    {
        var options = BuildConfiguredCookieOptions();
        var ctx = BuildRedirectContext(options, includeAuthCookie: false);

        await options.Events.OnRedirectToLogin(ctx);

        Assert.DoesNotContain("error=6", ctx.RedirectUri);
    }

    [Fact]
    public async Task SuppressRenewAfter_SetsShouldRenewFalseAfterInner()
    {
        // Guards against SecurityStampValidator (or any other inner delegate) setting
        // ShouldRenew=true, which would otherwise cause CookieAuthenticationHandler to
        // rewrite IssuedUtc/ExpiresUtc even though the operator asked for a fixed lifetime
        // via SessionSliding=false.
        var innerRan = false;
        Func<CookieValidatePrincipalContext, Task> inner = ctx =>
        {
            innerRan = true;
            ctx.ShouldRenew = true;
            return Task.CompletedTask;
        };
        var wrapped = Bit.Admin.IdentityServer.ServiceCollectionExtensions.SuppressRenewAfter(inner);
        var context = BuildValidateContext(shouldRenew: false);

        await wrapped(context);

        Assert.True(innerRan);
        Assert.False(context.ShouldRenew);
    }

    [Fact]
    public async Task SuppressRenewAfter_SetsShouldRenewFalseEvenWhenInnerDidNot()
    {
        Func<CookieValidatePrincipalContext, Task> inner = _ => Task.CompletedTask;
        var wrapped = Bit.Admin.IdentityServer.ServiceCollectionExtensions.SuppressRenewAfter(inner);
        var context = BuildValidateContext(shouldRenew: true);

        await wrapped(context);

        Assert.False(context.ShouldRenew);
    }

    [Fact]
    public void AddAdminUpstreamOidc_ThrowsWhenEmailClaimTypeIsNull()
    {
        var services = new ServiceCollection();
        var adminSettings = new AdminSettings
        {
            Oidc = new AdminSettings.OidcSettings
            {
                Authority = "https://idp.example.com",
                ClientId = "id",
                ClientSecret = "secret",
                EmailClaimType = null,
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddAdminUpstreamOidc(adminSettings));

        Assert.Contains("EmailClaimType", ex.Message);
    }

    private static CookieAuthenticationOptions BuildConfiguredCookieOptions()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        var globalSettings = new GlobalSettings { ProjectName = "Admin" };
        var adminSettings = new AdminSettings { SessionTimeoutMinutes = 60 };
        services.AddPasswordlessIdentityServices<ReadOnlyEnvIdentityUserStore>(globalSettings, adminSettings);
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        var provider = services.BuildServiceProvider();
        var monitor = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>();
        return monitor.Get(IdentityConstants.ApplicationScheme);
    }

    private static CookieValidatePrincipalContext BuildValidateContext(bool shouldRenew)
    {
        var scheme = new AuthenticationScheme(
            IdentityConstants.ApplicationScheme,
            IdentityConstants.ApplicationScheme,
            typeof(CookieAuthenticationHandler));
        var identity = new System.Security.Claims.ClaimsIdentity(authenticationType: "TestAuth");
        var ticket = new AuthenticationTicket(
            new System.Security.Claims.ClaimsPrincipal(identity),
            new AuthenticationProperties(),
            IdentityConstants.ApplicationScheme);
        var ctx = new CookieValidatePrincipalContext(
            new DefaultHttpContext(), scheme, new CookieAuthenticationOptions(), ticket)
        {
            ShouldRenew = shouldRenew,
        };
        return ctx;
    }

    private static RedirectContext<CookieAuthenticationOptions> BuildRedirectContext(
        CookieAuthenticationOptions options, bool includeAuthCookie)
    {
        var httpContext = new DefaultHttpContext();
        if (includeAuthCookie)
        {
            httpContext.Request.Headers.Cookie = $"{options.Cookie.Name}=stale-value";
        }
        var scheme = new Microsoft.AspNetCore.Authentication.AuthenticationScheme(
            IdentityConstants.ApplicationScheme,
            IdentityConstants.ApplicationScheme,
            typeof(CookieAuthenticationHandler));
        return new RedirectContext<CookieAuthenticationOptions>(
            httpContext,
            scheme,
            options,
            new Microsoft.AspNetCore.Authentication.AuthenticationProperties(),
            "/login?returnUrl=%2F");
    }
}
