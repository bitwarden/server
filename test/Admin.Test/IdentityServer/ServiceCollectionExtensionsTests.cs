using System.Security.Claims;
using Bit.Admin;
using Bit.Admin.Auth.IdentityServer;
using Bit.Admin.IdentityServer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Admin.Test.IdentityServer;

public class ServiceCollectionExtensionsTests
{
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

    [Fact]
    public async Task AddAdminUpstreamOidc_OnTokenValidated_FailsWhenAuthTimeMissingAsync()
    {
        var options = BuildOidcOptions();
        var ctx = BuildTokenValidatedContext(options, authTimeUnixSeconds: null);

        await options.Events.TokenValidated(ctx);

        Assert.NotNull(ctx.Result);
        Assert.NotNull(ctx.Result.Failure);
        Assert.Contains(JwtRegisteredClaimNames.AuthTime, ctx.Result.Failure.Message);
    }

    [Fact]
    public async Task AddAdminUpstreamOidc_OnTokenValidated_FailsWhenAuthTimeOlderThanMaxAgePlusClockSkewAsync()
    {
        var options = BuildOidcOptions();
        // MaxAge=0 + ClockSkew=120s - an auth_time 200s in the past exceeds the tolerance.
        var authTime = DateTimeOffset.UtcNow
            .AddSeconds(-(AdminSettings.OidcSettings.ClockSkewSeconds + 80))
            .ToUnixTimeSeconds();

        var ctx = BuildTokenValidatedContext(options, authTimeUnixSeconds: authTime);

        await options.Events.TokenValidated(ctx);

        Assert.NotNull(ctx.Result);
        Assert.NotNull(ctx.Result.Failure);
        Assert.Contains(JwtRegisteredClaimNames.AuthTime, ctx.Result.Failure.Message);
    }

    [Fact]
    public async Task AddAdminUpstreamOidc_OnTokenValidated_SucceedsWhenAuthTimeWithinClockSkewAsync()
    {
        var options = BuildOidcOptions();
        var authTime = DateTimeOffset.UtcNow
            .AddSeconds(-(AdminSettings.OidcSettings.ClockSkewSeconds - 10))
            .ToUnixTimeSeconds();

        var ctx = BuildTokenValidatedContext(options, authTimeUnixSeconds: authTime);

        await options.Events.TokenValidated(ctx);

        Assert.Null(ctx.Result);
    }

    private static OpenIdConnectOptions BuildOidcOptions()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAdminUpstreamOidc(new AdminSettings
        {
            Oidc = new AdminSettings.OidcSettings
            {
                Authority = "https://idp.example.com",
                ClientId = "id",
                ClientSecret = "secret",
            }
        });

        var sp = services.BuildServiceProvider();
        return sp.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(AdminAuthenticationSchemes.UpstreamOidc);
    }

    private static TokenValidatedContext BuildTokenValidatedContext(
        OpenIdConnectOptions options,
        long? authTimeUnixSeconds)
    {
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, "subject") };
        if (authTimeUnixSeconds is not null)
        {
            claims.Add(new Claim(
                JwtRegisteredClaimNames.AuthTime,
                authTimeUnixSeconds.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var scheme = new AuthenticationScheme(
            AdminAuthenticationSchemes.UpstreamOidc,
            AdminAuthenticationSchemes.UpstreamOidc,
            typeof(OpenIdConnectHandler));

        return new TokenValidatedContext(
            new DefaultHttpContext(),
            scheme,
            options,
            principal,
            new AuthenticationProperties());
    }
}
