using Bit.Api.Models.Response;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Api.SecretsManager.Models.Response;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.AuthorizationRequirements;
using Bit.Core.SecretsManager.Commands.AccessPolicies.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Queries.AccessPolicies.Interfaces;
using Bit.Core.SecretsManager.Queries.Interfaces;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.SecretsManager.Controllers;

[Authorize("secrets")]
[Route("access-policies")]
public class AccessPoliciesController : Controller
{
    private const int _maxBulkCreation = 15;
    private readonly IAccessPolicyRepository _accessPolicyRepository;
    private readonly ICreateAccessPoliciesCommand _createAccessPoliciesCommand;
    private readonly ICurrentContext _currentContext;
    private readonly IDeleteAccessPolicyCommand _deleteAccessPolicyCommand;
    private readonly IProjectRepository _projectRepository;
    private readonly IServiceAccountRepository _serviceAccountRepository;
    private readonly IUpdateAccessPolicyCommand _updateAccessPolicyCommand;
    private readonly IAccessClientQuery _accessClientQuery;
    private readonly IServiceAccountGrantedPolicyUpdatesQuery _serviceAccountGrantedPolicyUpdatesQuery;
    private readonly IUserService _userService;
    private readonly IAuthorizationService _authorizationService;

    public AccessPoliciesController(
        IAuthorizationService authorizationService,
        IUserService userService,
        ICurrentContext currentContext,
        IAccessPolicyRepository accessPolicyRepository,
        IServiceAccountRepository serviceAccountRepository,
        IProjectRepository projectRepository,
        IAccessClientQuery accessClientQuery,
        IServiceAccountGrantedPolicyUpdatesQuery serviceAccountGrantedPolicyUpdatesQuery,
        ICreateAccessPoliciesCommand createAccessPoliciesCommand,
        IDeleteAccessPolicyCommand deleteAccessPolicyCommand,
        IUpdateAccessPolicyCommand updateAccessPolicyCommand)
    {
        _authorizationService = authorizationService;
        _userService = userService;
        _currentContext = currentContext;
        _serviceAccountRepository = serviceAccountRepository;
        _projectRepository = projectRepository;
        _accessPolicyRepository = accessPolicyRepository;
        _createAccessPoliciesCommand = createAccessPoliciesCommand;
        _deleteAccessPolicyCommand = deleteAccessPolicyCommand;
        _updateAccessPolicyCommand = updateAccessPolicyCommand;
        _accessClientQuery = accessClientQuery;
        _serviceAccountGrantedPolicyUpdatesQuery = serviceAccountGrantedPolicyUpdatesQuery;
    }

    [HttpPost("/projects/{id}/access-policies")]
    public async Task<ProjectAccessPoliciesResponseModel> CreateProjectAccessPoliciesAsync([FromRoute] Guid id,
        [FromBody] AccessPoliciesCreateRequest request)
    {
        if (request.Count() > _maxBulkCreation)
        {
            throw new BadRequestException($"Can process no more than {_maxBulkCreation} creation requests at once.");
        }

        var project = await _projectRepository.GetByIdAsync(id);
        if (project == null)
        {
            throw new NotFoundException();
        }

        var policies = request.ToBaseAccessPoliciesForProject(id, project.OrganizationId);
        foreach (var policy in policies)
        {
            var authorizationResult = await _authorizationService.AuthorizeAsync(User, policy, AccessPolicyOperations.Create);
            if (!authorizationResult.Succeeded)
            {
                throw new NotFoundException();
            }
        }

        var results = await _createAccessPoliciesCommand.CreateManyAsync(policies);
        return new ProjectAccessPoliciesResponseModel(results);
    }

    [HttpGet("/projects/{id}/access-policies")]
    public async Task<ProjectAccessPoliciesResponseModel> GetProjectAccessPoliciesAsync([FromRoute] Guid id)
    {
        var project = await _projectRepository.GetByIdAsync(id);
        var (_, userId) = await CheckUserHasWriteAccessToProjectAsync(project);
        var results = await _accessPolicyRepository.GetManyByGrantedProjectIdAsync(id, userId);
        return new ProjectAccessPoliciesResponseModel(results);
    }

    [HttpPost("/service-accounts/{id}/granted-policies")]
    public async Task<ListResponseModel<ServiceAccountProjectAccessPolicyResponseModel>>
        CreateServiceAccountGrantedPoliciesAsync([FromRoute] Guid id,
            [FromBody] List<GrantedAccessPolicyRequest> requests)
    {
        if (requests.Count > _maxBulkCreation)
        {
            throw new BadRequestException($"Can process no more than {_maxBulkCreation} creation requests at once.");
        }

        if (requests.Count != requests.DistinctBy(request => request.GrantedId).Count())
        {
            throw new BadRequestException("Resources must be unique");
        }

        var serviceAccount = await _serviceAccountRepository.GetByIdAsync(id);
        if (serviceAccount == null)
        {
            throw new NotFoundException();
        }

        var policies = requests.Select(request => request.ToServiceAccountProjectAccessPolicy(id, serviceAccount.OrganizationId)).ToList();
        foreach (var policy in policies)
        {
            var authorizationResult = await _authorizationService.AuthorizeAsync(User, policy, AccessPolicyOperations.Create);
            if (!authorizationResult.Succeeded)
            {
                throw new NotFoundException();
            }
        }

        var results =
            await _createAccessPoliciesCommand.CreateManyAsync(new List<BaseAccessPolicy>(policies));
        var responses = results.Select(ap =>
            new ServiceAccountProjectAccessPolicyResponseModel((ServiceAccountProjectAccessPolicy)ap));
        return new ListResponseModel<ServiceAccountProjectAccessPolicyResponseModel>(responses);
    }

    [HttpPut("{id}")]
    public async Task<BaseAccessPolicyResponseModel> UpdateAccessPolicyAsync([FromRoute] Guid id,
        [FromBody] AccessPolicyUpdateRequest request)
    {
        var ap = await _accessPolicyRepository.GetByIdAsync(id);
        var authorizationResult =
            await _authorizationService.AuthorizeAsync(User, ap, AccessPolicyOperations.Update);
        if (!authorizationResult.Succeeded)
        {
            throw new NotFoundException();
        }

        var result = await _updateAccessPolicyCommand.UpdateAsync(id, request.Read, request.Write);

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
        var ap = await _accessPolicyRepository.GetByIdAsync(id);
        var authorizationResult =
            await _authorizationService.AuthorizeAsync(User, ap, AccessPolicyOperations.Delete);
        if (!authorizationResult.Succeeded)
        {
            throw new NotFoundException();
        }

        await _deleteAccessPolicyCommand.DeleteAsync(id);
    }

    [HttpGet("/organizations/{id}/access-policies/people/potential-grantees")]
    public async Task<ListResponseModel<PotentialGranteeResponseModel>> GetPeoplePotentialGranteesAsync(
        [FromRoute] Guid id)
    {
        if (!_currentContext.AccessSecretsManager(id))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User).Value;
        var peopleGrantees = await _accessPolicyRepository.GetPeopleGranteesAsync(id, userId);

        var userResponses = peopleGrantees.UserGrantees.Select(ug => new PotentialGranteeResponseModel(ug));
        var groupResponses = peopleGrantees.GroupGrantees.Select(g => new PotentialGranteeResponseModel(g));
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

    [HttpGet("/projects/{id}/access-policies/people")]
    public async Task<ProjectPeopleAccessPoliciesResponseModel> GetProjectPeopleAccessPoliciesAsync([FromRoute] Guid id)
    {
        var project = await _projectRepository.GetByIdAsync(id);
        var (_, userId) = await CheckUserHasWriteAccessToProjectAsync(project);
        var results = await _accessPolicyRepository.GetPeoplePoliciesByGrantedProjectIdAsync(id, userId);
        return new ProjectPeopleAccessPoliciesResponseModel(results, userId);
    }

    [HttpPut("/projects/{id}/access-policies/people")]
    public async Task<ProjectPeopleAccessPoliciesResponseModel> PutProjectPeopleAccessPoliciesAsync([FromRoute] Guid id,
        [FromBody] PeopleAccessPoliciesRequestModel request)
    {
        var project = await _projectRepository.GetByIdAsync(id);
        if (project == null)
        {
            throw new NotFoundException();
        }

        var peopleAccessPolicies = request.ToProjectPeopleAccessPolicies(id, project.OrganizationId);

        var authorizationResult = await _authorizationService.AuthorizeAsync(User, peopleAccessPolicies,
            ProjectPeopleAccessPoliciesOperations.Replace);
        if (!authorizationResult.Succeeded)
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User).Value;
        var results = await _accessPolicyRepository.ReplaceProjectPeopleAsync(peopleAccessPolicies, userId);
        return new ProjectPeopleAccessPoliciesResponseModel(results, userId);
    }

    [HttpGet("/service-accounts/{id}/access-policies/people")]
    public async Task<ServiceAccountPeopleAccessPoliciesResponseModel> GetServiceAccountPeopleAccessPoliciesAsync(
        [FromRoute] Guid id)
    {
        var serviceAccount = await _serviceAccountRepository.GetByIdAsync(id);
        var (_, userId) = await CheckUserHasWriteAccessToServiceAccountAsync(serviceAccount);
        var results = await _accessPolicyRepository.GetPeoplePoliciesByGrantedServiceAccountIdAsync(id, userId);
        return new ServiceAccountPeopleAccessPoliciesResponseModel(results, userId);
    }

    [HttpPut("/service-accounts/{id}/access-policies/people")]
    public async Task<ServiceAccountPeopleAccessPoliciesResponseModel> PutServiceAccountPeopleAccessPoliciesAsync(
        [FromRoute] Guid id,
        [FromBody] PeopleAccessPoliciesRequestModel request)
    {
        var serviceAccount = await _serviceAccountRepository.GetByIdAsync(id);
        if (serviceAccount == null)
        {
            throw new NotFoundException();
        }

        var peopleAccessPolicies = request.ToServiceAccountPeopleAccessPolicies(id, serviceAccount.OrganizationId);

        var authorizationResult = await _authorizationService.AuthorizeAsync(User, peopleAccessPolicies,
            ServiceAccountPeopleAccessPoliciesOperations.Replace);
        if (!authorizationResult.Succeeded)
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User)!.Value;
        var results = await _accessPolicyRepository.ReplaceServiceAccountPeopleAsync(peopleAccessPolicies, userId);
        return new ServiceAccountPeopleAccessPoliciesResponseModel(results, userId);
    }

    [HttpGet("/service-accounts/{id}/granted-policies")]
    public async Task<ServiceAccountGrantedPoliciesPermissionDetailsResponseModel>
        GetServiceAccountGrantedPoliciesAsync([FromRoute] Guid id)
    {
        var serviceAccount = await _serviceAccountRepository.GetByIdAsync(id);
        var authorizationResult =
            await _authorizationService.AuthorizeAsync(User, serviceAccount, ServiceAccountOperations.Update);

        if (!authorizationResult.Succeeded)
        {
            throw new NotFoundException();
        }

        var (accessClient, userId) = await _accessClientQuery.GetAccessClientAsync(User, serviceAccount.OrganizationId);
        var results =
            await _accessPolicyRepository.GetServiceAccountGrantedPoliciesPermissionDetailsAsync(id, userId,
                accessClient);
        return new ServiceAccountGrantedPoliciesPermissionDetailsResponseModel(results);
    }


    [HttpPut("/service-accounts/{id}/granted-policies")]
    public async Task<ServiceAccountGrantedPoliciesPermissionDetailsResponseModel>
        PutServiceAccountGrantedPoliciesAsync([FromRoute] Guid id,
            [FromBody] ServiceAccountGrantedPoliciesRequestModel request)
    {
        var serviceAccount = await _serviceAccountRepository.GetByIdAsync(id) ?? throw new NotFoundException();
        var grantedPoliciesUpdates = await _serviceAccountGrantedPolicyUpdatesQuery.GetAsync(request.ToGrantedPolicies(serviceAccount));

        var authorizationResult = await _authorizationService.AuthorizeAsync(User, grantedPoliciesUpdates,
            ServiceAccountGrantedPoliciesOperations.Updates);
        if (!authorizationResult.Succeeded)
        {
            throw new NotFoundException();
        }

        var (accessClient, userId) = await _accessClientQuery.GetAccessClientAsync(User, serviceAccount.OrganizationId);
        await _accessPolicyRepository.UpdateServiceAccountGrantedPoliciesAsync(grantedPoliciesUpdates);
        var results =
            await _accessPolicyRepository.GetServiceAccountGrantedPoliciesPermissionDetailsAsync(id, userId,
                accessClient);
        return new ServiceAccountGrantedPoliciesPermissionDetailsResponseModel(results);
    }

    private async Task<(AccessClientType AccessClientType, Guid UserId)> CheckUserHasWriteAccessToProjectAsync(Project project)
    {
        if (project == null)
        {
            throw new NotFoundException();
        }

        var (accessClient, userId) = await GetAccessClientTypeAsync(project.OrganizationId);
        var access = await _projectRepository.AccessToProjectAsync(project.Id, userId, accessClient);
        if (!access.Write || accessClient == AccessClientType.ServiceAccount)
        {
            throw new NotFoundException();
        }
        return (accessClient, userId);
    }

    private async Task<(AccessClientType AccessClientType, Guid UserId)> CheckUserHasWriteAccessToServiceAccountAsync(ServiceAccount serviceAccount)
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

        return (accessClient, userId);
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
