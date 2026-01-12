using System.Security.Claims;
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
        return Build(organizationId.ToString(), providerUserId, email, name, acrValue, userIdentifier);
    }

    /// <summary>
    /// Overload that accepts a custom scheme string. Useful for testing invalid provider scenarios
    /// where the scheme is not a valid GUID.
    /// </summary>
    public static AuthenticateResult Build(
            string scheme,
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
                ["scheme"] = scheme,
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
