using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Api.Models.Public;

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
        AccessAll = user.AccessAll;
        ExternalId = user.ExternalId;
        ResetPasswordEnrolled = user.ResetPasswordKey != null;
    }

    public MemberBaseModel(OrganizationUserUserDetails user)
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        Type = user.Type;
        AccessAll = user.AccessAll;
        ExternalId = user.ExternalId;
        ResetPasswordEnrolled = user.ResetPasswordKey != null;
    }

    /// <summary>
    /// The member's type (or role) within the organization.
    /// </summary>
    [Required]
    public OrganizationUserType? Type { get; set; }
    /// <summary>
    /// Determines if this member can access all collections within the organization, or only the associated
    /// collections. If set to <c>true</c>, this option overrides any collection assignments.
    /// </summary>
    [Required]
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
}
