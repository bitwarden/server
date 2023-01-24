using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Commands.AccessTokens.Interfaces;

public interface ICreateAccessTokenCommand
{
    Task<ApiKey> CreateAsync(ApiKey apiKey, Guid userId);
}
