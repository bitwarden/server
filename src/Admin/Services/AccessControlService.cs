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
            return true;

        var userRole = GetUserRoleFromClaim();
        if (string.IsNullOrEmpty(userRole) || !RolePermissionMapping.RolePermissions.ContainsKey(userRole))
            return false;

        return RolePermissionMapping.RolePermissions[userRole].Contains(permission);
    }

    public string GetUserRole(string userEmail)
    {
        var settings = _configuration.GetSection("adminSettings").GetChildren();

        if (settings == null || !settings.Any())
            return null;

        var rolePrefix = "role";
        userEmail = userEmail.ToLowerInvariant();

        var roleSetting = settings.FirstOrDefault(s => s.Key.StartsWith(rolePrefix)
                                                 && (s.Value != null ? s.Value.ToLowerInvariant().Split(',').Contains(userEmail) : false));

        if (roleSetting == null)
            return null;

        var role = roleSetting.Key.Substring(roleSetting.Key.IndexOf(rolePrefix) + rolePrefix.Length);
        return role.ToLowerInvariant();
    }

    private string GetUserRoleFromClaim()
    {
        return _httpContextAccessor.HttpContext?.User?.Claims?
                 .FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
    }
}
