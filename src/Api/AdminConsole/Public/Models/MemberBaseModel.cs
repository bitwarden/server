using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Api.AdminConsole.Public.Models;

public abstract class MemberBaseModel
{
    public MemberBaseModel() { }

    public MemberBaseModel(OrganizationUser user, bool flexibleCollectionsEnabled)
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        Type = flexibleCollectionsEnabled ? GetFlexibleCollectionsUserType(user.Type, user.GetPermissions()) : user.Type;
        AccessAll = user.AccessAll;
        ExternalId = user.ExternalId;
        ResetPasswordEnrolled = user.ResetPasswordKey != null;
    }

    public MemberBaseModel(OrganizationUserUserDetails user, bool flexibleCollectionsEnabled)
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        Type = flexibleCollectionsEnabled ? GetFlexibleCollectionsUserType(user.Type, user.GetPermissions()) : user.Type;
        AccessAll = user.AccessAll;
        ExternalId = user.ExternalId;
        ResetPasswordEnrolled = user.ResetPasswordKey != null;
    }

    /// <summary>
    /// The member's type (or role) within the organization. If your organization has is using the latest collection enhancements,
    /// you will not be allowed to assign the Manager role (OrganizationUserType = 3).
    /// </summary>
    [Required]
    public OrganizationUserType? Type { get; set; }
    /// <summary>
    /// Determines if this member can access all collections within the organization, or only the associated
    /// collections. If set to <c>true</c>, this option overrides any collection assignments. If your organization is using
    /// the latest collection enhancements, you will not be allowed to set this property to <c>true</c>.
    /// </summary>
    public bool? AccessAll { get; set; }
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
