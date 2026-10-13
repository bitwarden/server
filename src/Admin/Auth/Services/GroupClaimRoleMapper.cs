using System.Security.Claims;
using Bit.Admin.Utilities;

namespace Bit.Admin.Auth.Services;

// Translates IdP group claims into Admin Portal role names.
//
// An IdP ships group memberships in the `groups` claim of the ID token. By
// convention each Admin Portal group is named `<GroupPrefix><role>` (e.g.
// `us-prd-cloud-adminportal-admin`); the mapper strips the prefix and keeps the suffix only if
// it matches one of the role names in RolePermissionMapping. The suffix match is
// case-insensitive. A principal in multiple matched groups gets the union of their roles.
//
// Decoupled from the ClaimsPrincipal source so the SSO sign-in controller and the AccessControl
// pipeline can share the same mapper and unit tests do not need to spin up ASP.NET.
public interface IGroupClaimRoleMapper
{
    IReadOnlyCollection<string> MapRoles(ClaimsPrincipal principal);
}

public class GroupClaimRoleMapper : IGroupClaimRoleMapper
{
    // The "groups" claim is an OIDC convention currently supported by all major IdPs.
    public const string GroupsClaimType = "groups";

    private readonly string? _groupPrefix;

    public GroupClaimRoleMapper(AdminSettings adminSettings)
    {
        // Tolerant of an unset prefix so the type can be registered unconditionally. The SSO path
        // is the only caller; when OIDC is enabled, AddAdminUpstreamOidc fail-louds on an empty
        // prefix at startup, so a null here means OIDC is off and the mapper will never be hit.
        _groupPrefix = adminSettings?.Oidc?.GroupPrefix;
    }

    public IReadOnlyCollection<string> MapRoles(ClaimsPrincipal principal)
    {
        if (principal is null || string.IsNullOrWhiteSpace(_groupPrefix))
        {
            return Array.Empty<string>();
        }

        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var claim in principal.FindAll(GroupsClaimType))
        {
            if (string.IsNullOrWhiteSpace(claim.Value))
            {
                continue;
            }

            if (!claim.Value.StartsWith(_groupPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var suffix = claim.Value.Substring(_groupPrefix.Length).Trim().ToLowerInvariant();
            if (RolePermissionMapping.RolePermissions.ContainsKey(suffix))
            {
                roles.Add(suffix);
            }
        }

        return roles;
    }
}
