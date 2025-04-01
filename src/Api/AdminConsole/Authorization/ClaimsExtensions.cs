#nullable enable

using System.Security.Claims;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Identity;
using Bit.Core.Models.Data;

namespace Bit.Api.AdminConsole.Authorization;

public static class ClaimsExtensions
{
    // Relevant claim types for organization roles, SM access, and custom permissions
    private static readonly IEnumerable<string> _relevantClaimTypes = new List<string>{
        Claims.OrganizationOwner,
        Claims.OrganizationAdmin,
        Claims.OrganizationCustom,
        Claims.OrganizationUser,
        Claims.SecretsManagerAccess,
    }.Concat(new Permissions().ClaimsMap.Select(c => c.ClaimName));

    public static CurrentContextOrganization? GetCurrentContextOrganization(this ClaimsPrincipal user, Guid organizationId)
    {
        var claimsDict = user.Claims
            .Where(c => _relevantClaimTypes.Contains(c.Type) && Guid.TryParse(c.Value, out _))
            .GroupBy(c => c.Type)
            .ToDictionary(
                c => c.Key,
                c => c.Select(v => new Guid(v.Value)));

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
            AccessSecretsManager = claimsDict.ContainsOrganizationId(Claims.SecretsManagerAccess, organizationId),
            Permissions = role == OrganizationUserType.Custom
                ? claimsDict.GetPermissionsFromClaims(organizationId)
                : null
        };
    }

    private static bool ContainsOrganizationId(this Dictionary<string, IEnumerable<Guid>> claimsDict, string claimType,
        Guid organizationId)
        => claimsDict.TryGetValue(claimType, out var claimValue) &&
           claimValue.Any(guid => guid == organizationId);

    private static OrganizationUserType? GetRoleForOrganizationId(this Dictionary<string, IEnumerable<Guid>> claimsDict,
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

    private static Permissions GetPermissionsFromClaims(this Dictionary<string, IEnumerable<Guid>> claimsDict, Guid organizationId)
    {
        return new Permissions
        {
            AccessEventLogs = claimsDict.ContainsOrganizationId(Claims.AccessEventLogs, organizationId),
            AccessImportExport = claimsDict.ContainsOrganizationId(Claims.AccessImportExport, organizationId),
            AccessReports = claimsDict.ContainsOrganizationId(Claims.AccessReports, organizationId),
            CreateNewCollections = claimsDict.ContainsOrganizationId(Claims.CreateNewCollections, organizationId),
            EditAnyCollection = claimsDict.ContainsOrganizationId(Claims.EditAnyCollection, organizationId),
            DeleteAnyCollection = claimsDict.ContainsOrganizationId(Claims.DeleteAnyCollection, organizationId),
            ManageGroups = claimsDict.ContainsOrganizationId(Claims.ManageGroups, organizationId),
            ManagePolicies = claimsDict.ContainsOrganizationId(Claims.ManagePolicies, organizationId),
            ManageSso = claimsDict.ContainsOrganizationId(Claims.ManageSso, organizationId),
            ManageUsers = claimsDict.ContainsOrganizationId(Claims.ManageUsers, organizationId),
            ManageResetPassword = claimsDict.ContainsOrganizationId(Claims.ManageResetPassword, organizationId),
            ManageScim = claimsDict.ContainsOrganizationId(Claims.ManageScim, organizationId),
        };
    }
}
