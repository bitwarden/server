using System.Security.Claims;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Entities;
using Bit.IntegrationTestCommon;
using Bit.IntegrationTestCommon.Factories;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication;
using Xunit;

#nullable enable

namespace Bit.Identity.IntegrationTest.Endpoints;

public class SsoExternalCallbackTests
{
    private const string UserEmail = "sso_external_callback_user@email.com";

    [Theory]
    [InlineData(JwtClaimTypes.Subject)]
    [InlineData(ClaimTypes.NameIdentifier)]
    public async Task ExternalCallback_TicketFromSsoChallengeScheme_SignsIn(string userIdClaimType)
    {
        // Arrange
        var (factory, user) = await CreateFactoryWithUserAsync();
        var cookie = await SignInExternalCookieAsync(factory, userIdClaimType, user.Id.ToString(),
            challengeScheme: "sso");

        // Act
        var context = await factory.GetSsoExternalCallbackAsync(cookie.Name, cookie.Value);
        var tokenSubject = await factory.GetAccessTokenSubjectAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.True(factory.SetsIdentityServerSessionCookie(context));
        Assert.Equal(user.Id.ToString(), tokenSubject);
    }

    [Theory]
    [InlineData(JwtClaimTypes.Subject)]
    [InlineData(ClaimTypes.NameIdentifier)]
    public async Task ExternalCallback_TicketFromOtherScheme_DoesNotSignIn(string userIdClaimType)
    {
        // Arrange
        var (factory, user) = await CreateFactoryWithUserAsync();
        var cookie = await SignInExternalCookieAsync(factory, userIdClaimType, user.Id.ToString(),
            challengeScheme: Guid.NewGuid().ToString());

        // Act
        var context = await factory.GetSsoExternalCallbackAsync(cookie.Name, cookie.Value);
        var tokenSubject = await factory.GetAccessTokenSubjectAsync(context);

        // Assert
        Assert.Null(tokenSubject);
        Assert.False(factory.SetsIdentityServerSessionCookie(context));
        Assert.NotEqual(StatusCodes.Status302Found, context.Response.StatusCode);
    }

    private static async Task<(IdentityApplicationFactory Factory, User User)> CreateFactoryWithUserAsync()
    {
        var factory = new IdentityApplicationFactory();
        var user = await factory.RegisterNewIdentityFactoryUserAsync(new RegisterFinishRequestModel
        {
            Email = UserEmail,
            UserAsymmetricKeys = new KeysRequestModel
            {
                PublicKey = "public-key",
                EncryptedPrivateKey = "encrypted-private-key",
            },
        });
        return (factory, user);
    }

    /// <summary>
    /// Writes Identity's external cookie the way a remote handler does at the end of an external login:
    /// the external user id under <paramref name="userIdClaimType"/>, plus the properties set at challenge time.
    /// </summary>
    private static Task<(string Name, string Value)> SignInExternalCookieAsync(
        IdentityApplicationFactory factory,
        string userIdClaimType,
        string userId,
        string challengeScheme)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(userIdClaimType, userId)], "External"));
        var properties = new AuthenticationProperties
        {
            Items =
            {
                { "scheme", challengeScheme },
                { "return_url", "~/" },
            },
        };

        return ExternalCookieTestHelpers.SignInCookieAsync(
            factory.Services, factory.GetIdentityExternalScheme(), principal, properties);
    }
}
