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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.SecretsManager.Controllers;

[SecretsManager]
[Authorize("secrets")]
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
        var project = await _projectRepository.GetByIdAsync(id);
        if (project == null)
        {
            throw new NotFoundException();
        }

        var (accessClient, userId) = await GetAccessClientTypeAsync(project.OrganizationId);
        var policies = request.ToBaseAccessPoliciesForProject(id);
        await _createAccessPoliciesCommand.CreateManyAsync(policies, userId, accessClient);
        var results = await _accessPolicyRepository.GetManyByGrantedProjectIdAsync(id);
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

    [HttpPost("/service-accounts/{id}/access-policies")]
    public async Task<ServiceAccountAccessPoliciesResponseModel> CreateServiceAccountAccessPoliciesAsync(
        [FromRoute] Guid id,
        [FromBody] AccessPoliciesCreateRequest request)
    {
        var serviceAccount = await _serviceAccountRepository.GetByIdAsync(id);
        if (serviceAccount == null)
        {
            throw new NotFoundException();
        }

        var (accessClient, userId) = await GetAccessClientTypeAsync(serviceAccount.OrganizationId);
        var policies = request.ToBaseAccessPoliciesForServiceAccount(id);
        await _createAccessPoliciesCommand.CreateManyAsync(policies, userId, accessClient);
        var results = await _accessPolicyRepository.GetManyByGrantedServiceAccountIdAsync(id);
        return new ServiceAccountAccessPoliciesResponseModel(results);
    }

    [HttpGet("/service-accounts/{id}/access-policies")]
    public async Task<ServiceAccountAccessPoliciesResponseModel> GetServiceAccountAccessPoliciesAsync(
        [FromRoute] Guid id)
    {
        var serviceAccount = await _serviceAccountRepository.GetByIdAsync(id);
        await CheckUserHasWriteAccessToServiceAccountAsync(serviceAccount);

        var results = await _accessPolicyRepository.GetManyByGrantedServiceAccountIdAsync(id);
        return new ServiceAccountAccessPoliciesResponseModel(results);
    }

    [HttpGet("/service-accounts/{id}/granted-policies")]
    public async Task<ListResponseModel<ServiceAccountProjectAccessPolicyResponseModel>>
        GetServiceAccountGrantedPoliciesAsync([FromRoute] Guid id)
    {
        var serviceAccount = await _serviceAccountRepository.GetByIdAsync(id);
        if (serviceAccount == null)
        {
            throw new NotFoundException();
        }

        var (accessClient, userId) = await GetAccessClientTypeAsync(serviceAccount.OrganizationId);
        var results = await _accessPolicyRepository.GetManyByServiceAccountIdAsync(id, userId, accessClient);
        var responses = results.Select(ap =>
            new ServiceAccountProjectAccessPolicyResponseModel((ServiceAccountProjectAccessPolicy)ap));
        return new ListResponseModel<ServiceAccountProjectAccessPolicyResponseModel>(responses);
    }

    [HttpPost("/service-accounts/{id}/granted-policies")]
    public async Task<ListResponseModel<ServiceAccountProjectAccessPolicyResponseModel>>
        CreateServiceAccountGrantedPoliciesAsync([FromRoute] Guid id,
            [FromBody] List<GrantedAccessPolicyRequest> requests)
    {
        var serviceAccount = await _serviceAccountRepository.GetByIdAsync(id);
        if (serviceAccount == null)
        {
            throw new NotFoundException();
        }

        var (accessClient, userId) = await GetAccessClientTypeAsync(serviceAccount.OrganizationId);
        var policies = requests.Select(request => request.ToServiceAccountProjectAccessPolicy(id));
        var results =
            await _createAccessPoliciesCommand.CreateManyAsync(new List<BaseAccessPolicy>(policies), userId, accessClient);
        var responses = results.Select(ap =>
            new ServiceAccountProjectAccessPolicyResponseModel((ServiceAccountProjectAccessPolicy)ap));
        return new ListResponseModel<ServiceAccountProjectAccessPolicyResponseModel>(responses);
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
            UserServiceAccountAccessPolicy accessPolicy =>
                new UserServiceAccountAccessPolicyResponseModel(accessPolicy),
            GroupProjectAccessPolicy accessPolicy => new GroupProjectAccessPolicyResponseModel(accessPolicy),
            GroupServiceAccountAccessPolicy accessPolicy => new GroupServiceAccountAccessPolicyResponseModel(
                accessPolicy),
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

    [HttpGet("/organizations/{id}/access-policies/people/potential-grantees")]
    public async Task<ListResponseModel<PotentialGranteeResponseModel>> GetPeoplePotentialGranteesAsync(
        [FromRoute] Guid id)
    {
        if (!_currentContext.AccessSecretsManager(id))
        {
            throw new NotFoundException();
        }

        var groups = await _groupRepository.GetManyByOrganizationIdAsync(id);
        var groupResponses = groups.Select(g => new PotentialGranteeResponseModel(g));

        var organizationUsers =
            await _organizationUserRepository.GetManyDetailsByOrganizationAsync(id);
        var userResponses = organizationUsers
            .Where(user => user.AccessSecretsManager)
            .Select(userDetails => new PotentialGranteeResponseModel(userDetails));

        return new ListResponseModel<PotentialGranteeResponseModel>(userResponses.Concat(groupResponses));
    }

    [HttpGet("/organizations/{id}/access-policies/service-accounts/potential-grantees")]
    public async Task<ListResponseModel<PotentialGranteeResponseModel>> GetServiceAccountsPotentialGranteesAsync(
        [FromRoute] Guid id)
    {
        var (accessClient, userId) = await GetAccessClientTypeAsync(id);

        var serviceAccounts =
            await _serviceAccountRepository.GetManyByOrganizationIdWriteAccessAsync(id,
                userId,
                accessClient);
        var serviceAccountResponses =
            serviceAccounts.Select(serviceAccount => new PotentialGranteeResponseModel(serviceAccount));

        return new ListResponseModel<PotentialGranteeResponseModel>(serviceAccountResponses);
    }

    [HttpGet("/organizations/{id}/access-policies/projects/potential-grantees")]
    public async Task<ListResponseModel<PotentialGranteeResponseModel>> GetProjectPotentialGranteesAsync(
        [FromRoute] Guid id)
    {
        var (accessClient, userId) = await GetAccessClientTypeAsync(id);

        var projects =
            await _projectRepository.GetManyByOrganizationIdWriteAccessAsync(id,
                userId,
                accessClient);
        var projectResponses =
            projects.Select(project => new PotentialGranteeResponseModel(project));

        return new ListResponseModel<PotentialGranteeResponseModel>(projectResponses);
    }

    private async Task CheckUserHasWriteAccessToProjectAsync(Project project)
    {
        if (project == null)
        {
            throw new NotFoundException();
        }

        var (accessClient, userId) = await GetAccessClientTypeAsync(project.OrganizationId);
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

    private async Task CheckUserHasWriteAccessToServiceAccountAsync(ServiceAccount serviceAccount)
    {
        if (serviceAccount == null)
        {
            throw new NotFoundException();
        }

        var (accessClient, userId) = await GetAccessClientTypeAsync(serviceAccount.OrganizationId);
        var hasAccess = accessClient switch
        {
            AccessClientType.NoAccessCheck => true,
            AccessClientType.User => await _serviceAccountRepository.UserHasWriteAccessToServiceAccount(
                serviceAccount.Id, userId),
            _ => false,
        };

        if (!hasAccess)
        {
            throw new NotFoundException();
        }
    }

    private async Task<(AccessClientType AccessClientType, Guid UserId)> GetAccessClientTypeAsync(Guid organizationId)
    {
        if (!_currentContext.AccessSecretsManager(organizationId))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User).Value;
        var orgAdmin = await _currentContext.OrganizationAdmin(organizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);
        return (accessClient, userId);
    }
}
