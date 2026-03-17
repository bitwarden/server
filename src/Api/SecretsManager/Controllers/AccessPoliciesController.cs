using Bit.Api.Models.Response;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Api.SecretsManager.Models.Response;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.AuthorizationRequirements;
using Bit.Core.SecretsManager.Commands.AccessPolicies.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Enums.AccessPolicies;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Models.Data.AccessPolicyUpdates;
using Bit.Core.SecretsManager.Queries.AccessPolicies.Interfaces;
using Bit.Core.SecretsManager.Queries.Interfaces;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

#nullable enable

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
    private readonly IEventService _eventService;
    private readonly IProjectServiceAccountsAccessPoliciesUpdatesQuery
        _projectServiceAccountsAccessPoliciesUpdatesQuery;
    private readonly IUpdateProjectServiceAccountsAccessPoliciesCommand
        _updateProjectServiceAccountsAccessPoliciesCommand;
    private readonly ISecretAccessPoliciesUpdatesQuery _secretAccessPoliciesUpdatesQuery;
    private readonly IUpdateSecretAccessPoliciesCommand _updateSecretAccessPoliciesCommand;


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
        IUpdateProjectServiceAccountsAccessPoliciesCommand updateProjectServiceAccountsAccessPoliciesCommand,
        ISecretAccessPoliciesUpdatesQuery secretAccessPoliciesUpdatesQuery,
        IUpdateSecretAccessPoliciesCommand updateSecretAccessPoliciesCommand,
        IEventService eventService)
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
        _secretAccessPoliciesUpdatesQuery = secretAccessPoliciesUpdatesQuery;
        _updateSecretAccessPoliciesCommand = updateSecretAccessPoliciesCommand;
        _eventService = eventService;
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
        var (_, userId) = await CheckUserHasManageAccessToProjectAsync(project);
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
        var currentPolicies = await _accessPolicyRepository.GetPeoplePoliciesByGrantedProjectIdAsync(id, userId) ?? [];
        var results = await _accessPolicyRepository.ReplaceProjectPeopleAsync(peopleAccessPolicies, userId);
        await LogProjectPeopleAccessPolicyChangesAsync(currentPolicies, results, userId, project.OrganizationId);
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
        var currentPolicies = await _accessPolicyRepository.GetPeoplePoliciesByGrantedServiceAccountIdAsync(peopleAccessPolicies.Id, userId);
        var results = await _accessPolicyRepository.ReplaceServiceAccountPeopleAsync(peopleAccessPolicies, userId);
        await LogAccessPolicyServiceAccountChanges(currentPolicies, results, userId);
        return new ServiceAccountPeopleAccessPoliciesResponseModel(results, userId);
    }

    [HttpGet("/service-accounts/{id}/granted-policies")]
    public async Task<ServiceAccountGrantedPoliciesPermissionDetailsResponseModel>
        GetServiceAccountGrantedPoliciesAsync([FromRoute] Guid id)
    {
        var serviceAccount = await _serviceAccountRepository.GetByIdAsync(id);
        if (serviceAccount == null)
        {
            throw new NotFoundException();
        }

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

        var userId = _userService.GetProperUserId(User)!.Value;
        await _updateServiceAccountGrantedPoliciesCommand.UpdateAsync(grantedPoliciesUpdates);
        await LogServiceAccountGrantedPolicyChangesAsync(grantedPoliciesUpdates, userId);
        return await GetServiceAccountGrantedPoliciesAsync(serviceAccount);
    }

    [HttpGet("/projects/{id}/access-policies/service-accounts")]
    public async Task<ProjectServiceAccountsAccessPoliciesResponseModel>
        GetProjectServiceAccountsAccessPoliciesAsync(
            [FromRoute] Guid id)
    {
        var project = await _projectRepository.GetByIdAsync(id);
        await CheckUserHasManageAccessToProjectAsync(project);
        var results =
            await _accessPolicyRepository.GetProjectServiceAccountsAccessPoliciesAsync(id)
            ?? new ProjectServiceAccountsAccessPolicies();
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

        var userId = _userService.GetProperUserId(User)!.Value;
        await _updateProjectServiceAccountsAccessPoliciesCommand.UpdateAsync(accessPoliciesUpdates);
        await LogProjectServiceAccountAccessPolicyChangesAsync(accessPoliciesUpdates, userId);

        var results = await _accessPolicyRepository.GetProjectServiceAccountsAccessPoliciesAsync(id)
            ?? new ProjectServiceAccountsAccessPolicies();
        return new ProjectServiceAccountsAccessPoliciesResponseModel(results);
    }

    [HttpGet("/secrets/{secretId}/access-policies")]
    public async Task<SecretAccessPoliciesResponseModel> GetSecretAccessPoliciesAsync(Guid secretId)
    {
        var secret = await _secretRepository.GetByIdAsync(secretId);
        if (secret == null)
        {
            throw new NotFoundException();
        }

        var authorizationResult = await _authorizationService.AuthorizeAsync(User, secret, SecretOperations.ReadAccessPolicies);

        if (!authorizationResult.Succeeded)
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User)!.Value;
        var accessPolicies = await _accessPolicyRepository.GetSecretAccessPoliciesAsync(secretId, userId)
            ?? new SecretAccessPolicies { SecretId = secretId, OrganizationId = secret!.OrganizationId };
        return new SecretAccessPoliciesResponseModel(accessPolicies, userId);
    }

    [HttpPut("/secrets/{secretId}/access-policies")]
    public async Task<SecretAccessPoliciesResponseModel> PutSecretAccessPoliciesAsync(
        [FromRoute] Guid secretId,
        [FromBody] SecretAccessPoliciesRequestsModel request)
    {
        var secret = await _secretRepository.GetByIdAsync(secretId);
        if (secret == null)
        {
            throw new NotFoundException();
        }

        var (accessClient, _) = await _accessClientQuery.GetAccessClientAsync(User, secret.OrganizationId);
        if (accessClient == AccessClientType.ServiceAccount)
        {
            throw new NotFoundException();
        }

        var totalPolicies =
            (request.UserAccessPolicyRequests?.Count() ?? 0) +
            (request.GroupAccessPolicyRequests?.Count() ?? 0) +
            (request.ServiceAccountAccessPolicyRequests?.Count() ?? 0);
        if (totalPolicies == 0)
        {
            throw new BadRequestException("At least one policy entry is required.");
        }

        var userId = _userService.GetProperUserId(User)!.Value;
        var accessPoliciesUpdates = await _secretAccessPoliciesUpdatesQuery.GetAsync(
            request.ToSecretAccessPolicies(secretId, secret.OrganizationId), userId);

        var authorizationResult = await _authorizationService.AuthorizeAsync(
            User, accessPoliciesUpdates, SecretAccessPoliciesOperations.Updates);
        if (!authorizationResult.Succeeded)
        {
            throw new NotFoundException();
        }

        await _updateSecretAccessPoliciesCommand.UpdateAsync(accessPoliciesUpdates);
        await LogSecretAccessPolicyChangesAsync(accessPoliciesUpdates, userId);

        var results = await _accessPolicyRepository.GetSecretAccessPoliciesAsync(secretId, userId)
            ?? new SecretAccessPolicies { SecretId = secretId, OrganizationId = secret.OrganizationId };
        return new SecretAccessPoliciesResponseModel(results, userId);
    }

    private async Task<(AccessClientType AccessClientType, Guid UserId)> CheckUserHasManageAccessToProjectAsync(
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
        if (!access.Manage)
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

    private async Task LogAccessPolicyServiceAccountChanges(IEnumerable<BaseAccessPolicy> currentPolicies, IEnumerable<BaseAccessPolicy> updatedPolicies, Guid userId)
    {
        foreach (var current in currentPolicies.OfType<GroupServiceAccountAccessPolicy>())
        {
            if (!updatedPolicies.Any(r => r.Id == current.Id))
            {
                await _eventService.LogServiceAccountGroupEventAsync(userId, current, EventType.ServiceAccount_GroupRemoved, _currentContext.IdentityClientType);
            }
        }

        foreach (var policy in updatedPolicies.OfType<GroupServiceAccountAccessPolicy>())
        {
            if (!currentPolicies.Any(e => e.Id == policy.Id))
            {
                await _eventService.LogServiceAccountGroupEventAsync(userId, policy, EventType.ServiceAccount_GroupAdded, _currentContext.IdentityClientType);
            }
        }

        foreach (var current in currentPolicies.OfType<UserServiceAccountAccessPolicy>())
        {
            if (!updatedPolicies.Any(r => r.Id == current.Id))
            {
                await _eventService.LogServiceAccountPeopleEventAsync(userId, current, EventType.ServiceAccount_UserRemoved, _currentContext.IdentityClientType);
            }
        }

        foreach (var policy in updatedPolicies.OfType<UserServiceAccountAccessPolicy>())
        {
            if (!currentPolicies.Any(e => e.Id == policy.Id))
            {
                await _eventService.LogServiceAccountPeopleEventAsync(userId, policy, EventType.ServiceAccount_UserAdded, _currentContext.IdentityClientType);
            }
        }

        foreach (var policy in updatedPolicies.OfType<UserServiceAccountAccessPolicy>())
        {
            var existing = currentPolicies.OfType<UserServiceAccountAccessPolicy>()
                .FirstOrDefault(p => p.OrganizationUserId == policy.OrganizationUserId);
            if (existing != null && PermissionsChanged(existing, policy))
            {
                await _eventService.LogServiceAccountPeopleEventAsync(userId, policy,
                    EventType.ServiceAccount_UserPermissionUpdated, _currentContext.IdentityClientType);
            }
        }

        foreach (var policy in updatedPolicies.OfType<GroupServiceAccountAccessPolicy>())
        {
            var existing = currentPolicies.OfType<GroupServiceAccountAccessPolicy>()
                .FirstOrDefault(p => p.GroupId == policy.GroupId);
            if (existing != null && PermissionsChanged(existing, policy))
            {
                await _eventService.LogServiceAccountGroupEventAsync(userId, policy,
                    EventType.ServiceAccount_GroupPermissionUpdated, _currentContext.IdentityClientType);
            }
        }
    }

    private async Task LogProjectPeopleAccessPolicyChangesAsync(
        IEnumerable<BaseAccessPolicy> before,
        IEnumerable<BaseAccessPolicy> after,
        Guid userId,
        Guid organizationId)
    {
        foreach (var policy in after.OfType<UserProjectAccessPolicy>())
        {
            var existing = before.OfType<UserProjectAccessPolicy>()
                .FirstOrDefault(p => p.OrganizationUserId == policy.OrganizationUserId);
            var type = existing is null ? EventType.Project_UserAccessGranted
                : PermissionsChanged(existing, policy) ? EventType.Project_UserAccessUpdated
                : (EventType?)null;
            if (type.HasValue)
            {
                await _eventService.LogProjectAccessPolicyEventAsync(userId, organizationId, policy, type.Value,
                    _currentContext.IdentityClientType);
            }
        }

        foreach (var policy in before.OfType<UserProjectAccessPolicy>())
        {
            if (!after.OfType<UserProjectAccessPolicy>().Any(p => p.OrganizationUserId == policy.OrganizationUserId))
            {
                await _eventService.LogProjectAccessPolicyEventAsync(userId, organizationId, policy,
                    EventType.Project_UserAccessRevoked, _currentContext.IdentityClientType);
            }
        }

        foreach (var policy in after.OfType<GroupProjectAccessPolicy>())
        {
            var existing = before.OfType<GroupProjectAccessPolicy>()
                .FirstOrDefault(p => p.GroupId == policy.GroupId);
            var type = existing is null ? EventType.Project_GroupAccessGranted
                : PermissionsChanged(existing, policy) ? EventType.Project_GroupAccessUpdated
                : (EventType?)null;
            if (type.HasValue)
            {
                await _eventService.LogProjectAccessPolicyEventAsync(userId, organizationId, policy, type.Value,
                    _currentContext.IdentityClientType);
            }
        }

        foreach (var policy in before.OfType<GroupProjectAccessPolicy>())
        {
            if (!after.OfType<GroupProjectAccessPolicy>().Any(p => p.GroupId == policy.GroupId))
            {
                await _eventService.LogProjectAccessPolicyEventAsync(userId, organizationId, policy,
                    EventType.Project_GroupAccessRevoked, _currentContext.IdentityClientType);
            }
        }
    }

    private async Task LogProjectServiceAccountAccessPolicyChangesAsync(
        ProjectServiceAccountsAccessPoliciesUpdates? updates,
        Guid userId)
    {
        if (updates is null) return;
        foreach (var update in updates.ServiceAccountAccessPolicyUpdates)
        {
            var type = update.Operation switch
            {
                AccessPolicyOperation.Create => EventType.Project_ServiceAccountAccessGranted,
                AccessPolicyOperation.Update => EventType.Project_ServiceAccountAccessUpdated,
                AccessPolicyOperation.Delete => EventType.Project_ServiceAccountAccessRevoked,
                _ => (EventType?)null
            };
            if (type.HasValue)
            {
                await _eventService.LogProjectAccessPolicyEventAsync(userId, updates.OrganizationId,
                    update.AccessPolicy, type.Value, _currentContext.IdentityClientType);
            }
        }
    }

    private async Task LogSecretAccessPolicyChangesAsync(
        SecretAccessPoliciesUpdates updates,
        Guid userId)
    {
        foreach (var update in updates.UserAccessPolicyUpdates)
        {
            var type = update.Operation switch
            {
                AccessPolicyOperation.Create => EventType.Secret_UserAccessGranted,
                AccessPolicyOperation.Update => EventType.Secret_UserAccessUpdated,
                AccessPolicyOperation.Delete => EventType.Secret_UserAccessRevoked,
                _ => (EventType?)null
            };
            if (type.HasValue)
            {
                await _eventService.LogSecretAccessPolicyEventAsync(userId, updates.OrganizationId,
                    update.AccessPolicy, type.Value, _currentContext.IdentityClientType);
            }
        }

        foreach (var update in updates.GroupAccessPolicyUpdates)
        {
            var type = update.Operation switch
            {
                AccessPolicyOperation.Create => EventType.Secret_GroupAccessGranted,
                AccessPolicyOperation.Update => EventType.Secret_GroupAccessUpdated,
                AccessPolicyOperation.Delete => EventType.Secret_GroupAccessRevoked,
                _ => (EventType?)null
            };
            if (type.HasValue)
            {
                await _eventService.LogSecretAccessPolicyEventAsync(userId, updates.OrganizationId,
                    update.AccessPolicy, type.Value, _currentContext.IdentityClientType);
            }
        }

        foreach (var update in updates.ServiceAccountAccessPolicyUpdates)
        {
            var type = update.Operation switch
            {
                AccessPolicyOperation.Create => EventType.Secret_ServiceAccountAccessGranted,
                AccessPolicyOperation.Update => EventType.Secret_ServiceAccountAccessUpdated,
                AccessPolicyOperation.Delete => EventType.Secret_ServiceAccountAccessRevoked,
                _ => (EventType?)null
            };
            if (type.HasValue)
            {
                await _eventService.LogSecretAccessPolicyEventAsync(userId, updates.OrganizationId,
                    update.AccessPolicy, type.Value, _currentContext.IdentityClientType);
            }
        }
    }

    private async Task LogServiceAccountGrantedPolicyChangesAsync(
        ServiceAccountGrantedPoliciesUpdates? updates,
        Guid userId)
    {
        if (updates is null) return;
        foreach (var update in updates.ProjectGrantedPolicyUpdates)
        {
            var type = update.Operation switch
            {
                AccessPolicyOperation.Create => EventType.Project_ServiceAccountAccessGranted,
                AccessPolicyOperation.Update => EventType.Project_ServiceAccountAccessUpdated,
                AccessPolicyOperation.Delete => EventType.Project_ServiceAccountAccessRevoked,
                _ => (EventType?)null
            };
            if (type.HasValue)
            {
                await _eventService.LogProjectAccessPolicyEventAsync(userId, updates.OrganizationId,
                    update.AccessPolicy, type.Value, _currentContext.IdentityClientType);
            }
        }
    }

    private static bool PermissionsChanged(BaseAccessPolicy before, BaseAccessPolicy after) =>
        before.Read != after.Read || before.Write != after.Write || before.Manage != after.Manage;
}
