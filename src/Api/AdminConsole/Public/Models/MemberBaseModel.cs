using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Api.AdminConsole.Public.Models;

public abstract class MemberBaseModel
{
    public MemberBaseModel() { }

    public MemberBaseModel(OrganizationUser user)
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        Type = GetFlexibleCollectionsUserType(user.Type, user.GetPermissions());
        ExternalId = user.ExternalId;
        ResetPasswordEnrolled = user.ResetPasswordKey != null;

        if (Type == OrganizationUserType.Custom)
        {
            Permissions = new PermissionsModel(user.GetPermissions());
        }
    }

    public MemberBaseModel(OrganizationUserUserDetails user)
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        Type = GetFlexibleCollectionsUserType(user.Type, user.GetPermissions());
        ExternalId = user.ExternalId;
        ResetPasswordEnrolled = user.ResetPasswordKey != null;

        if (Type == OrganizationUserType.Custom)
        {
            Permissions = new PermissionsModel(user.GetPermissions());
        }
    }

    /// <summary>
    /// The member's type (or role) within the organization. If your organization has is using the latest collection enhancements,
    /// you will not be allowed to assign the Manager role (OrganizationUserType = 3).
    /// </summary>
    [Required]
    public OrganizationUserType? Type { get; set; }
    /// <summary>
    /// External identifier for reference or linking this member to another system, such as a user directory.
    /// </summary>
    /// <example>external_id_123456</example>
    [StringLength(300)]
    public string ExternalId { get; set; }
    /// <summary>
    /// Returns <c>true</c> if the member has enrolled in Password Reset assistance within the organization
    /// </summary>
    [Required]
    public bool ResetPasswordEnrolled { get; set; }
    /// <summary>
    /// The member's custom permissions if the member has a Custom role. If not supplied, all custom permissions will
    /// default to false.
    /// </summary>
    public PermissionsModel? Permissions { get; set; }

    // TODO: AC-2188 - Remove this method when the custom users with no other permissions than 'Edit/Delete Assigned Collections' are migrated
    private OrganizationUserType GetFlexibleCollectionsUserType(OrganizationUserType type, Permissions permissions)
    {
        // Downgrade Custom users with no other permissions than 'Edit/Delete Assigned Collections' to User
        if (type == OrganizationUserType.Custom)
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
