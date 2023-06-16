using Bit.Core.Entities;
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

    public async Task<List<(OrganizationUser organizationUser, string error)>> EnableUsersAsync(
        IEnumerable<OrganizationUser> organizationUsers)
    {
        var results = new List<(OrganizationUser organizationUser, string error)>();
        var usersToEnable = new List<OrganizationUser>();

        foreach (var orgUser in organizationUsers)
        {
            if (orgUser.AccessSecretsManager)
            {
                results.Add((orgUser, "User already has access to Secrets Manager"));
            }
            else
            {
                orgUser.AccessSecretsManager = true;
                usersToEnable.Add(orgUser);
                results.Add((orgUser, ""));
            }
        }

        if (usersToEnable.Any())
        {
            await _organizationUserRepository.ReplaceManyAsync(usersToEnable);
        }

        return results;
    }
}
