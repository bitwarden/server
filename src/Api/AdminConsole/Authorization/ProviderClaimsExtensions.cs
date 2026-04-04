using System.Security.Claims;
using Bit.Core.AdminConsole.Context;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Auth.Identity;

namespace Bit.Api.AdminConsole.Authorization;

public static class ProviderClaimsExtensions
{
    /// <summary>
    /// Parses a user's claims and returns an object representing their claims for the specified provider.
    /// </summary>
    /// <param name="user">The user who has the claims.</param>
    /// <param name="providerId">The providerId to look for in the claims.</param>
    /// <returns>
    /// A <see cref="CurrentContextProvider"/> representing the user's claims for that provider, or null
    /// if the user does not have any claims for that provider.
    /// </returns>
    public static CurrentContextProvider? GetCurrentContextProvider(this ClaimsPrincipal user, Guid providerId)
    {
        var claimsDict = user.Claims
            .GroupBy(c => c.Type)
            .ToDictionary(
                c => c.Key,
                c => c.ToList());

        bool hasClaim(string claimType) =>
            claimsDict.TryGetValue(claimType, out var claims) &&
            claims.Any(c => Guid.TryParse(c.Value, out var id) && id == providerId);

        if (hasClaim(Claims.ProviderAdmin))
        {
            return new CurrentContextProvider { Id = providerId, Type = ProviderUserType.ProviderAdmin };
        }

        if (hasClaim(Claims.ProviderServiceUser))
        {
            return new CurrentContextProvider { Id = providerId, Type = ProviderUserType.ServiceUser };
        }

        return null;
    }
}
