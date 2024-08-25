using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.Enums;

public enum OrganizationUserRemovalType : int
{
    AdminRemoved = EventType.OrganizationUser_Removed, // Only org user is deleted by admin
    AdminDeleted = EventType.OrganizationUser_Deleted, // User's personal and org account deleted
    SelfRemoved = EventType.OrganizationUser_Left, // Only org user is deleted by user
}
