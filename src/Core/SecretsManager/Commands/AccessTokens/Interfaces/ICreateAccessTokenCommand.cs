using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Core.SecretsManager.Commands.AccessTokens.Interfaces;

public interface ICreateAccessTokenCommand
{
    Task<ApiKeyClientSecretDetails> CreateAsync(ApiKey apiKey);
}
