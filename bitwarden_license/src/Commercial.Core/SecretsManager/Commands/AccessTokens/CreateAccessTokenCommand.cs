using Bit.Core.SecretsManager.Commands.AccessTokens.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Utilities;

namespace Bit.Commercial.Core.SecretsManager.Commands.AccessTokens;

public class CreateAccessTokenCommand : ICreateAccessTokenCommand
{
    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly int _clientSecretMaxLength = 30;

    public CreateAccessTokenCommand(
        IApiKeyRepository apiKeyRepository)
    {
        _apiKeyRepository = apiKeyRepository;
    }

    public async Task<ApiKey> CreateAsync(ApiKey apiKey)
    {
        apiKey.ClientSecret = CoreHelpers.SecureRandomString(_clientSecretMaxLength);
        return await _apiKeyRepository.CreateAsync(apiKey);
    }
}
