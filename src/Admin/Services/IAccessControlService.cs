using Bit.Admin.Enums;

namespace Bit.Admin.Services;

public interface IAccessControlService
{
    public bool UserHasPermission(Permission permission);
    public string GetUserRole(string userEmail);
}
