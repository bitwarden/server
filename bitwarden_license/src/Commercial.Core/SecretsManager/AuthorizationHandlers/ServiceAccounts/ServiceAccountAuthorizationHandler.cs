using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.SecretsManager.AuthorizationRequirements;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Queries.Interfaces;
using Bit.Core.SecretsManager.Repositories;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Commercial.Core.SecretsManager.AuthorizationHandlers.ServiceAccounts;

public class
    ServiceAccountAuthorizationHandler : AuthorizationHandler<ServiceAccountOperationRequirement, ServiceAccount>
{
    private readonly ICurrentContext _currentContext;
    private readonly IServiceAccountRepository _serviceAccountRepository;
    private readonly IAccessClientQuery _accessClientQuery;

    public ServiceAccountAuthorizationHandler(ICurrentContext currentContext,
        IServiceAccountRepository serviceAccountRepository,
        IAccessClientQuery accessClientQuery)
    {
        _currentContext = currentContext;
        _serviceAccountRepository = serviceAccountRepository;
        _accessClientQuery = accessClientQuery;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        ServiceAccountOperationRequirement requirement,
        ServiceAccount resource)
    {
        if (!_currentContext.AccessSecretsManager(resource.OrganizationId))
        {
            return;
        }

        switch (requirement)
        {
            case not null when requirement == ServiceAccountOperations.Create:
                await CanCreateServiceAccountAsync(context, requirement, resource);
                break;
            case not null when requirement == ServiceAccountOperations.Read:
                await CanReadServiceAccountAsync(context, requirement, resource);
                break;
            case not null when requirement == ServiceAccountOperations.Update:
                await CanUpdateServiceAccountAsync(context, requirement, resource);
                break;
            default:
                throw new ArgumentException("Unsupported operation requirement type provided.",
                    nameof(requirement));
        }
    }

    private async Task CanCreateServiceAccountAsync(AuthorizationHandlerContext context,
        ServiceAccountOperationRequirement requirement, ServiceAccount resource)
    {
        var (accessClient, _) = await _accessClientQuery.GetAccessClientAsync(context.User, resource.OrganizationId);
        var hasAccess = accessClient switch
        {
            AccessClientType.NoAccessCheck => true,
            AccessClientType.User => true,
            AccessClientType.ServiceAccount => false,
            _ => false,
        };

        if (hasAccess)
        {
            context.Succeed(requirement);
        }
    }

    private async Task CanReadServiceAccountAsync(AuthorizationHandlerContext context,
        ServiceAccountOperationRequirement requirement, ServiceAccount resource)
    {
        var (accessClient, userId) = await _accessClientQuery.GetAccessClientAsync(context.User, resource.OrganizationId);
        var access =
            await _serviceAccountRepository.AccessToServiceAccountAsync(resource.Id, userId,
                accessClient);

        if (access.Read)
        {
            context.Succeed(requirement);
        }
    }

    private async Task CanUpdateServiceAccountAsync(AuthorizationHandlerContext context,
        ServiceAccountOperationRequirement requirement, ServiceAccount resource)
    {
        var (accessClient, userId) = await _accessClientQuery.GetAccessClientAsync(context.User, resource.OrganizationId);
        var access =
            await _serviceAccountRepository.AccessToServiceAccountAsync(resource.Id, userId,
                accessClient);

        if (access.Write)
        {
            context.Succeed(requirement);
        }
    }
}
