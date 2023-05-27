using Microsoft.Extensions.Options;
using System.Security.Claims;
using Bit.Admin.Enums;
using Bit.Admin.Utilities;
using Bit.Core.Settings;

namespace Bit.Admin.Services;

public class AccessControlService : IAccessControlService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IOptionsMonitor<AdminSettingsOptions> _adminSettingsOptionsDelegate;
    private readonly IGlobalSettings _globalSettings;

    public AccessControlService(
        IHttpContextAccessor httpContextAccessor,
        IOptionsMonitor<AdminSettingsOptions> adminSettingsOptionsDelegate,
        IGlobalSettings globalSettings)
    {
        _httpContextAccessor = httpContextAccessor;
        _adminSettingsOptionsDelegate = adminSettingsOptionsDelegate;
        _globalSettings = globalSettings;
    }

    public bool UserHasPermission(Permission permission)
    {
        if (_globalSettings.SelfHosted)
        {
            return true;
        }

        var userRole = GetUserRoleFromClaim();
        if (string.IsNullOrEmpty(userRole) || !RolePermissionMapping.RolePermissions.ContainsKey(userRole))
        {
            return false;
        }

        return RolePermissionMapping.RolePermissions[userRole].Contains(permission);
    }

    public string GetUserRole(string userEmail)
    {
        var roles = _adminSettingsOptionsDelegate.CurrentValue?.Role;

        if (roles == null || !roles.Any())
        {
            return null;
        }

        userEmail = userEmail.ToLowerInvariant();

        var userRole = roles.FirstOrDefault(s => (s.Value != null ? s.Value.ToLowerInvariant().Split(',').Contains(userEmail) : false));

        if (userRole.Equals(default(KeyValuePair<string, string>)))
        {
            return null;
        }

        return userRole.Key.ToLowerInvariant();
    }

    private string GetUserRoleFromClaim()
    {
        return _httpContextAccessor.HttpContext?.User?.Claims?
                 .FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
    }
}
