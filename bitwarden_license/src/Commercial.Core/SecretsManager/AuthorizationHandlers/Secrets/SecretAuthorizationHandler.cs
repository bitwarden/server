using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.SecretsManager.AuthorizationRequirements;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Queries.Interfaces;
using Bit.Core.SecretsManager.Repositories;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Commercial.Core.SecretsManager.AuthorizationHandlers.Secrets;

public class SecretAuthorizationHandler : AuthorizationHandler<SecretOperationRequirement, Secret>
{
    private readonly ICurrentContext _currentContext;
    private readonly IAccessClientQuery _accessClientQuery;
    private readonly IProjectRepository _projectRepository;
    private readonly ISecretRepository _secretRepository;

    public SecretAuthorizationHandler(ICurrentContext currentContext, IAccessClientQuery accessClientQuery,
        IProjectRepository projectRepository, ISecretRepository secretRepository)
    {
        _currentContext = currentContext;
        _accessClientQuery = accessClientQuery;
        _projectRepository = projectRepository;
        _secretRepository = secretRepository;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        SecretOperationRequirement requirement,
        Secret resource)
    {
        if (!_currentContext.AccessSecretsManager(resource.OrganizationId))
        {
            return;
        }

        switch (requirement)
        {
            case not null when requirement == SecretOperations.Create:
                await CanCreateSecretAsync(context, requirement, resource);
                break;
            case not null when requirement == SecretOperations.Read:
                await CanReadSecretAsync(context, requirement, resource);
                break;
            case not null when requirement == SecretOperations.Update:
                await CanUpdateSecretAsync(context, requirement, resource);
                break;
            case not null when requirement == SecretOperations.Delete:
                await CanDeleteSecretAsync(context, requirement, resource);
                break;
            default:
                throw new ArgumentException("Unsupported operation requirement type provided.", nameof(requirement));
        }
    }

    private async Task CanCreateSecretAsync(AuthorizationHandlerContext context,
        SecretOperationRequirement requirement, Secret resource)
    {
        var (accessClient, userId) = await _accessClientQuery.GetAccessClientAsync(context.User, resource.OrganizationId);
        var project = resource.Projects?.FirstOrDefault();

        if (project == null && accessClient != AccessClientType.NoAccessCheck)
        {
            return;
        }

        // All projects should be apart of the same organization
        if (resource.Projects != null
            && resource.Projects.Any()
            && !await _projectRepository.ProjectsAreInOrganization(resource.Projects.Select(p => p.Id).ToList(),
                resource.OrganizationId))
        {
            return;
        }

        var hasAccess = accessClient switch
        {
            AccessClientType.NoAccessCheck => true,
            AccessClientType.User => (await _projectRepository.AccessToProjectAsync(project!.Id, userId, accessClient))
                .Write,
            AccessClientType.ServiceAccount => (await _projectRepository.AccessToProjectAsync(project!.Id, userId, accessClient))
                .Write,
            _ => false,
        };

        if (hasAccess)
        {
            context.Succeed(requirement);
        }
    }

    private async Task CanReadSecretAsync(AuthorizationHandlerContext context,
        SecretOperationRequirement requirement, Secret resource)
    {
        var (accessClient, userId) = await _accessClientQuery.GetAccessClientAsync(context.User, resource.OrganizationId);

        var access = await _secretRepository.AccessToSecretAsync(resource.Id, userId, accessClient);

        if (access.Read)
        {
            context.Succeed(requirement);
        }
    }

    private async Task CanUpdateSecretAsync(AuthorizationHandlerContext context,
        SecretOperationRequirement requirement, Secret resource)
    {
        var (accessClient, userId) = await _accessClientQuery.GetAccessClientAsync(context.User, resource.OrganizationId);

        // All projects should be apart of the same organization
        if (resource.Projects != null
            && resource.Projects.Any()
            && !await _projectRepository.ProjectsAreInOrganization(resource.Projects.Select(p => p.Id).ToList(),
                resource.OrganizationId))
        {
            return;
        }

        bool hasAccess;

        switch (accessClient)
        {
            case AccessClientType.NoAccessCheck:
                hasAccess = true;
                break;
            case AccessClientType.User:
                hasAccess = await GetAccessToUpdateSecretAsync(resource, userId, accessClient);
                break;
            case AccessClientType.ServiceAccount:
                hasAccess = await GetAccessToUpdateSecretAsync(resource, userId, accessClient);
                break;
            default:
                hasAccess = false;
                break;
        }

        if (hasAccess)
        {
            context.Succeed(requirement);
        }
    }

    private async Task CanDeleteSecretAsync(AuthorizationHandlerContext context,
        SecretOperationRequirement requirement, Secret resource)
    {
        var (accessClient, userId) = await _accessClientQuery.GetAccessClientAsync(context.User, resource.OrganizationId);

        var access = await _secretRepository.AccessToSecretAsync(resource.Id, userId, accessClient);

        if (access.Write)
        {
            context.Succeed(requirement);
        }
    }

    private async Task<bool> GetAccessToUpdateSecretAsync(Secret resource, Guid userId, AccessClientType accessClient)
    {
        var newProject = resource.Projects?.FirstOrDefault();
        var access = (await _secretRepository.AccessToSecretAsync(resource.Id, userId, accessClient)).Write;
        var accessToNew = newProject != null &&
                          (await _projectRepository.AccessToProjectAsync(newProject.Id, userId, accessClient))
                          .Write;
        return access && accessToNew;
    }
}
