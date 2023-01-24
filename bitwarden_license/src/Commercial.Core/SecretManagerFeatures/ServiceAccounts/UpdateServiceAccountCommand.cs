using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.SecretManagerFeatures.ServiceAccounts.Interfaces;

namespace Bit.Commercial.Core.SecretManagerFeatures.ServiceAccounts;

public class UpdateServiceAccountCommand : IUpdateServiceAccountCommand
{
    private readonly IServiceAccountRepository _serviceAccountRepository;
    private readonly ICurrentContext _currentContext;

    public UpdateServiceAccountCommand(IServiceAccountRepository serviceAccountRepository, ICurrentContext currentContext)
    {
        _serviceAccountRepository = serviceAccountRepository;
        _currentContext = currentContext;
    }

    public async Task<ServiceAccount> UpdateAsync(ServiceAccount updatedServiceAccount, Guid userId)
    {
        var serviceAccount = await _serviceAccountRepository.GetByIdAsync(updatedServiceAccount.Id);
        if (serviceAccount == null)
        {
            throw new NotFoundException();
        }

        var orgAdmin = await _currentContext.OrganizationAdmin(serviceAccount.OrganizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);

        var hasAccess = accessClient switch
        {
            AccessClientType.NoAccessCheck => true,
            AccessClientType.User => await _serviceAccountRepository.UserHasWriteAccessToServiceAccount(updatedServiceAccount.Id, userId),
            _ => false,
        };

        if (!hasAccess)
        {
            throw new UnauthorizedAccessException();
        }

        serviceAccount.Name = updatedServiceAccount.Name;
        serviceAccount.RevisionDate = DateTime.UtcNow;

        await _serviceAccountRepository.ReplaceAsync(serviceAccount);
        return serviceAccount;
    }
}
