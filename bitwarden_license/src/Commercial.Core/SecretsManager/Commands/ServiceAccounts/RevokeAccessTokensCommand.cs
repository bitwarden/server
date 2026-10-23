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

    public async Task<ICollection<ApiKey>> RevokeAsync(ServiceAccount serviceAccount, IEnumerable<Guid> ids)
    {
        var accessTokens = await _apiKeyRepository.GetManyByServiceAccountIdAsync(serviceAccount.Id);

        var tokensToDelete = accessTokens.Where(at => ids.Contains(at.Id)).ToList();

        await _apiKeyRepository.DeleteManyAsync(tokensToDelete);

        return tokensToDelete;
    }
}
