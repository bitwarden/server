using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Api.AdminConsole.Public.Models.Request;

public class MemberUpdateRequestModel : MemberBaseModel, IValidatableObject
{
    /// <summary>
    /// The associated collections that this member can access.
    /// </summary>
    public IEnumerable<AssociationWithPermissionsRequestModel>? Collections { get; set; }

    /// <summary>
    /// Ids of the associated groups that this member will belong to
    /// </summary>
    public IEnumerable<Guid>? Groups { get; set; }

    /// <summary>
    /// The member's email address. Can only be changed for a claimed member without a master password when the
    /// new address is on a domain verified by the organization.
    /// </summary>
    [StrictEmailAddressNullable]
    [StringLength(256)]
    public string? Email { get; set; }

    /// <summary>
    /// The member's name. Can only be changed for a claimed member.
    /// </summary>
    [StringLength(50)]
    public string? Name { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Type is not OrganizationUserType.Custom && Permissions is not null)
        {
            yield return new ValidationResult("Only users with the Custom role may use custom permissions.");
        }
    }
}
