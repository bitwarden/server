using Bit.Pam.Entities;
using Bit.Pam.Models;

namespace Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;

public interface ICreateAccessRuleCommand
{
    /// <summary>
    /// Creates an access rule and associates exactly the given collections with it.
    /// </summary>
    Task<AccessRuleDetails> CreateAsync(AccessRule rule, IEnumerable<Guid> collectionIds);
}
