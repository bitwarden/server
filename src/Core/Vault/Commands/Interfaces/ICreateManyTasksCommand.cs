using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Api;

namespace Bit.Core.Vault.Commands.Interfaces;

public interface ICreateManyTasksCommand
{
    /// <summary>
    /// Creates multiple security tasks for an organization.
    /// Each task must be authorized and the user must have the Create permission
    /// and associated ciphers must belong to the organization.
    /// </summary>
    /// <param name="organizationId">The </param>
    /// <param name="tasks"></param>
    /// <returns>Collection of created security tasks</returns>
    Task<ICollection<SecurityTask>> CreateAsync(Guid organizationId, IEnumerable<SecurityTaskCreateRequest> tasks);
}
