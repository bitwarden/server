using System.Security.Claims;
using Bit.Admin;
using Bit.Admin.Auth.IdentityServer;
using Bit.Admin.IdentityServer;
using Bit.Core.Settings;
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
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddAdminUpstreamOidc(
                BuildAdminSettings(emailClaimType: null),
                BuildGlobalSettings(selfHosted: false)));

        Assert.Contains("EmailClaimType", ex.Message);
    }

    [Fact]
    public void AddAdminUpstreamOidc_SelfHosted_ThrowsWhenAdminsEmpty()
    {
        // Self-hosted uses AdminSettings:Admins as the sign-in tenancy gate. Without it every
        // SSO sign-in would be rejected at the gate.
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddAdminUpstreamOidc(
                BuildAdminSettings(admins: null, groupPrefix: "us-prd-adminportal-"),
                BuildGlobalSettings(selfHosted: true)));

        Assert.Contains("Admins", ex.Message);
        Assert.Contains("self-hosted", ex.Message);
    }

    [Fact]
    public void AddAdminUpstreamOidc_Cloud_ThrowsWhenGroupPrefixEmpty()
    {
        // Cloud uses "has at least one mapped role" as the sign-in tenancy gate, which requires
        // AdminSettings:Oidc:GroupPrefix. Without it every SSO sign-in would be rejected.
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddAdminUpstreamOidc(
                BuildAdminSettings(admins: "admin@example.com", groupPrefix: null),
                BuildGlobalSettings(selfHosted: false)));

        Assert.Contains("GroupPrefix", ex.Message);
        Assert.Contains("cloud", ex.Message);
    }

    [Fact]
    public void AddAdminUpstreamOidc_SelfHosted_Succeeds_WhenAdminsConfigured()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddAdminUpstreamOidc(
            BuildAdminSettings(admins: "admin@example.com", groupPrefix: null),
            BuildGlobalSettings(selfHosted: true));
    }

    [Fact]
    public void AddAdminUpstreamOidc_Cloud_Succeeds_WhenGroupPrefixConfigured()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddAdminUpstreamOidc(
            BuildAdminSettings(admins: null, groupPrefix: "us-prd-adminportal-"),
            BuildGlobalSettings(selfHosted: false));
    }

    private static AdminSettings BuildAdminSettings(
        string admins = "admin@example.com",
        string groupPrefix = "us-prd-adminportal-",
        string emailClaimType = "email") =>
        new()
        {
            Admins = admins,
            Oidc = new AdminSettings.OidcSettings
            {
                Authority = "https://idp.example.com",
                ClientId = "id",
                ClientSecret = "secret",
                GroupPrefix = groupPrefix,
                EmailClaimType = emailClaimType,
            },
        };

    private static GlobalSettings BuildGlobalSettings(bool selfHosted) =>
        new() { SelfHosted = selfHosted };

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
        services.AddAdminUpstreamOidc(BuildAdminSettings(), BuildGlobalSettings(selfHosted: false));

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
