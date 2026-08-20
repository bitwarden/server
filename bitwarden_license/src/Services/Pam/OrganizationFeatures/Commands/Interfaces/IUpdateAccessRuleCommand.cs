using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Pam.Entities;
using Bit.Pam.Models;

namespace Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;

public interface IUpdateAccessRuleCommand
{
    /// <summary>
    /// Updates an access rule and replaces its collection associations with exactly the given collections.
    /// </summary>
    /// <returns>
    /// The updated rule, <see cref="Errors.AccessRuleNotFound"/>, or the first validation failure — see
    /// <c>IAccessRuleWriteValidator.ValidateAsync</c> for the errors a write can produce.
    /// </returns>
    Task<CommandResult<AccessRuleDetails>> UpdateAsync(Guid organizationId, Guid id, AccessRule update, IEnumerable<Guid> collectionIds);
}
