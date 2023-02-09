public interface IAccessControlService
{
    public bool UserHasPermission(Permission permission);
    public string GetUserRole(string userEmail);
}