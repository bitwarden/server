using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Partnerships.Interfaces;

public interface IProvisionPartnershipEntitlementCommand
{
    /// <summary>
    /// Provisions an entitlement for the partner's external ID. An existing entitlement that is not canceled is
    /// returned unchanged; a canceled one is reset to provisioned and its account binding released.
    /// </summary>
    /// <returns>The provisioning outcome, or an <see cref="IPartnershipError"/>.</returns>
    Task<CommandResult<ProvisionPartnershipEntitlementResult>> ProvisionAsync(ProvisionPartnershipEntitlementRequest request);
}
