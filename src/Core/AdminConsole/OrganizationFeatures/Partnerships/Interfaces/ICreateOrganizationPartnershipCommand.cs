using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Partnerships.Interfaces;

public interface ICreateOrganizationPartnershipCommand
{
    /// <summary>
    /// Creates an active partnership for an organization that does not have one.
    /// </summary>
    /// <returns>The created <see cref="OrganizationPartnership"/>, or an <see cref="IPartnershipError"/>.</returns>
    Task<CommandResult<OrganizationPartnership>> CreateAsync(CreateOrganizationPartnershipRequest request);
}
