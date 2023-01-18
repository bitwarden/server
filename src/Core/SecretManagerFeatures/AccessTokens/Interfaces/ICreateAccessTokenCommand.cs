using Bit.Core.Entities;

namespace Bit.Core.SecretManagerFeatures.AccessTokens.Interfaces;

public interface ICreateAccessTokenCommand
{
    Task<ApiKey> CreateAsync(ApiKey apiKey);
}
