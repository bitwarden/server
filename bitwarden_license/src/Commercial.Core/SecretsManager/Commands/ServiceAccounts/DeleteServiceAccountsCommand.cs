using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.ServiceAccounts.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Commands.ServiceAccounts;

public class DeleteServiceAccountsCommand : IDeleteServiceAccountsCommand
{
    private readonly IServiceAccountRepository _serviceAccountRepository;
    private readonly ICurrentContext _currentContext;

    public DeleteServiceAccountsCommand(
        IServiceAccountRepository serviceAccountRepository,
        ICurrentContext currentContext)
    {
        _serviceAccountRepository = serviceAccountRepository;
        _currentContext = currentContext;
    }

    public async Task<List<Tuple<ServiceAccount, string>>> DeleteServiceAccounts(List<Guid> ids, Guid userId)
    {
        if (ids.Any() != true || userId == new Guid())
        {
            throw new ArgumentNullException();
        }

        var serviceAccounts = (await _serviceAccountRepository.GetManyByIds(ids))?.ToList();

        if (serviceAccounts?.Any() != true || serviceAccounts.Count != ids.Count)
        {
            throw new NotFoundException();
        }

        // Ensure all service accounts belongs to the same organization
        var organizationId = serviceAccounts.First().OrganizationId;
        if (serviceAccounts.Any(p => p.OrganizationId != organizationId))
        {
            throw new BadRequestException();
        }

        if (!_currentContext.AccessSecretsManager(organizationId))
        {
            throw new NotFoundException();
        }

        var orgAdmin = await _currentContext.OrganizationAdmin(organizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);

        var results = new List<Tuple<ServiceAccount, String>>(serviceAccounts.Count);
        var deleteIds = new List<Guid>();

        foreach (var sa in serviceAccounts)
        {
            var hasAccess = accessClient switch
            {
                AccessClientType.NoAccessCheck => true,
                AccessClientType.User => await _serviceAccountRepository.UserHasWriteAccessToServiceAccount(sa.Id, userId),
                _ => false,
            };

            if (!hasAccess)
            {
                results.Add(new Tuple<ServiceAccount, string>(sa, "access denied"));
            }
            else
            {
                results.Add(new Tuple<ServiceAccount, string>(sa, ""));
                deleteIds.Add(sa.Id);
            }
        }

        if (deleteIds.Count > 0)
        {
            await _serviceAccountRepository.DeleteManyByIdAsync(deleteIds);
        }

        return results;
    }
}

