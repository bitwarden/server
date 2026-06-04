using Bit.Core.Pam.Entities;
using Bit.Core.Pam.Models;

namespace Bit.Core.Pam.OrganizationFeatures.Commands.Interfaces;

public interface IUpdateAccessRuleCommand
{
    /// <summary>
    /// Updates an access rule and replaces its collection associations with exactly the given collections.
    /// </summary>
    Task<AccessRuleDetails> UpdateAsync(Guid organizationId, Guid id, AccessRule update, IEnumerable<Guid> collectionIds);
}
