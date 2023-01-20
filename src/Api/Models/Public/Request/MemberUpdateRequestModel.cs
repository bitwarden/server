using Bit.Core.Entities;

namespace Bit.Api.Models.Public.Request;

public class MemberUpdateRequestModel : MemberBaseModel
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
        existingUser.AccessAll = AccessAll.Value;
        existingUser.ExternalId = ExternalId;
        return existingUser;
    }
}
