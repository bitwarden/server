#nullable enable

using System.Security.Claims;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Identity;

namespace Bit.Api.AdminConsole.Authorization;

public static class ClaimsExtensions
{
    public static CurrentContextOrganization? GetCurrentContextOrganization(this ClaimsPrincipal user, Guid organizationId)
    {
        var claimsDict = user.Claims
            .GroupBy(c => c.Type)
            .ToDictionary(c => c.Key, c => c.Select(v => v));

        var accessSecretsManager = claimsDict.TryGetValue(Claims.SecretsManagerAccess, out var value)
            ? value
                .Where(s => Guid.TryParse(s.Value, out _))
                .Select(s => new Guid(s.Value))
                .ToHashSet()
            : [];

        var role = claimsDict.GetRoleForOrganizationId(organizationId);
        if (!role.HasValue)
        {
            // Not an organization member
            return null;
        }

        return new CurrentContextOrganization
        {
            Id = organizationId,
            Type = role.Value,
            AccessSecretsManager = accessSecretsManager.Contains(organizationId),
            Permissions = role == OrganizationUserType.Custom
                ? CurrentContext.SetOrganizationPermissionsFromClaims(organizationId.ToString(), claimsDict)
                : null
        };
    }

    private static bool ContainsOrganizationId(this Dictionary<string, IEnumerable<Claim>> claimsDict, string claimType,
        Guid organizationId)
        => claimsDict.TryGetValue(claimType, out var claimValue) &&
           claimValue.Any(c => c.Value.EqualsGuid(organizationId));

    private static OrganizationUserType? GetRoleForOrganizationId(this Dictionary<string, IEnumerable<Claim>> claimsDict,
        Guid organizationId)
    {
        if (claimsDict.ContainsOrganizationId(Claims.OrganizationOwner, organizationId))
        {
            return OrganizationUserType.Owner;
        }

        if (claimsDict.ContainsOrganizationId(Claims.OrganizationAdmin, organizationId))
        {
            return OrganizationUserType.Admin;
        }

        if (claimsDict.ContainsOrganizationId(Claims.OrganizationCustom, organizationId))
        {
            return OrganizationUserType.Custom;
        }

        if (claimsDict.ContainsOrganizationId(Claims.OrganizationUser, organizationId))
        {
            return OrganizationUserType.User;
        }

        return null;
    }

    private static bool EqualsGuid(this string value, Guid guid)
        => Guid.TryParse(value, out var parsedValue) && parsedValue == guid;
}
