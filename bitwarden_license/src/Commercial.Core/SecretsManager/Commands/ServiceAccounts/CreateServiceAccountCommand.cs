using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Commands.ServiceAccounts.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Commands.ServiceAccounts;

public class CreateServiceAccountCommand : ICreateServiceAccountCommand
{
    private readonly IAccessPolicyRepository _accessPolicyRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IServiceAccountRepository _serviceAccountRepository;

    public CreateServiceAccountCommand(
        IAccessPolicyRepository accessPolicyRepository,
        IOrganizationUserRepository organizationUserRepository,
        IServiceAccountRepository serviceAccountRepository
    )
    {
        _accessPolicyRepository = accessPolicyRepository;
        _organizationUserRepository = organizationUserRepository;
        _serviceAccountRepository = serviceAccountRepository;
    }

    public async Task<ServiceAccount> CreateAsync(ServiceAccount serviceAccount, Guid userId)
    {
        var createdServiceAccount = await _serviceAccountRepository.CreateAsync(serviceAccount);

        var user = await _organizationUserRepository.GetByOrganizationAsync(
            createdServiceAccount.OrganizationId,
            userId
        );
        var accessPolicy = new UserServiceAccountAccessPolicy
        {
            OrganizationUserId = user.Id,
            GrantedServiceAccountId = createdServiceAccount.Id,
            Read = true,
            Write = true,
        };
        await _accessPolicyRepository.CreateManyAsync(new List<BaseAccessPolicy> { accessPolicy });
        return createdServiceAccount;
    }
}
