using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Enums;
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

        Type = user.Type;
        ExternalId = user.ExternalId;

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

        Type = user.Type;
        ExternalId = user.ExternalId;

        if (Type == OrganizationUserType.Custom)
        {
            Permissions = new PermissionsModel(user.GetPermissions());
        }
    }

    /// <summary>
    /// The member's type (or role) within the organization.
    /// </summary>
    [Required]
    [EnumDataType(typeof(OrganizationUserType))]
    public OrganizationUserType? Type { get; set; }
    /// <summary>
    /// External identifier for reference or linking this member to another system, such as a user directory.
    /// </summary>
    /// <example>external_id_123456</example>
    [StringLength(300)]
    public string ExternalId { get; set; }

    /// <summary>
    /// The member's custom permissions if the member has a Custom role. If not supplied, all custom permissions will
    /// default to false.
    /// </summary>
    public PermissionsModel? Permissions { get; set; }
}
