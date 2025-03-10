using Bit.Core.Vault.Entities;

namespace Bit.Core.Vault.Commands.Interfaces;

public interface ICreateManyTaskNotificationsCommand
{
    /// <summary>
    /// Creates email and push notifications for the given security tasks.
    /// </summary>
    /// <param name="organizationId">The organization Id </param>
    /// <param name="securityTasks">All applicable security tasks</param>
    Task CreateAsync(Guid organizationId, IEnumerable<SecurityTask> securityTasks);
}
