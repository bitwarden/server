// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Commands.ServiceAccounts.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;

namespace Bit.Commercial.Core.SecretsManager.Commands.ServiceAccounts;

public class CreateServiceAccountCommand : ICreateServiceAccountCommand
{
    private readonly IAccessPolicyRepository _accessPolicyRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IServiceAccountRepository _serviceAccountRepository;
    private readonly IEventService _eventService;
    private readonly ICurrentContext _currentContext;

    public CreateServiceAccountCommand(
        IAccessPolicyRepository accessPolicyRepository,
        IOrganizationUserRepository organizationUserRepository,
        IServiceAccountRepository serviceAccountRepository,
        IEventService eventService,
        ICurrentContext currentContext)
    {
        _accessPolicyRepository = accessPolicyRepository;
        _organizationUserRepository = organizationUserRepository;
        _serviceAccountRepository = serviceAccountRepository;
        _eventService = eventService;
        _currentContext = currentContext;
    }

    public async Task<ServiceAccount> CreateAsync(ServiceAccount serviceAccount, Guid userId)
    {
        var createdServiceAccount = await _serviceAccountRepository.CreateAsync(serviceAccount);

        var user = await _organizationUserRepository.GetByOrganizationAsync(createdServiceAccount.OrganizationId,
            userId);
        var accessPolicy = new UserServiceAccountAccessPolicy
        {
            OrganizationUserId = user.Id,
            GrantedServiceAccountId = createdServiceAccount.Id,
            Read = true,
            Write = true,
        };
        await _accessPolicyRepository.CreateManyAsync(new List<BaseAccessPolicy> { accessPolicy });
        await _eventService.LogServiceAccountPeopleEventAsync(user.Id, accessPolicy, EventType.ServiceAccount_UserAdded, _currentContext.IdentityClientType);
        return createdServiceAccount;
    }
}
