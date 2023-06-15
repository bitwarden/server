using Bit.Core.Entities;

namespace Bit.Core.SecretsManager.Commands.EnableAccessSecretsManager.Interfaces;

public interface IEnableAccessSecretsManagerCommand
{
    Task<List<(OrganizationUser, string)>> EnableUsersAsync(Guid organizationId,
        ICollection<OrganizationUser> organizationUsers);
}
