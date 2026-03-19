using System.Security.Cryptography;
using System.Text;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Utilities;
using Bit.Core.Vault.Commands.Interfaces;

namespace Bit.Core.Vault.Commands;

public class CreateCollectionApiKeyCommand : ICreateCollectionApiKeyCommand
{
    private const int ClientSecretMaxLength = 30;
    private readonly IApiKeyRepository _apiKeyRepository;

    public CreateCollectionApiKeyCommand(IApiKeyRepository apiKeyRepository)
    {
        _apiKeyRepository = apiKeyRepository;
    }

    public async Task<ApiKeyClientSecretDetails> CreateAsync(ApiKey apiKey)
    {
        if (apiKey.CollectionId == null)
        {
            throw new ArgumentException("CollectionId is required for collection-scoped API keys.");
        }

        if (apiKey.OrganizationId == null)
        {
            throw new ArgumentException("OrganizationId is required for collection-scoped API keys.");
        }

        apiKey.Id = CoreHelpers.GenerateComb();
        var clientSecret = CoreHelpers.SecureRandomString(ClientSecretMaxLength);
        apiKey.ClientSecretHash = GetHash(clientSecret);
        apiKey.CreationDate = DateTime.UtcNow;
        apiKey.RevisionDate = DateTime.UtcNow;

        await _apiKeyRepository.CreateAsync(apiKey);

        return new ApiKeyClientSecretDetails
        {
            ApiKey = apiKey,
            ClientSecret = clientSecret,
        };
    }

    private static string GetHash(string input)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
