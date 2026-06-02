using Bit.Core.PrivilegedAccessManagement.Entities;
using Bit.Core.PrivilegedAccessManagement.Models;

namespace Bit.Core.PrivilegedAccessManagement.OrganizationFeatures.Commands.Interfaces;

public interface ICreateAccessRuleCommand
{
    /// <summary>
    /// Creates an access rule and associates exactly the given collections with it.
    /// </summary>
    Task<AccessRuleDetails> CreateAsync(AccessRule rule, IEnumerable<Guid> collectionIds);
}
