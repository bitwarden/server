using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Commands.ServiceAccounts.Interfaces;

public interface IRevokeAccessTokensCommand
{
    Task RevokeAsync(ServiceAccount serviceAccount, IEnumerable<Guid> ids);
}
