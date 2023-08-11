using System.Net.Http.Headers;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Commands.AccessTokens.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Api.IntegrationTest.SecretsManager;

public class SecretsManagerOrganizationHelper
{
    private readonly ApiApplicationFactory _factory;
    private readonly string _ownerEmail;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IServiceAccountRepository _serviceAccountRepository;
    private readonly ICreateAccessTokenCommand _createAccessTokenCommand;
    private readonly HttpClient _client;

    public Organization _organization = null!;
    public OrganizationUser _owner = null!;

    public SecretsManagerOrganizationHelper(ApiApplicationFactory factory, string ownerEmail, HttpClient? client = null)
    {
        _factory = factory;
        if (client != null)
        {
            _client = client;
        }
        _organizationRepository = factory.GetService<IOrganizationRepository>();
        _organizationUserRepository = factory.GetService<IOrganizationUserRepository>();
        _ownerEmail = ownerEmail;
        _serviceAccountRepository = factory.GetService<IServiceAccountRepository>();
        _createAccessTokenCommand = factory.GetService<ICreateAccessTokenCommand>();
    }

    public async Task<(Organization organization, OrganizationUser owner)> Initialize(bool useSecrets, bool ownerAccessSecrets)
    {
        (_organization, _owner) = await OrganizationTestHelpers.SignUpAsync(_factory, ownerEmail: _ownerEmail, billingEmail: _ownerEmail);

        if (useSecrets)
        {
            _organization.UseSecretsManager = true;
            await _organizationRepository.ReplaceAsync(_organization);
        }

        if (ownerAccessSecrets)
        {
            _owner.AccessSecretsManager = ownerAccessSecrets;
            await _organizationUserRepository.ReplaceAsync(_owner);
        }

        return (_organization, _owner);
    }

    public async Task<(string email, OrganizationUser orgUser)> CreateNewUser(OrganizationUserType userType, bool accessSecrets)
    {
        var email = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(email);
        var orgUser = await OrganizationTestHelpers.CreateUserAsync(_factory, _organization.Id, email, userType, accessSecrets);

        return (email, orgUser);
    }

    public async Task<(Guid serviceAccountId, ApiKeyClientSecretDetails apiKeyDetails)> CreateNewServiceAccountApiKeyAsync()
    {
        var serviceAccountId = Guid.NewGuid();
        var serviceAccount = new ServiceAccount()
        {
            Id = serviceAccountId,
            OrganizationId = _organization.Id,
            Name = $"integration-test-{serviceAccountId}sa",
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow,
        };
        await _serviceAccountRepository.CreateAsync(serviceAccount);

        var apiKey = new ApiKey()
        {
            ServiceAccountId = serviceAccountId,
            Name = "integration-token",
            Key = Guid.NewGuid().ToString(),
            ExpireAt = null,
            Scope = "[\"api.secrets\"]",
            EncryptedPayload = Guid.NewGuid().ToString(),
        };
        var result = await _createAccessTokenCommand.CreateAsync(apiKey);

        return (serviceAccountId, result);
    }

    public async Task LoginAsync(string email)
    {
        var tokens = await _factory.LoginAsync(email);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);
    }

    public async Task LoginAsync(Guid serviceAccountId, Guid clientId, string clientSecret)
    {
        var token = await _factory.LoginWithClientSecretAsync(clientId, clientSecret);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _client.DefaultRequestHeaders.Add("service_account_id", serviceAccountId.ToString());
    }
}
