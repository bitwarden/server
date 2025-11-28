using Bit.Core.SecretsManager.Commands.ServiceAccounts.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Commands.ServiceAccounts;

public class RevokeAccessTokensCommand : IRevokeAccessTokensCommand
{
    private readonly IApiKeyRepository _apiKeyRepository;

    public RevokeAccessTokensCommand(IApiKeyRepository apiKeyRepository)
    {
        _apiKeyRepository = apiKeyRepository;
    }

    public async Task RevokeAsync(ServiceAccount serviceAccount, IEnumerable<Guid> Ids)
    {
        var accessTokens = await _apiKeyRepository.GetManyByServiceAccountIdAsync(serviceAccount.Id);

        var tokensToDelete = accessTokens.Where(at => Ids.Contains(at.Id));

        await _apiKeyRepository.DeleteManyAsync(tokensToDelete);
    }
}
