using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Commands.ServiceAccounts.Interfaces;

public interface IRevokeAccessTokensCommand
{
    Task<ICollection<ApiKey>> RevokeAsync(ServiceAccount serviceAccount, IEnumerable<Guid> ids);
}
