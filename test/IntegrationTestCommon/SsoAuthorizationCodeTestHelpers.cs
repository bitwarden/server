using System.Globalization;
using System.Security.Claims;
using Bit.IntegrationTestCommon.Factories;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.IntegrationTestCommon;

/// <summary>
/// Issues SSO authorization codes through the Identity host's real <see cref="IAuthorizationCodeStore"/>, so a
/// token request redeems them through the same store, serializer, and grant repository as a real SSO login.
/// </summary>
public static class SsoAuthorizationCodeTestHelpers
{
    public const string ClientId = "web";
    public const string RedirectUri = "https://localhost:8080/sso-connector.html";

    /// <summary>
    /// Stores an authorization code for an SSO login by <paramref name="userId"/> through
    /// <paramref name="organizationId"/>, and returns the code that a client sends to the token endpoint.
    /// </summary>
    /// <param name="codeVerifier">The PKCE verifier that the token request must send as <c>code_verifier</c>.</param>
    public static async Task<string> StoreSsoAuthorizationCodeAsync(
        this IdentityApplicationFactory factory,
        Guid userId,
        string email,
        Guid organizationId,
        string codeVerifier)
    {
        var authorizationCode = new AuthorizationCode
        {
            ClientId = ClientId,
            CreationTime = DateTime.UtcNow,
            Lifetime = (int)TimeSpan.FromMinutes(5).TotalSeconds,
            RedirectUri = RedirectUri,
            RequestedScopes = ["api", "offline_access"],
            CodeChallenge = codeVerifier.Sha256(),
            CodeChallengeMethod = OidcConstants.CodeChallengeMethods.Plain,
            Subject = BuildSsoSubject(userId, email, organizationId),
        };

        using var scope = factory.Services.CreateScope();
        var authorizationCodeStore = scope.ServiceProvider.GetRequiredService<IAuthorizationCodeStore>();
        return await authorizationCodeStore.StoreAuthorizationCodeAsync(authorizationCode);
    }

    private static ClaimsPrincipal BuildSsoSubject(Guid userId, string email, Guid organizationId)
    {
        return new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(JwtClaimTypes.Subject, userId.ToString()),
            new Claim(JwtClaimTypes.Name, email),
            new Claim(JwtClaimTypes.IdentityProvider, "sso"),
            new Claim("organizationId", organizationId.ToString()),
            new Claim(JwtClaimTypes.SessionId, "SOMETHING"),
            new Claim(JwtClaimTypes.AuthenticationMethod, "external"),
            new Claim(JwtClaimTypes.AuthenticationTime,
                new DateTimeOffset(DateTime.UtcNow.AddMinutes(-1)).ToUnixTimeSeconds()
                    .ToString(CultureInfo.InvariantCulture))
        ], "Duende.IdentityServer", JwtClaimTypes.Name, JwtClaimTypes.Role));
    }
}
