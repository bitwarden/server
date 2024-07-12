using Bit.Core.Models.Data;

namespace Bit.Core.Enums;

public enum OrganizationUserType : byte
{
    Owner = 0,
    Admin = 1,
    User = 2,
    Manager = 3,
    Custom = 4,
}

public static class OrganizationUserTypeExtensions
{
    public static OrganizationUserType GetFlexibleCollectionsUserType(this OrganizationUserType type, Permissions permissions)
    {
        // Downgrade Custom users with no other permissions than 'Edit/Delete Assigned Collections' to User
        if (type == OrganizationUserType.Custom && permissions is not null)
        {
            if ((permissions.EditAssignedCollections || permissions.DeleteAssignedCollections) &&
                permissions is
                {
                    AccessEventLogs: false,
                    AccessImportExport: false,
                    AccessReports: false,
                    CreateNewCollections: false,
                    EditAnyCollection: false,
                    DeleteAnyCollection: false,
                    ManageGroups: false,
                    ManagePolicies: false,
                    ManageSso: false,
                    ManageUsers: false,
                    ManageResetPassword: false,
                    ManageScim: false
                })
            {
                return OrganizationUserType.User;
            }
        }

        return type;
    }
}
