using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Commercial.Core.SecretsManager.AuthorizationHandlers.Projects;

public class ProjectAuthorizationHandler : AuthorizationHandler<ProjectOperationRequirement, Project>
{
    private readonly ICurrentContext _currentContext;
    private readonly IProjectRepository _projectRepository;
    private readonly IUserService _userService;

    public ProjectAuthorizationHandler(ICurrentContext currentContext, IUserService userService,
        IProjectRepository projectRepository)
    {
        _currentContext = currentContext;
        _userService = userService;
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
        }
    }

    private async Task CanCreateProjectAsync(AuthorizationHandlerContext context,
        ProjectOperationRequirement requirement, Project resource)
    {
        var accessClient = await GetAccessClientAsync(resource.OrganizationId);
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
        var accessClient = await GetAccessClientAsync(resource.OrganizationId);
        if (accessClient == AccessClientType.ServiceAccount)
        {
            return;
        }

        var userId = _userService.GetProperUserId(context.User).Value;
        var access = await _projectRepository.AccessToProjectAsync(resource.Id, userId, accessClient);

        if (access.Write)
        {
            context.Succeed(requirement);
        }
    }

    private async Task<AccessClientType> GetAccessClientAsync(Guid organizationId)
    {
        var orgAdmin = await _currentContext.OrganizationAdmin(organizationId);
        return AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);
    }
}
