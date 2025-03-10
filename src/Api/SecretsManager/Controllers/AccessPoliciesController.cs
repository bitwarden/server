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
public class AccessPoliciesController : Controller
{
    private readonly IAccessClientQuery _accessClientQuery;
    private readonly IAccessPolicyRepository _accessPolicyRepository;
    private readonly IAuthorizationService _authorizationService;
    private readonly ICurrentContext _currentContext;
    private readonly IProjectRepository _projectRepository;
    private readonly ISecretRepository _secretRepository;
    private readonly IServiceAccountGrantedPolicyUpdatesQuery _serviceAccountGrantedPolicyUpdatesQuery;
    private readonly IServiceAccountRepository _serviceAccountRepository;
    private readonly IUpdateServiceAccountGrantedPoliciesCommand _updateServiceAccountGrantedPoliciesCommand;
    private readonly IUserService _userService;
    private readonly IProjectServiceAccountsAccessPoliciesUpdatesQuery
        _projectServiceAccountsAccessPoliciesUpdatesQuery;
    private readonly IUpdateProjectServiceAccountsAccessPoliciesCommand
        _updateProjectServiceAccountsAccessPoliciesCommand;


    public AccessPoliciesController(
        IAuthorizationService authorizationService,
        IUserService userService,
        ICurrentContext currentContext,
        IAccessPolicyRepository accessPolicyRepository,
        IServiceAccountRepository serviceAccountRepository,
        IProjectRepository projectRepository,
        ISecretRepository secretRepository,
        IAccessClientQuery accessClientQuery,
        IServiceAccountGrantedPolicyUpdatesQuery serviceAccountGrantedPolicyUpdatesQuery,
        IProjectServiceAccountsAccessPoliciesUpdatesQuery projectServiceAccountsAccessPoliciesUpdatesQuery,
        IUpdateServiceAccountGrantedPoliciesCommand updateServiceAccountGrantedPoliciesCommand,
        IUpdateProjectServiceAccountsAccessPoliciesCommand updateProjectServiceAccountsAccessPoliciesCommand)
    {
        _authorizationService = authorizationService;
        _userService = userService;
        _currentContext = currentContext;
        _serviceAccountRepository = serviceAccountRepository;
        _projectRepository = projectRepository;
        _secretRepository = secretRepository;
        _accessPolicyRepository = accessPolicyRepository;
        _updateServiceAccountGrantedPoliciesCommand = updateServiceAccountGrantedPoliciesCommand;
        _accessClientQuery = accessClientQuery;
        _serviceAccountGrantedPolicyUpdatesQuery = serviceAccountGrantedPolicyUpdatesQuery;
        _projectServiceAccountsAccessPoliciesUpdatesQuery = projectServiceAccountsAccessPoliciesUpdatesQuery;
        _updateProjectServiceAccountsAccessPoliciesCommand = updateProjectServiceAccountsAccessPoliciesCommand;
    }

    [HttpGet("/organizations/{id}/access-policies/people/potential-grantees")]
    public async Task<ListResponseModel<PotentialGranteeResponseModel>> GetPeoplePotentialGranteesAsync(
        [FromRoute] Guid id)
    {
        if (!_currentContext.AccessSecretsManager(id))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User)!.Value;
        var peopleGrantees = await _accessPolicyRepository.GetPeopleGranteesAsync(id, userId);

        var userResponses = peopleGrantees.UserGrantees.Select(ug => new PotentialGranteeResponseModel(ug));
        var groupResponses = peopleGrantees.GroupGrantees.Select(g => new PotentialGranteeResponseModel(g));
        return new ListResponseModel<PotentialGranteeResponseModel>(userResponses.Concat(groupResponses));
    }

    [HttpGet("/organizations/{id}/access-policies/service-accounts/potential-grantees")]
    public async Task<ListResponseModel<PotentialGranteeResponseModel>> GetServiceAccountsPotentialGranteesAsync(
        [FromRoute] Guid id)
    {
        if (!_currentContext.AccessSecretsManager(id))
        {
            throw new NotFoundException();
        }

        var (accessClient, userId) = await _accessClientQuery.GetAccessClientAsync(User, id);

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
        if (!_currentContext.AccessSecretsManager(id))
        {
            throw new NotFoundException();
        }

        var (accessClient, userId) = await _accessClientQuery.GetAccessClientAsync(User, id);

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

        var userId = _userService.GetProperUserId(User)!.Value;
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

        return await GetServiceAccountGrantedPoliciesAsync(serviceAccount);
    }


    [HttpPut("/service-accounts/{id}/granted-policies")]
    public async Task<ServiceAccountGrantedPoliciesPermissionDetailsResponseModel>
        PutServiceAccountGrantedPoliciesAsync([FromRoute] Guid id,
            [FromBody] ServiceAccountGrantedPoliciesRequestModel request)
    {
        var serviceAccount = await _serviceAccountRepository.GetByIdAsync(id) ?? throw new NotFoundException();
        var grantedPoliciesUpdates =
            await _serviceAccountGrantedPolicyUpdatesQuery.GetAsync(request.ToGrantedPolicies(serviceAccount));

        var authorizationResult = await _authorizationService.AuthorizeAsync(User, grantedPoliciesUpdates,
            ServiceAccountGrantedPoliciesOperations.Updates);
        if (!authorizationResult.Succeeded)
        {
            throw new NotFoundException();
        }

        await _updateServiceAccountGrantedPoliciesCommand.UpdateAsync(grantedPoliciesUpdates);
        return await GetServiceAccountGrantedPoliciesAsync(serviceAccount);
    }

    [HttpGet("/projects/{id}/access-policies/service-accounts")]
    public async Task<ProjectServiceAccountsAccessPoliciesResponseModel>
        GetProjectServiceAccountsAccessPoliciesAsync(
            [FromRoute] Guid id)
    {
        var project = await _projectRepository.GetByIdAsync(id);
        await CheckUserHasWriteAccessToProjectAsync(project);
        var results =
            await _accessPolicyRepository.GetProjectServiceAccountsAccessPoliciesAsync(id);
        return new ProjectServiceAccountsAccessPoliciesResponseModel(results);
    }

    [HttpPut("/projects/{id}/access-policies/service-accounts")]
    public async Task<ProjectServiceAccountsAccessPoliciesResponseModel>
        PutProjectServiceAccountsAccessPoliciesAsync([FromRoute] Guid id,
            [FromBody] ProjectServiceAccountsAccessPoliciesRequestModel request)
    {
        var project = await _projectRepository.GetByIdAsync(id) ?? throw new NotFoundException();
        var accessPoliciesUpdates =
            await _projectServiceAccountsAccessPoliciesUpdatesQuery.GetAsync(
                request.ToProjectServiceAccountsAccessPolicies(project));

        var authorizationResult = await _authorizationService.AuthorizeAsync(User, accessPoliciesUpdates,
            ProjectServiceAccountsAccessPoliciesOperations.Updates);
        if (!authorizationResult.Succeeded)
        {
            throw new NotFoundException();
        }

        await _updateProjectServiceAccountsAccessPoliciesCommand.UpdateAsync(accessPoliciesUpdates);

        var results = await _accessPolicyRepository.GetProjectServiceAccountsAccessPoliciesAsync(id);
        return new ProjectServiceAccountsAccessPoliciesResponseModel(results);
    }

    [HttpGet("/secrets/{secretId}/access-policies")]
    public async Task<SecretAccessPoliciesResponseModel> GetSecretAccessPoliciesAsync(Guid secretId)
    {
        var secret = await _secretRepository.GetByIdAsync(secretId);
        var authorizationResult = await _authorizationService.AuthorizeAsync(User, secret, SecretOperations.ReadAccessPolicies);

        if (!authorizationResult.Succeeded)
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User)!.Value;
        var accessPolicies = await _accessPolicyRepository.GetSecretAccessPoliciesAsync(secretId, userId);
        return new SecretAccessPoliciesResponseModel(accessPolicies, userId);
    }

    private async Task<(AccessClientType AccessClientType, Guid UserId)> CheckUserHasWriteAccessToProjectAsync(
        Project project)
    {
        if (project == null)
        {
            throw new NotFoundException();
        }

        if (!_currentContext.AccessSecretsManager(project.OrganizationId))
        {
            throw new NotFoundException();
        }

        var (accessClient, userId) = await _accessClientQuery.GetAccessClientAsync(User, project.OrganizationId);

        var access = await _projectRepository.AccessToProjectAsync(project.Id, userId, accessClient);
        if (!access.Write || accessClient == AccessClientType.ServiceAccount)
        {
            throw new NotFoundException();
        }

        return (accessClient, userId);
    }

    private async Task<(AccessClientType AccessClientType, Guid UserId)> CheckUserHasWriteAccessToServiceAccountAsync(
        ServiceAccount serviceAccount)
    {
        if (serviceAccount == null)
        {
            throw new NotFoundException();
        }

        if (!_currentContext.AccessSecretsManager(serviceAccount.OrganizationId))
        {
            throw new NotFoundException();
        }

        var (accessClient, userId) = await _accessClientQuery.GetAccessClientAsync(User, serviceAccount.OrganizationId);

        var access =
            await _serviceAccountRepository.AccessToServiceAccountAsync(serviceAccount.Id, userId, accessClient);
        if (!access.Write)
        {
            throw new NotFoundException();
        }

        return (accessClient, userId);
    }

    private async Task<ServiceAccountGrantedPoliciesPermissionDetailsResponseModel>
        GetServiceAccountGrantedPoliciesAsync(ServiceAccount serviceAccount)
    {
        var (accessClient, userId) = await _accessClientQuery.GetAccessClientAsync(User, serviceAccount.OrganizationId);
        var results =
            await _accessPolicyRepository.GetServiceAccountGrantedPoliciesPermissionDetailsAsync(serviceAccount.Id,
                userId, accessClient);
        return new ServiceAccountGrantedPoliciesPermissionDetailsResponseModel(results);
    }
}
