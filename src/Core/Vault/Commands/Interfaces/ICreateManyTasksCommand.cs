using Bit.Core.Vault.Models.Api;

namespace Bit.Core.Vault.Commands.Interfaces;

public interface ICreateManyTasksCommand
{
    Task CreateAsync(Guid organizationId, IEnumerable<SecurityTaskCreateRequest> tasks);
}
