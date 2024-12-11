namespace Bit.Core.Enums;

public enum OrganizationUserType : byte
{
    Owner = 0,
    Admin = 1,
    User = 2,

    // Manager = 3 has been intentionally permanently deleted
    Custom = 4,
}
