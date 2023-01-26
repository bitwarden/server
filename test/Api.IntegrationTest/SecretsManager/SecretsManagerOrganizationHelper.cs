using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;

namespace Bit.Api.IntegrationTest.SecretsManager;

public class SecretsManagerOrganizationHelper
{
    private readonly ApiApplicationFactory _factory;
    private readonly string _ownerEmail;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;

    public Organization _organization = null!;
    public OrganizationUser _owner = null!;

    public SecretsManagerOrganizationHelper(ApiApplicationFactory factory, string ownerEmail)
    {
        _factory = factory;
        _organizationRepository = factory.GetService<IOrganizationRepository>();
        _organizationUserRepository = factory.GetService<IOrganizationUserRepository>();

        _ownerEmail = ownerEmail;
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

    public async Task<string> CreateNewUser(OrganizationUserType userType, bool accessSecrets)
    {
        var email = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(email);
        await OrganizationTestHelpers.CreateUserAsync(_factory, _organization.Id, email, userType, accessSecrets);

        return email;
    }
}
