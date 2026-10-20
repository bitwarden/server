using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Partnerships.Interfaces;

public interface ISetOrganizationPartnershipStatusCommand
{
    /// <summary>
    /// Turns an organization's partner access on or off. Setting the current status is a no-op.
    /// Existing entitlements keep their state.
    /// </summary>
    /// <returns>The <see cref="OrganizationPartnership"/>, or <see cref="PartnershipNotFound"/>.</returns>
    Task<CommandResult<OrganizationPartnership>> SetStatusAsync(Guid organizationId, PartnershipStatus status);
}
