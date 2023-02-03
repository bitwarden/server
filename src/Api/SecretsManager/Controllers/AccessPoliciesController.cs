using Bit.Api.Models.Response;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Api.SecretsManager.Models.Response;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Commands.AccessPolicies.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.SecretsManager.Controllers;

[SecretsManager]
[Route("access-policies")]
public class AccessPoliciesController : Controller
{
    private readonly IAccessPolicyRepository _accessPolicyRepository;
    private readonly ICreateAccessPoliciesCommand _createAccessPoliciesCommand;
    private readonly ICurrentContext _currentContext;
    private readonly IDeleteAccessPolicyCommand _deleteAccessPolicyCommand;
    private readonly IGroupRepository _groupRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IServiceAccountRepository _serviceAccountRepository;
    private readonly IUpdateAccessPolicyCommand _updateAccessPolicyCommand;
    private readonly IUserService _userService;

    public AccessPoliciesController(
        IUserService userService,
        ICurrentContext currentContext,
        IAccessPolicyRepository accessPolicyRepository,
        IServiceAccountRepository serviceAccountRepository,
        IGroupRepository groupRepository,
        IProjectRepository projectRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICreateAccessPoliciesCommand createAccessPoliciesCommand,
        IDeleteAccessPolicyCommand deleteAccessPolicyCommand,
        IUpdateAccessPolicyCommand updateAccessPolicyCommand)
    {
        _userService = userService;
        _currentContext = currentContext;
        _serviceAccountRepository = serviceAccountRepository;
        _projectRepository = projectRepository;
        _groupRepository = groupRepository;
        _organizationUserRepository = organizationUserRepository;
        _accessPolicyRepository = accessPolicyRepository;
        _createAccessPoliciesCommand = createAccessPoliciesCommand;
        _deleteAccessPolicyCommand = deleteAccessPolicyCommand;
        _updateAccessPolicyCommand = updateAccessPolicyCommand;
    }

    [HttpPost("/projects/{id}/access-policies")]
    public async Task<ProjectAccessPoliciesResponseModel> CreateProjectAccessPoliciesAsync([FromRoute] Guid id,
        [FromBody] AccessPoliciesCreateRequest request)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var policies = request.ToBaseAccessPoliciesForProject(id);
        var results = await _createAccessPoliciesCommand.CreateForProjectAsync(id, policies, userId);
        return new ProjectAccessPoliciesResponseModel(results);
    }

    [HttpGet("/projects/{id}/access-policies")]
    public async Task<ProjectAccessPoliciesResponseModel> GetProjectAccessPoliciesAsync([FromRoute] Guid id)
    {
        var project = await _projectRepository.GetByIdAsync(id);
        await CheckUserHasWriteAccessToProjectAsync(project);

        var results = await _accessPolicyRepository.GetManyByGrantedProjectIdAsync(id);
        return new ProjectAccessPoliciesResponseModel(results);
    }

    [HttpPut("{id}")]
    public async Task<BaseAccessPolicyResponseModel> UpdateAccessPolicyAsync([FromRoute] Guid id,
        [FromBody] AccessPolicyUpdateRequest request)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var result = await _updateAccessPolicyCommand.UpdateAsync(id, request.Read, request.Write, userId);

        return result switch
        {
            UserProjectAccessPolicy accessPolicy => new UserProjectAccessPolicyResponseModel(accessPolicy),
            GroupProjectAccessPolicy accessPolicy => new GroupProjectAccessPolicyResponseModel(accessPolicy),
            ServiceAccountProjectAccessPolicy accessPolicy => new ServiceAccountProjectAccessPolicyResponseModel(
                accessPolicy),
            _ => throw new ArgumentException("Unsupported access policy type provided."),
        };
    }

    [HttpDelete("{id}")]
    public async Task DeleteAccessPolicyAsync([FromRoute] Guid id)
    {
        var userId = _userService.GetProperUserId(User).Value;
        await _deleteAccessPolicyCommand.DeleteAsync(id, userId);
    }

    [HttpGet("/organizations/{id}/access-policies/potential-grantees")]
    public async Task<ListResponseModel<PotentialGranteeResponseModel>> GetPotentialGranteesAsync([FromRoute] Guid id, [FromQuery] bool includeServiceAccounts = false)
    {
        if (!_currentContext.AccessSecretsManager(id))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User).Value;
        var orgAdmin = await _currentContext.OrganizationAdmin(id);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);

        var groups = await _groupRepository.GetManyByOrganizationIdAsync(id);
        var groupResponses = groups.Select(g => new PotentialGranteeResponseModel(g));

        var organizationUsers =
            await _organizationUserRepository.GetManyDetailsByOrganizationAsync(id);
        var userResponses = organizationUsers
            .Where(user => user.AccessSecretsManager)
            .Select(userDetails => new PotentialGranteeResponseModel(userDetails));

        if (!includeServiceAccounts)
        {
            return new ListResponseModel<PotentialGranteeResponseModel>(userResponses.Concat(groupResponses));
        }

        var serviceAccounts =
            await _serviceAccountRepository.GetManyByOrganizationIdWriteAccessAsync(id,
                userId,
                accessClient);
        var serviceAccountResponses =
            serviceAccounts.Select(serviceAccount => new PotentialGranteeResponseModel(serviceAccount));

        return new ListResponseModel<PotentialGranteeResponseModel>(userResponses.Concat(groupResponses).Concat(serviceAccountResponses));
    }

    private async Task CheckUserHasWriteAccessToProjectAsync(Project project)
    {
        if (project == null || !_currentContext.AccessSecretsManager(project.OrganizationId))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User).Value;
        var orgAdmin = await _currentContext.OrganizationAdmin(project.OrganizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);

        var hasAccess = accessClient switch
        {
            AccessClientType.NoAccessCheck => true,
            AccessClientType.User => await _projectRepository.UserHasWriteAccessToProject(project.Id, userId),
            _ => false,
        };

        if (!hasAccess)
        {
            throw new NotFoundException();
        }
    }
}
