using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.Enums;

public enum OrganizationUserRemovalType : int
{
    AdminRemove = EventType.OrganizationUser_Removed, // Only org user is deleted by admin
    AdminDelete = EventType.OrganizationUser_Deleted, // User's personal and org account deleted
    SelfRemove = EventType.OrganizationUser_Left, // Only org user is deleted by user
}
