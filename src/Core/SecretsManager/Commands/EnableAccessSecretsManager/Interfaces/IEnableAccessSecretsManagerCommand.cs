using Bit.Core.Entities;

namespace Bit.Core.SecretsManager.Commands.EnableAccessSecretsManager.Interfaces;

public interface IEnableAccessSecretsManagerCommand
{
    Task<List<(OrganizationUser organizationUser, string error)>> EnableUsersAsync(
        IEnumerable<OrganizationUser> organizationUsers);
}
