﻿using Bit.Api.Models.Response;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Api.SecretsManager.Models.Response;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Identity;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.AuthorizationRequirements;
using Bit.Core.SecretsManager.Commands.Secrets.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Models.Data.AccessPolicyUpdates;
using Bit.Core.SecretsManager.Queries.AccessPolicies.Interfaces;
using Bit.Core.SecretsManager.Queries.Interfaces;
using Bit.Core.SecretsManager.Queries.Secrets.Interfaces;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.SecretsManager.Controllers;

[Authorize("secrets")]
public class SecretsController : Controller
{
    private readonly ICurrentContext _currentContext;
    private readonly IProjectRepository _projectRepository;
    private readonly ISecretRepository _secretRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ICreateSecretCommand _createSecretCommand;
    private readonly IUpdateSecretCommand _updateSecretCommand;
    private readonly IDeleteSecretCommand _deleteSecretCommand;
    private readonly IAccessClientQuery _accessClientQuery;
    private readonly ISecretsSyncQuery _secretsSyncQuery;
    private readonly ISecretAccessPoliciesUpdatesQuery _secretAccessPoliciesUpdatesQuery;
    private readonly IUserService _userService;
    private readonly IEventService _eventService;
    private readonly IReferenceEventService _referenceEventService;
    private readonly IAuthorizationService _authorizationService;

    public SecretsController(
        ICurrentContext currentContext,
        IProjectRepository projectRepository,
        ISecretRepository secretRepository,
        IOrganizationRepository organizationRepository,
        ICreateSecretCommand createSecretCommand,
        IUpdateSecretCommand updateSecretCommand,
        IDeleteSecretCommand deleteSecretCommand,
        IAccessClientQuery accessClientQuery,
        ISecretsSyncQuery secretsSyncQuery,
        ISecretAccessPoliciesUpdatesQuery secretAccessPoliciesUpdatesQuery,
        IUserService userService,
        IEventService eventService,
        IReferenceEventService referenceEventService,
        IAuthorizationService authorizationService)
    {
        _currentContext = currentContext;
        _projectRepository = projectRepository;
        _secretRepository = secretRepository;
        _organizationRepository = organizationRepository;
        _createSecretCommand = createSecretCommand;
        _updateSecretCommand = updateSecretCommand;
        _deleteSecretCommand = deleteSecretCommand;
        _accessClientQuery = accessClientQuery;
        _secretsSyncQuery = secretsSyncQuery;
        _secretAccessPoliciesUpdatesQuery = secretAccessPoliciesUpdatesQuery;
        _userService = userService;
        _eventService = eventService;
        _referenceEventService = referenceEventService;
        _authorizationService = authorizationService;

    }

    [HttpGet("organizations/{organizationId}/secrets")]
    public async Task<SecretWithProjectsListResponseModel> ListByOrganizationAsync([FromRoute] Guid organizationId)
    {
        if (!_currentContext.AccessSecretsManager(organizationId))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User).Value;
        var orgAdmin = await _currentContext.OrganizationAdmin(organizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.IdentityClientType, orgAdmin);

        var secrets = await _secretRepository.GetManyDetailsByOrganizationIdAsync(organizationId, userId, accessClient);

        return new SecretWithProjectsListResponseModel(secrets);
    }

    [HttpPost("organizations/{organizationId}/secrets")]
    public async Task<SecretResponseModel> CreateAsync([FromRoute] Guid organizationId,
        [FromBody] SecretCreateRequestModel createRequest)
    {
        var secret = createRequest.ToSecret(organizationId);
        var authorizationResult = await _authorizationService.AuthorizeAsync(User, secret, SecretOperations.Create);
        if (!authorizationResult.Succeeded)
        {
            throw new NotFoundException();
        }

        SecretAccessPoliciesUpdates accessPoliciesUpdates = null;
        if (createRequest.AccessPoliciesRequests != null)
        {
            secret.SetNewId();
            accessPoliciesUpdates =
                new SecretAccessPoliciesUpdates(
                    createRequest.AccessPoliciesRequests.ToSecretAccessPolicies(secret.Id, organizationId));
            var accessPolicyAuthorizationResult = await _authorizationService.AuthorizeAsync(User,
                accessPoliciesUpdates, SecretAccessPoliciesOperations.Create);
            if (!accessPolicyAuthorizationResult.Succeeded)
            {
                throw new NotFoundException();
            }
        }

        var result = await _createSecretCommand.CreateAsync(secret, accessPoliciesUpdates);

        // Creating a secret means you have read & write permission.
        return new SecretResponseModel(result, true, true);
    }

    [HttpGet("secrets/{id}")]
    public async Task<SecretResponseModel> GetAsync([FromRoute] Guid id)
    {
        var secret = await _secretRepository.GetByIdAsync(id);

        if (secret == null || !_currentContext.AccessSecretsManager(secret.OrganizationId))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User).Value;
        var orgAdmin = await _currentContext.OrganizationAdmin(secret.OrganizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.IdentityClientType, orgAdmin);

        var access = await _secretRepository.AccessToSecretAsync(id, userId, accessClient);

        if (!access.Read)
        {
            throw new NotFoundException();
        }

        if (_currentContext.IdentityClientType == IdentityClientType.ServiceAccount)
        {
            await _eventService.LogServiceAccountSecretEventAsync(userId, secret, EventType.Secret_Retrieved);

            var org = await _organizationRepository.GetByIdAsync(secret.OrganizationId);
            await _referenceEventService.RaiseEventAsync(new ReferenceEvent(ReferenceEventType.SmServiceAccountAccessedSecret, org, _currentContext));
        }

        return new SecretResponseModel(secret, access.Read, access.Write);
    }

    [HttpGet("projects/{projectId}/secrets")]
    public async Task<SecretWithProjectsListResponseModel> GetSecretsByProjectAsync([FromRoute] Guid projectId)
    {
        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null || !_currentContext.AccessSecretsManager(project.OrganizationId))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User).Value;
        var orgAdmin = await _currentContext.OrganizationAdmin(project.OrganizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.IdentityClientType, orgAdmin);

        var secrets = await _secretRepository.GetManyDetailsByProjectIdAsync(projectId, userId, accessClient);

        return new SecretWithProjectsListResponseModel(secrets);
    }

    [HttpPut("secrets/{id}")]
    public async Task<SecretResponseModel> UpdateSecretAsync([FromRoute] Guid id, [FromBody] SecretUpdateRequestModel updateRequest)
    {
        var secret = await _secretRepository.GetByIdAsync(id);
        if (secret == null)
        {
            throw new NotFoundException();
        }

        var updatedSecret = updateRequest.ToSecret(secret);
        var authorizationResult = await _authorizationService.AuthorizeAsync(User, updatedSecret, SecretOperations.Update);
        if (!authorizationResult.Succeeded)
        {
            throw new NotFoundException();
        }

        SecretAccessPoliciesUpdates accessPoliciesUpdates = null;
        if (updateRequest.AccessPoliciesRequests != null)
        {
            var userId = _userService.GetProperUserId(User)!.Value;
            accessPoliciesUpdates = await _secretAccessPoliciesUpdatesQuery.GetAsync(updateRequest.AccessPoliciesRequests.ToSecretAccessPolicies(id, secret.OrganizationId), userId);

            var accessPolicyAuthorizationResult = await _authorizationService.AuthorizeAsync(User, accessPoliciesUpdates, SecretAccessPoliciesOperations.Updates);
            if (!accessPolicyAuthorizationResult.Succeeded)
            {
                throw new NotFoundException();
            }

        }

        var result = await _updateSecretCommand.UpdateAsync(updatedSecret, accessPoliciesUpdates);

        // Updating a secret means you have read & write permission.
        return new SecretResponseModel(result, true, true);
    }

    [HttpPost("secrets/delete")]
    public async Task<ListResponseModel<BulkDeleteResponseModel>> BulkDeleteAsync([FromBody] List<Guid> ids)
    {
        var secrets = (await _secretRepository.GetManyByIds(ids)).ToList();
        if (!secrets.Any() || secrets.Count != ids.Count)
        {
            throw new NotFoundException();
        }

        // Ensure all secrets belong to the same organization.
        var organizationId = secrets.First().OrganizationId;
        if (secrets.Any(secret => secret.OrganizationId != organizationId) ||
            !_currentContext.AccessSecretsManager(organizationId))
        {
            throw new NotFoundException();
        }

        var secretsToDelete = new List<Secret>();
        var results = new List<(Secret Secret, string Error)>();

        foreach (var secret in secrets)
        {
            var authorizationResult =
                await _authorizationService.AuthorizeAsync(User, secret, SecretOperations.Delete);
            if (authorizationResult.Succeeded)
            {
                secretsToDelete.Add(secret);
                results.Add((secret, ""));
            }
            else
            {
                results.Add((secret, "access denied"));
            }
        }

        await _deleteSecretCommand.DeleteSecrets(secretsToDelete);
        var responses = results.Select(r => new BulkDeleteResponseModel(r.Secret.Id, r.Error));
        return new ListResponseModel<BulkDeleteResponseModel>(responses);
    }

    [HttpPost("secrets/get-by-ids")]
    public async Task<ListResponseModel<BaseSecretResponseModel>> GetSecretsByIdsAsync(
        [FromBody] GetSecretsRequestModel request)
    {
        var secrets = (await _secretRepository.GetManyByIds(request.Ids)).ToList();
        if (!secrets.Any() || secrets.Count != request.Ids.Count())
        {
            throw new NotFoundException();
        }

        var authorizationResult = await _authorizationService.AuthorizeAsync(User, secrets, BulkSecretOperations.ReadAll);
        if (!authorizationResult.Succeeded)
        {
            throw new NotFoundException();
        }

        await LogSecretsRetrievalAsync(secrets.First().OrganizationId, secrets);

        var responses = secrets.Select(s => new BaseSecretResponseModel(s));
        return new ListResponseModel<BaseSecretResponseModel>(responses);
    }

    [HttpGet("/organizations/{organizationId}/secrets/sync")]
    public async Task<SecretsSyncResponseModel> GetSecretsSyncAsync([FromRoute] Guid organizationId,
        [FromQuery] DateTime? lastSyncedDate = null)
    {
        if (lastSyncedDate.HasValue && lastSyncedDate.Value > DateTime.UtcNow)
        {
            throw new BadRequestException("Last synced date must be in the past.");
        }

        if (!_currentContext.AccessSecretsManager(organizationId))
        {
            throw new NotFoundException();
        }

        var (accessClient, serviceAccountId) = await _accessClientQuery.GetAccessClientAsync(User, organizationId);
        if (accessClient != AccessClientType.ServiceAccount)
        {
            throw new BadRequestException("Only service accounts can sync secrets.");
        }

        var syncRequest = new SecretsSyncRequest
        {
            AccessClientType = accessClient,
            OrganizationId = organizationId,
            ServiceAccountId = serviceAccountId,
            LastSyncedDate = lastSyncedDate
        };
        var syncResult = await _secretsSyncQuery.GetAsync(syncRequest);

        if (syncResult.HasChanges)
        {
            await LogSecretsRetrievalAsync(organizationId, syncResult.Secrets);
        }

        return new SecretsSyncResponseModel(syncResult.HasChanges, syncResult.Secrets);
    }

    private async Task LogSecretsRetrievalAsync(Guid organizationId, IEnumerable<Secret> secrets)
    {
        if (_currentContext.IdentityClientType == IdentityClientType.ServiceAccount)
        {
            var userId = _userService.GetProperUserId(User)!.Value;
            var org = await _organizationRepository.GetByIdAsync(organizationId);
            await _eventService.LogServiceAccountSecretsEventAsync(userId, secrets, EventType.Secret_Retrieved);
            await _referenceEventService.RaiseEventAsync(
                new ReferenceEvent(ReferenceEventType.SmServiceAccountAccessedSecret, org, _currentContext));
        }
    }
}
