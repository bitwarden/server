using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Commands.EnableAccessSecretsManager.Interfaces;

namespace Bit.Core.SecretsManager.Commands.EnableAccessSecretsManager;

public class EnableAccessSecretsManagerCommand : IEnableAccessSecretsManagerCommand
{
    private readonly IOrganizationUserRepository _organizationUserRepository;

    public EnableAccessSecretsManagerCommand(IOrganizationUserRepository organizationUserRepository)
    {
        _organizationUserRepository = organizationUserRepository;
    }

    public async Task<List<(OrganizationUser, string)>> EnableUsersAsync(Guid organizationId,
        ICollection<OrganizationUser> organizationUsers)
    {
        var filteredUsersByOrg = organizationUsers.Where(ou => ou.OrganizationId == organizationId)
            .ToList();

        if (!filteredUsersByOrg.Any())
        {
            throw new BadRequestException("Users invalid.");
        }

        var filteredUsers = filteredUsersByOrg.Where(ou => ou.AccessSecretsManager == false).ToList();
        if (filteredUsers.Any())
        {
            foreach (var orgUser in filteredUsers)
            {
                orgUser.AccessSecretsManager = true;
            }

            await _organizationUserRepository.ReplaceManyAsync(filteredUsers);
        }

        return filteredUsersByOrg.Select(ou => (ou, "")).ToList();
    }
}
