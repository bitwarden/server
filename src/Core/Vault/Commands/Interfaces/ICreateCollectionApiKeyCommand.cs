using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Core.Vault.Commands.Interfaces;

public interface ICreateCollectionApiKeyCommand
{
    Task<ApiKeyClientSecretDetails> CreateAsync(ApiKey apiKey);
}
