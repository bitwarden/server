using Bit.Core.Vault.Commands.Interfaces;
using Bit.Core.Vault.Models.Api;

namespace Bit.Core.Vault.Commands;

public class CreateManyTasksCommand : ICreateManyTasksCommand
{
    public async Task CreateAsync(Guid organizationId, IEnumerable<SecurityTaskCreateRequest> tasks)
    {

    }
}
