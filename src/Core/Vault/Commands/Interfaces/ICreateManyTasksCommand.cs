using Bit.Core.Vault.Models.Api;

namespace Bit.Core.Vault.Commands.Interfaces;

public interface ICreateManyTasksCommand
{
    /// <summary>
    /// Creates multiple security tasks for an organization.
    /// </summary>
    /// <param name="organizationId">The </param>
    /// <param name="tasks"></param>
    /// <returns></returns>
    Task CreateAsync(Guid organizationId, IEnumerable<SecurityTaskCreateRequest> tasks);
}
