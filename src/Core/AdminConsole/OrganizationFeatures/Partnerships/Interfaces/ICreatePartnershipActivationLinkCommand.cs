using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Partnerships.Interfaces;

public interface ICreatePartnershipActivationLinkCommand
{
    /// <summary>
    /// Mints a short-lived activation link for an entitlement that a customer can still bind: a provisioned one, or a
    /// canceled one whose binding is held inside its resume window.
    /// </summary>
    /// <returns>The activation link, or an <see cref="IPartnershipError"/>.</returns>
    Task<CommandResult<PartnershipActivationLink>> CreateAsync(CreatePartnershipActivationLinkRequest request);
}
