// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Scim.Models;

namespace Bit.Scim.Users.Interfaces;

public interface IPostUserCommand
{
    Task<OrganizationUserUserDetails> PostUserAsync(Guid organizationId, ScimUserRequestModel model);
}
