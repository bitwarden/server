using Bit.Core.Enums;
using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Core.SecretsManager.Queries.ServiceAccounts.Interfaces;

public interface IServiceAccountSecretsDetailsQuery
{
    public Task<IEnumerable<ServiceAccountSecretsDetails>> GetManyByOrganizationIdAsync(
        Guid organizationId,
        Guid userId,
        AccessClientType accessClient,
        bool includeAccessToSecrets
    );
}
