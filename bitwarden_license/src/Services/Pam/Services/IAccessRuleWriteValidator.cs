using Bit.Core.Exceptions;
using Bit.Pam.Entities;

namespace Bit.Services.Pam.Services;

public interface IAccessRuleWriteValidator
{
    /// <summary>
    /// Validates a rule about to be persisted and returns the deduplicated collection ids to associate with it.
    /// </summary>
    /// <param name="existingRuleId">The rule being updated, which may keep its own name and collections.</param>
    /// <exception cref="BadRequestException">Thrown on the first validation failure.</exception>
    Task<List<Guid>> ValidateAsync(Guid organizationId, AccessRule rule, IEnumerable<Guid> collectionIds,
        Guid? existingRuleId = null);
}
