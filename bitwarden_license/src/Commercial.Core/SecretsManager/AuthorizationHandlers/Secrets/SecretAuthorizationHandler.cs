using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.SecretsManager.AuthorizationRequirements;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Commercial.Core.SecretsManager.AuthorizationHandlers.Secrets;

public class SecretAuthorizationHandler : AuthorizationHandler<SecretOperationRequirement, Secret>
{
    private readonly ICurrentContext _currentContext;
    private readonly IProjectRepository _projectRepository;
    private readonly ISecretRepository _secretRepository;
    private readonly IUserService _userService;

    public SecretAuthorizationHandler(ICurrentContext currentContext, IUserService userService,
        IProjectRepository projectRepository, ISecretRepository secretRepository)
    {
        _currentContext = currentContext;
        _userService = userService;
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
            case not null when requirement == SecretOperations.Update:
                await CanUpdateSecretAsync(context, requirement, resource);
                break;
            default:
                throw new ArgumentException("Unsupported operation requirement type provided.", nameof(requirement));
        }
    }

    private async Task CanCreateSecretAsync(AuthorizationHandlerContext context,
        SecretOperationRequirement requirement, Secret resource)
    {
        var (accessClient, userId) = await GetAccessClientAsync(context, resource.OrganizationId);
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
            AccessClientType.ServiceAccount => false,
            _ => false,
        };

        if (hasAccess)
        {
            context.Succeed(requirement);
        }
    }

    private async Task CanUpdateSecretAsync(AuthorizationHandlerContext context,
        SecretOperationRequirement requirement, Secret resource)
    {
        var (accessClient, userId) = await GetAccessClientAsync(context, resource.OrganizationId);

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
                var newProject = resource.Projects?.FirstOrDefault();
                var access = (await _secretRepository.AccessToSecretAsync(resource.Id, userId, accessClient)).Write;
                var accessToNew = newProject != null &&
                                  (await _projectRepository.AccessToProjectAsync(newProject.Id, userId, accessClient))
                                  .Write;
                hasAccess = access && accessToNew;
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

    private async Task<(AccessClientType AccessClientType, Guid UserId)> GetAccessClientAsync(
        AuthorizationHandlerContext context, Guid organizationId)
    {
        var orgAdmin = await _currentContext.OrganizationAdmin(organizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);
        var userId = _userService.GetProperUserId(context.User).Value;
        return (accessClient, userId);
    }
}
