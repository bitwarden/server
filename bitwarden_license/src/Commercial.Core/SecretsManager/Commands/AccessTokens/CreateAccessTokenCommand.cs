using System.Security.Cryptography;
using System.Text;
using Bit.Core;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.AccessTokens.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Utilities;
using Bitwarden.Server.Sdk.Features;

namespace Bit.Commercial.Core.SecretsManager.Commands.AccessTokens;

public class CreateAccessTokenCommand : ICreateAccessTokenCommand
{
    private const int _clientSecretRandomLength = 30;
    private const string _clientSecretPrefix = "bw_";
    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly IFeatureService _featureService;

    public CreateAccessTokenCommand(IApiKeyRepository apiKeyRepository, IFeatureService featureService)
    {
        _apiKeyRepository = apiKeyRepository;
        _featureService = featureService;
    }

    public async Task<ApiKeyClientSecretDetails> CreateAsync(ApiKey apiKey)
    {
        if (apiKey.ServiceAccountId == null)
        {
            throw new BadRequestException();
        }

        var clientSecret = CoreHelpers.SecureRandomString(_clientSecretRandomLength);
        if (_featureService.IsEnabled(FeatureFlagKeys.MachineAccountTokenPrefix))
        {
            clientSecret = _clientSecretPrefix + clientSecret;
        }
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
