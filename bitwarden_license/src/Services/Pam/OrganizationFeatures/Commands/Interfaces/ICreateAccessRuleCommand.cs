using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Pam.Entities;
using Bit.Pam.Models;

namespace Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;

public interface ICreateAccessRuleCommand
{
    /// <summary>
    /// Creates an access rule and associates exactly the given collections with it.
    /// </summary>
    /// <returns>
    /// The created rule, or the first validation failure — see <c>IAccessRuleWriteValidator.ValidateAsync</c> for
    /// the errors a write can produce.
    /// </returns>
    Task<CommandResult<AccessRuleDetails>> CreateAsync(AccessRule rule, IEnumerable<Guid> collectionIds);
}
