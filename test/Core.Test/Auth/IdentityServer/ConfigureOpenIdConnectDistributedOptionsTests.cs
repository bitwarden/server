using System.Security.Claims;
using System.Security.Cryptography;
using Bit.Core.Auth.IdentityServer;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

#nullable enable

namespace Bit.Core.Test.Auth.IdentityServer;

public class ConfigureOpenIdConnectDistributedOptionsTests
{
    private const string IdentityScheme = AuthenticationSchemes.IdentityExternalCookie;
    private const string SsoScheme = AuthenticationSchemes.SsoExternalCookie;

    // One cache and one data protection key ring for every scheme, as the deployed apps share them.
    private readonly ConfigureOpenIdConnectDistributedOptions _sut = new(
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
        new EphemeralDataProtectionProvider(),
        new IdentityServerOptions());

    [Theory]
    [InlineData(IdentityScheme)]
    [InlineData(SsoScheme)]
    public void PostConfigure_ExternalCookieScheme_UsesSchemeNameAsCookieName(string scheme)
    {
        var options = Configure(scheme);

        Assert.Equal(scheme, options.Cookie.Name);
        Assert.IsType<DistributedCacheTicketDataFormatter>(options.TicketDataFormat);
        Assert.IsType<DistributedCacheTicketStore>(options.SessionStore);
    }

    [Fact]
    public void PostConfigure_OtherScheme_KeepsCookieAndTicketSettings()
    {
        var options = new CookieAuthenticationOptions();
        var cookieName = options.Cookie.Name;

        _sut.PostConfigure("other", options);

        Assert.Equal(cookieName, options.Cookie.Name);
        Assert.Null(options.TicketDataFormat);
        Assert.Null(options.SessionStore);
        Assert.IsType<DistributedCacheCookieManager>(options.CookieManager);
    }

    [Theory]
    [InlineData(SsoScheme, IdentityScheme)]
    [InlineData(IdentityScheme, SsoScheme)]
    public void TicketDataFormat_TicketFromOneScheme_DoesNotUnprotectWithOtherScheme(
        string writerScheme, string readerScheme)
    {
        var writer = Configure(writerScheme);
        var reader = Configure(readerScheme);
        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(JwtClaimTypes.Subject, Guid.NewGuid().ToString())], "External")),
            writerScheme);

        var protectedTicket = writer.TicketDataFormat.Protect(ticket);

        Assert.NotNull(writer.TicketDataFormat.Unprotect(protectedTicket));
        Assert.ThrowsAny<CryptographicException>(() => reader.TicketDataFormat.Unprotect(protectedTicket));
    }

    private CookieAuthenticationOptions Configure(string scheme)
    {
        var options = new CookieAuthenticationOptions();
        _sut.PostConfigure(scheme, options);
        return options;
    }
}
