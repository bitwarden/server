using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.SecretsManager.AuthorizationRequirements;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Queries.Interfaces;
using Bit.Core.SecretsManager.Repositories;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Commercial.Core.SecretsManager.AuthorizationHandlers.Projects;

public class ProjectAuthorizationHandler : AuthorizationHandler<ProjectOperationRequirement, Project>
{
    private readonly IAccessClientQuery _accessClientQuery;
    private readonly ICurrentContext _currentContext;
    private readonly IProjectRepository _projectRepository;

    public ProjectAuthorizationHandler(ICurrentContext currentContext, IAccessClientQuery accessClientQuery,
        IProjectRepository projectRepository)
    {
        _currentContext = currentContext;
        _accessClientQuery = accessClientQuery;
        _projectRepository = projectRepository;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        ProjectOperationRequirement requirement,
        Project resource)
    {
        if (!_currentContext.AccessSecretsManager(resource.OrganizationId))
        {
            return;
        }

        switch (requirement)
        {
            case not null when requirement == ProjectOperations.Create:
                await CanCreateProjectAsync(context, requirement, resource);
                break;
            case not null when requirement == ProjectOperations.Update:
                await CanUpdateProjectAsync(context, requirement, resource);
                break;
            case not null when requirement == ProjectOperations.Delete:
                await CanDeleteProjectAsync(context, requirement, resource);
                break;
            default:
                throw new ArgumentException("Unsupported operation requirement type provided.", nameof(requirement));
        }
    }

    private async Task CanCreateProjectAsync(AuthorizationHandlerContext context,
        ProjectOperationRequirement requirement, Project resource)
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

    private async Task CanUpdateProjectAsync(AuthorizationHandlerContext context,
        ProjectOperationRequirement requirement, Project resource)
    {
        var (accessClient, userId) =
            await _accessClientQuery.GetAccessClientAsync(context.User, resource.OrganizationId);
        if (accessClient == AccessClientType.ServiceAccount)
        {
            return;
        }

        var access = await _projectRepository.AccessToProjectAsync(resource.Id, userId, accessClient);

        if (access.Write)
        {
            context.Succeed(requirement);
        }
    }

    private async Task CanDeleteProjectAsync(AuthorizationHandlerContext context,
        ProjectOperationRequirement requirement, Project resource)
    {
        var (accessClient, userId) =
            await _accessClientQuery.GetAccessClientAsync(context.User, resource.OrganizationId);
        if (accessClient == AccessClientType.ServiceAccount)
        {
            return;
        }

        var access = await _projectRepository.AccessToProjectAsync(resource.Id, userId, accessClient);

        if (access.Write)
        {
            context.Succeed(requirement);
        }
    }
}
