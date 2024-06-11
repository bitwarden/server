using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Api.AdminConsole.Public.Models.Request;

public class MemberUpdateRequestModel : MemberBaseModel, IValidatableObject
{
    /// <summary>
    /// The associated collections that this member can access.
    /// </summary>
    public IEnumerable<AssociationWithPermissionsRequestModel> Collections { get; set; }

    /// <summary>
    /// Ids of the associated groups that this member will belong to
    /// </summary>
    public IEnumerable<Guid> Groups { get; set; }

    public virtual OrganizationUser ToOrganizationUser(OrganizationUser existingUser)
    {
        existingUser.Type = Type.Value;
        existingUser.ExternalId = ExternalId;

        // Permissions property is optional for backwards compatibility with existing usage
        if (existingUser.Type is OrganizationUserType.Custom && Permissions is not null)
        {
            existingUser.SetPermissions(Permissions.ToData());
        }

        return existingUser;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Type is not OrganizationUserType.Custom && Permissions is not null)
        {
            yield return new ValidationResult("Only users with the Custom role may use custom permissions.");
        }
    }
}
