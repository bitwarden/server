using System.Security.Cryptography;
using System.Text;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.AccessTokens.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Utilities;

namespace Bit.Commercial.Core.SecretsManager.Commands.AccessTokens;

public class CreateAccessTokenCommand : ICreateAccessTokenCommand
{
    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly int _clientSecretMaxLength = 30;
    private readonly ICurrentContext _currentContext;
    private readonly IServiceAccountRepository _serviceAccountRepository;

    public CreateAccessTokenCommand(
        IApiKeyRepository apiKeyRepository,
        ICurrentContext currentContext,
        IServiceAccountRepository serviceAccountRepository)
    {
        _apiKeyRepository = apiKeyRepository;
        _currentContext = currentContext;
        _serviceAccountRepository = serviceAccountRepository;
    }

    public async Task<ApiKey> CreateAsync(ApiKey apiKey, Guid userId)
    {
        if (apiKey.ServiceAccountId == null)
        {
            throw new BadRequestException();
        }

        var serviceAccount = await _serviceAccountRepository.GetByIdAsync(apiKey.ServiceAccountId.Value);

        if (!_currentContext.AccessSecretsManager(serviceAccount.OrganizationId))
        {
            throw new NotFoundException();
        }

        var orgAdmin = await _currentContext.OrganizationAdmin(serviceAccount.OrganizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);

        var hasAccess = accessClient switch
        {
            AccessClientType.NoAccessCheck => true,
            AccessClientType.User => await _serviceAccountRepository.UserHasWriteAccessToServiceAccount(
                apiKey.ServiceAccountId.Value, userId),
            _ => false,
        };

        if (!hasAccess)
        {
            throw new NotFoundException();
        }

        var clientSecret = CoreHelpers.SecureRandomString(_clientSecretMaxLength);

        // Store the hash of the client secret in the database.
        apiKey.HashedClientSecret = GetHash(clientSecret);
        var result = await _apiKeyRepository.CreateAsync(apiKey);

        // Return the plain text client secret so the client can generate the access token.
        result.HashedClientSecret = clientSecret;
        return result;
    }

    private static string GetHash(string input)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
