using System.Security.Claims;
using Bit.Core;
using Bit.Core.Auth.IdentityServer;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Services;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using Bit.Identity.Controllers;
using Bitwarden.Server.Sdk.Features;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using AuthenticationOptions = Duende.IdentityServer.Configuration.AuthenticationOptions;

#nullable enable

namespace Bit.Identity.Test.Auth.Controllers;

public class OidcControllerTests
{
    private const string CookieScheme = "idsrv";
    private const string CsrfCookieName = "bw.oidc_consent_csrf";
    private const string PartnerClientId = "partner.acme";
    private const string ReturnUrl = "/connect/authorize/callback?client_id=partner.acme&state=abc";

    private readonly IIdentityServerInteractionService _interaction =
        Substitute.For<IIdentityServerInteractionService>();
    private readonly IOpenIdConnectPartnerStore _partnerStore = Substitute.For<IOpenIdConnectPartnerStore>();
    private readonly IDataProtectorTokenFactory<OAuthConsentTokenable> _consentTokenFactory =
        Substitute.For<IDataProtectorTokenFactory<OAuthConsentTokenable>>();
    private readonly IOAuthConsentNonceStore _nonceStore = Substitute.For<IOAuthConsentNonceStore>();
    private readonly IFeatureService _featureService = Substitute.For<IFeatureService>();
    private readonly IAuthenticationService _authenticationService = Substitute.For<IAuthenticationService>();
    private readonly IDataProtectionProvider _dataProtectionProvider = new EphemeralDataProtectionProvider();
    private readonly GlobalSettings _globalSettings = new();
    private readonly OidcController _sut;

    public OidcControllerTests()
    {
        _globalSettings.BaseServiceUri.Vault = "https://vault.example.com";
        _featureService.IsEnabled(FeatureFlagKeys.OpenIdConnectProvider).Returns(true);
        _partnerStore.IsRegistered(PartnerClientId).Returns(true);
        _partnerStore.GetByClientId(PartnerClientId)
            .Returns(new OpenIdConnectPartner { ClientId = PartnerClientId, Name = "Acme" });

        _sut = new OidcController(_interaction, _partnerStore, _consentTokenFactory, _nonceStore,
            _featureService, _globalSettings, _dataProtectionProvider,
            Substitute.For<ILogger<OidcController>>());

        var schemeProvider = Substitute.For<IAuthenticationSchemeProvider>();
        schemeProvider.GetDefaultAuthenticateSchemeAsync()
            .Returns(new AuthenticationScheme(CookieScheme, CookieScheme, typeof(IAuthenticationHandler)));

        var services = new ServiceCollection();
        services.AddSingleton(_authenticationService);
        services.AddSingleton(schemeProvider);
        services.AddSingleton(new IdentityServerOptions
        {
            Authentication = new AuthenticationOptions { CookieAuthenticationScheme = CookieScheme },
        });

        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() },
        };
    }

    [Fact]
    public async Task ConsentCallback_ValidCookie_SignsInShortLivedAndGrantsConsent()
    {
        SetPartnerContext();
        var token = SetValidConsentToken();
        SetRequestCookie(CsrfCookieName, CsrfCookieFor(ReturnUrl));

        var result = await _sut.ConsentCallback("token", ReturnUrl);

        Assert.Equal(ReturnUrl, Assert.IsType<RedirectResult>(result).Url);
        // F1: the session is non-persistent and short-lived (just long enough to finish the authorize).
        await _authenticationService.Received(1).SignInAsync(
            Arg.Any<HttpContext>(),
            CookieScheme,
            Arg.Any<ClaimsPrincipal>(),
            Arg.Is<AuthenticationProperties>(p => p.IsPersistent == false && p.ExpiresUtc != null));
        await _interaction.Received(1).GrantConsentAsync(
            Arg.Any<AuthorizationRequest>(), Arg.Any<ConsentResponse>(), token.UserId.ToString());
    }

    [Fact]
    public async Task ConsentCallback_MissingCsrfCookie_ThrowsAndDoesNotSignIn()
    {
        SetPartnerContext();
        SetValidConsentToken();
        // No browser-binding cookie present.

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ConsentCallback("token", ReturnUrl));

        await _authenticationService.DidNotReceiveWithAnyArgs()
            .SignInAsync(default!, default, default!, default);
    }

    [Fact]
    public async Task ConsentCallback_CsrfCookieBoundToDifferentRequest_ThrowsAndDoesNotSignIn()
    {
        SetPartnerContext();
        SetValidConsentToken();
        SetRequestCookie(CsrfCookieName, CsrfCookieFor("/connect/authorize/callback?client_id=partner.acme&state=other"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ConsentCallback("token", ReturnUrl));

        await _authenticationService.DidNotReceiveWithAnyArgs()
            .SignInAsync(default!, default, default!, default);
    }

    [Fact]
    public async Task Login_PartnerRequest_SetsCsrfCookieAndRedirectsToVault()
    {
        SetPartnerContext();

        var result = await _sut.Login(ReturnUrl);

        Assert.StartsWith("https://vault.example.com/#/oauth/authorize",
            Assert.IsType<RedirectResult>(result).Url);
        Assert.Contains(_sut.HttpContext.Response.Headers.SetCookie,
            header => header != null && header.Contains(CsrfCookieName));
    }

    private void SetPartnerContext()
    {
        var context = new AuthorizationRequest { Client = new Client { ClientId = PartnerClientId } };
        context.Parameters.Add("scope", "openid profile email");
        _interaction.GetAuthorizationContextAsync(ReturnUrl).Returns(context);
    }

    private OAuthConsentTokenable SetValidConsentToken()
    {
        var token = new OAuthConsentTokenableFactory(TimeProvider.System)
            .CreateToken(Guid.NewGuid(), ReturnUrl, "nonce-123");
        _consentTokenFactory.TryUnprotect(Arg.Any<string>(), out Arg.Any<OAuthConsentTokenable>()!)
            .Returns(call => { call[1] = token; return true; });
        _nonceStore.TryConsumeAsync(token.Nonce).Returns(true);
        return token;
    }

    private string CsrfCookieFor(string returnUrl) =>
        _dataProtectionProvider.CreateProtector("OidcConsentCsrf")
            .Protect(OAuthConsentTokenable.HashAuthorizationRequest(returnUrl));

    private void SetRequestCookie(string name, string value) =>
        _sut.HttpContext.Request.Headers.Cookie = $"{name}={value}";
}
