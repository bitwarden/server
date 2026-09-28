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
