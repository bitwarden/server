using System.Security.Claims;
using Bit.Core;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication;

namespace Bitwarden.License.Test.Sso.IntegrationTest.Utilities;

/// <summary>
/// Creates a mock for use in tests requiring a valid external authentication result.
/// </summary>
internal static class MockSuccessfulAuthResult
{
    /// <summary>
    /// Since this tests the external Authentication flow, only the OrganizationId is strictly required.
    /// However, some tests may require additional claims to be present, so they can be optionally added.
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="providerUserId"></param>
    /// <param name="email"></param>
    /// <param name="name"></param>
    /// <param name="acrValue"></param>
    /// <param name="userIdentifier"></param>
    /// <returns></returns>
    public static AuthenticateResult Build(
            Guid organizationId,
            string? providerUserId,
            string? email,
            string? name = null,
            string? acrValue = null,
            string? userIdentifier = null)
    {
        var claims = new List<Claim>();

        if (!string.IsNullOrEmpty(email))
        {
            claims.Add(new Claim(JwtClaimTypes.Email, email));
        }

        if (!string.IsNullOrEmpty(providerUserId))
        {
            claims.Add(new Claim(JwtClaimTypes.Subject, providerUserId));
        }

        if (!string.IsNullOrEmpty(name))
        {
            claims.Add(new Claim(JwtClaimTypes.Name, name));
        }

        if (!string.IsNullOrEmpty(acrValue))
        {
            claims.Add(new Claim(JwtClaimTypes.AuthenticationContextClassReference, acrValue));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "External"));
        var properties = new AuthenticationProperties
        {
            Items =
            {
                ["scheme"] = organizationId.ToString(),
                ["return_url"] = "~/",
                ["state"] = "test-state",
                ["user_identifier"] = userIdentifier ?? string.Empty
            }
        };

        var ticket = new AuthenticationTicket(
            principal,
            properties,
            AuthenticationSchemes.BitwardenExternalCookieAuthenticationScheme);

        return AuthenticateResult.Success(ticket);
    }
}