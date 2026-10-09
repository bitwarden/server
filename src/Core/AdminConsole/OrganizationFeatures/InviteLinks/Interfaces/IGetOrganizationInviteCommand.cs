using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;

/// <summary>
/// Retrieves the opaque invite for an invite link after validating that the user is eligible to use it.
/// </summary>
/// <remarks>
/// Eligibility is validated by <see cref="IGetOrganizationInviteValidator"/>.
/// </remarks>
public interface IGetOrganizationInviteCommand
{
    Task<CommandResult<string>> GetInviteAsync(GetOrganizationInviteRequest request);
}
