#nullable enable

using System.Security.Claims;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Identity;
using Bit.Core.Models.Data;

namespace Bit.Api.AdminConsole.Authorization;

public static class OrganizationClaimsExtensions
{
    /// <summary>
    /// Parses a user's claims and returns an object representing their claims for the specified organization.
    /// </summary>
    /// <param name="user">The user who has the claims.</param>
    /// <param name="organizationId">The organizationId to look for in the claims.</param>
    /// <returns>
    /// A <see cref="CurrentContextOrganization"/> representing the user's claims for that organization, or null
    /// if the user does not have any claims for that organization.
    /// </returns>
    public static CurrentContextOrganization? GetCurrentContextOrganization(this ClaimsPrincipal user, Guid organizationId)
    {
        var hasClaim = GetClaimsParser(user, organizationId);

        var role = GetRoleFromClaims(hasClaim);
        if (!role.HasValue)
        {
            // Not an organization member
            return null;
        }

        return new CurrentContextOrganization
        {
            Id = organizationId,
            Type = role.Value,
            AccessSecretsManager = hasClaim(Claims.SecretsManagerAccess),
            Permissions = role == OrganizationUserType.Custom
                ? GetPermissionsFromClaims(hasClaim)
                : new Permissions()
        };
    }

    /// <summary>
    /// Returns a function for evaluating claims for the specified user and organizationId.
    /// The function returns true if the claim type exists and false otherwise.
    /// </summary>
    private static Func<string, bool> GetClaimsParser(ClaimsPrincipal user, Guid organizationId)
    {
        // Group claims by ClaimType
        var claimsDict = user.Claims
            .GroupBy(c => c.Type)
            .ToDictionary(
                c => c.Key,
                c => c.ToList());

        return claimType
            => claimsDict.TryGetValue(claimType, out var claims) &&
               claims
                   .ParseGuids()
                   .Any(v => v == organizationId);
    }

    /// <summary>
    /// Parses the provided claims into proper Guids, or ignore them if they are not valid guids.
    /// </summary>
    private static IEnumerable<Guid> ParseGuids(this IEnumerable<Claim> claims)
    {
        foreach (var claim in claims)
        {
            if (Guid.TryParse(claim.Value, out var guid))
            {
                yield return guid;
            }
        }
    }

    private static OrganizationUserType? GetRoleFromClaims(Func<string, bool> hasClaim)
    {
        if (hasClaim(Claims.OrganizationOwner))
        {
            return OrganizationUserType.Owner;
        }

        if (hasClaim(Claims.OrganizationAdmin))
        {
            return OrganizationUserType.Admin;
        }

        if (hasClaim(Claims.OrganizationCustom))
        {
            return OrganizationUserType.Custom;
        }

        if (hasClaim(Claims.OrganizationUser))
        {
            return OrganizationUserType.User;
        }

        return null;
    }

    private static Permissions GetPermissionsFromClaims(Func<string, bool> hasClaim)
    => new()
    {
        AccessEventLogs = hasClaim(Claims.CustomPermissions.AccessEventLogs),
        AccessImportExport = hasClaim(Claims.CustomPermissions.AccessImportExport),
        AccessReports = hasClaim(Claims.CustomPermissions.AccessReports),
        CreateNewCollections = hasClaim(Claims.CustomPermissions.CreateNewCollections),
        EditAnyCollection = hasClaim(Claims.CustomPermissions.EditAnyCollection),
        DeleteAnyCollection = hasClaim(Claims.CustomPermissions.DeleteAnyCollection),
        ManageGroups = hasClaim(Claims.CustomPermissions.ManageGroups),
        ManagePolicies = hasClaim(Claims.CustomPermissions.ManagePolicies),
        ManageSso = hasClaim(Claims.CustomPermissions.ManageSso),
        ManageUsers = hasClaim(Claims.CustomPermissions.ManageUsers),
        ManageResetPassword = hasClaim(Claims.CustomPermissions.ManageResetPassword),
        ManageScim = hasClaim(Claims.CustomPermissions.ManageScim),
    };
}
