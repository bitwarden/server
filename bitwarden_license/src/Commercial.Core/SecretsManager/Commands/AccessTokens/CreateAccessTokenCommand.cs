using System.Security.Cryptography;
using System.Text;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.AccessTokens.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Utilities;

namespace Bit.Commercial.Core.SecretsManager.Commands.AccessTokens;

public class CreateAccessTokenCommand : ICreateAccessTokenCommand
{
    private const int _clientSecretMaxLength = 30;
    private readonly IApiKeyRepository _apiKeyRepository;

    public CreateAccessTokenCommand(IApiKeyRepository apiKeyRepository)
    {
        _apiKeyRepository = apiKeyRepository;
    }

    public async Task<ApiKeyClientSecretDetails> CreateAsync(ApiKey apiKey)
    {
        if (apiKey.ServiceAccountId == null)
        {
            throw new BadRequestException();
        }

        var clientSecret = CoreHelpers.SecureRandomString(_clientSecretMaxLength);
        apiKey.ClientSecretHash = GetHash(clientSecret);
        var result = await _apiKeyRepository.CreateAsync(apiKey);
        return new ApiKeyClientSecretDetails { ApiKey = result, ClientSecret = clientSecret };
    }

    private static string GetHash(string input)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
