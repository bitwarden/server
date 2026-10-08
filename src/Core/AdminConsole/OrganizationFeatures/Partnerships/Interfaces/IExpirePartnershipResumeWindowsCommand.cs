using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Partnerships.Interfaces;

public interface IExpirePartnershipResumeWindowsCommand
{
    /// <summary>
    /// Releases the account binding held by every canceled entitlement whose resume window has ended.
    /// An entitlement that fails to release is logged and skipped.
    /// </summary>
    /// <returns>The number of entitlements released.</returns>
    Task<CommandResult<int>> ExpireAsync();
}
