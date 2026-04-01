using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;
using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IInitPendingOrganizationCommand
{
    /// <summary>
    /// Initializes a pending organization created via the Bitwarden Portal on behalf of a Reseller.
    /// See <see cref="ResellerClientOrganizationSignUpCommand"/>.
    /// It also confirms the first owner.
    /// </summary>
    /// <remarks>
    /// The user initializing the organization is the first user to access it - there is no existing 
    /// owner or provider who can change its settings. Therefore, validation in this command assumes 
    /// a default state. For example, it does not enforce policies for this organization because none 
    /// will be enabled yet.
    /// </remarks>
    /// <returns>A CommandResult indicating success or specific validation errors.</returns>
    Task<CommandResult> InitPendingOrganizationAsync(InitPendingOrganizationRequest request);
}
