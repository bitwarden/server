// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Security.Claims;
using Bit.Admin.Enums;
using Bit.Admin.Utilities;
using Bit.Core.Settings;

namespace Bit.Admin.Services;

public class AccessControlService : IAccessControlService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;
    private readonly IGlobalSettings _globalSettings;

    public AccessControlService(
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration,
        IGlobalSettings globalSettings)
    {
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
        _globalSettings = globalSettings;
    }

    public bool UserHasPermission(Permission permission)
    {
        if (_globalSettings.SelfHosted)
        {
            return true;
        }

        // A principal may hold multiple role claims when the IdP groups-to-roles mapper stamps
        // more than one role on an SSO sign-in. Treat permissions as the union across every
        // role the user holds: if any role grants the permission, the user has it.
        foreach (var role in GetUserRolesFromClaims())
        {
            if (RolePermissionMapping.RolePermissions.TryGetValue(role, out var rolePermissions) &&
                rolePermissions.Contains(permission))
            {
                return true;
            }
        }

        return false;
    }

    public string GetUserRole(string userEmail)
    {
        var roles = _configuration.GetSection("adminSettings:role").GetChildren();

        if (roles == null || !roles.Any())
        {
            return null;
        }

        userEmail = userEmail.ToLowerInvariant();

        var userRole = roles.FirstOrDefault(s => (s.Value != null ? s.Value.ToLowerInvariant().Split(',').Contains(userEmail) : false));

        if (userRole == null)
        {
            return null;
        }

        return userRole.Key.ToLowerInvariant();
    }

    private IEnumerable<string> GetUserRolesFromClaims()
    {
        var claims = _httpContextAccessor.HttpContext?.User?.Claims;
        if (claims == null)
        {
            return [];
        }

        return claims
            .Where(c => c.Type == ClaimTypes.Role && !string.IsNullOrEmpty(c.Value))
            .Select(c => c.Value.ToLowerInvariant());
    }
}
