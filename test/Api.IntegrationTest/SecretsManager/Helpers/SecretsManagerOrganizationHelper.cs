using System.Diagnostics;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Commands.AccessTokens.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Api.IntegrationTest.SecretsManager.Helpers;

public class SecretsManagerOrganizationHelper
{
    private readonly ApiApplicationFactory _factory;
    private readonly string _ownerEmail;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IServiceAccountRepository _serviceAccountRepository;
    private readonly ICreateAccessTokenCommand _createAccessTokenCommand;

    private Organization _organization = null!;
    private OrganizationUser _owner = null!;

    public SecretsManagerOrganizationHelper(ApiApplicationFactory factory, string ownerEmail)
    {
        _factory = factory;
        _organizationRepository = factory.GetService<IOrganizationRepository>();
        _organizationUserRepository = factory.GetService<IOrganizationUserRepository>();
        _ownerEmail = ownerEmail;
        _serviceAccountRepository = factory.GetService<IServiceAccountRepository>();
        _createAccessTokenCommand = factory.GetService<ICreateAccessTokenCommand>();
    }

    public async Task<(Organization organization, OrganizationUser owner)> Initialize(
        bool useSecrets,
        bool ownerAccessSecrets,
        bool organizationEnabled
    )
    {
        (_organization, _owner!) = await OrganizationTestHelpers.SignUpAsync(
            _factory,
            ownerEmail: _ownerEmail,
            billingEmail: _ownerEmail
        );
        Debug.Assert(_owner is not null);

        if (useSecrets || !organizationEnabled)
        {
            if (useSecrets)
            {
                _organization.UseSecretsManager = true;
            }

            if (!organizationEnabled)
            {
                _organization.Enabled = false;
            }

            await _organizationRepository.ReplaceAsync(_organization);
        }

        if (ownerAccessSecrets)
        {
            _owner.AccessSecretsManager = ownerAccessSecrets;
            await _organizationUserRepository.ReplaceAsync(_owner);
        }

        return (_organization, _owner);
    }

    public async Task<Organization> CreateSmOrganizationAsync()
    {
        var email = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(email);
        var (organization, _) = await OrganizationTestHelpers.SignUpAsync(
            _factory,
            ownerEmail: email,
            billingEmail: email
        );
        return organization;
    }

    public async Task<(string email, OrganizationUser orgUser)> CreateNewUser(
        OrganizationUserType userType,
        bool accessSecrets
    )
    {
        var email = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(email);
        var orgUser = await OrganizationTestHelpers.CreateUserAsync(
            _factory,
            _organization.Id,
            email,
            userType,
            accessSecrets
        );

        return (email, orgUser);
    }

    public async Task<ApiKeyClientSecretDetails> CreateNewServiceAccountApiKeyAsync()
    {
        var serviceAccountId = Guid.NewGuid();
        var serviceAccount = new ServiceAccount
        {
            Id = serviceAccountId,
            OrganizationId = _organization.Id,
            Name = $"integration-test-{serviceAccountId}sa",
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow,
        };
        await _serviceAccountRepository.CreateAsync(serviceAccount);

        var apiKey = new ApiKey
        {
            ServiceAccountId = serviceAccountId,
            Name = "integration-token",
            Key = Guid.NewGuid().ToString(),
            ExpireAt = null,
            Scope = "[\"api.secrets\"]",
            EncryptedPayload = Guid.NewGuid().ToString(),
        };
        return await _createAccessTokenCommand.CreateAsync(apiKey);
    }
}
