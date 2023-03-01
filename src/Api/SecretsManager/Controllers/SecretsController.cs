using Bit.Api.Models.Response;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Api.SecretsManager.Models.Response;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Identity;
using Bit.Core.SecretsManager.Commands.Secrets.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.SecretsManager.Controllers;

[SecretsManager]
[Authorize("secrets")]
public class SecretsController : Controller
{
    private readonly ICurrentContext _currentContext;
    private readonly IProjectRepository _projectRepository;
    private readonly ISecretRepository _secretRepository;
    private readonly ICreateSecretCommand _createSecretCommand;
    private readonly IUpdateSecretCommand _updateSecretCommand;
    private readonly IDeleteSecretCommand _deleteSecretCommand;
    private readonly IUserService _userService;
    private readonly IEventService _eventService;

    public SecretsController(
        ICurrentContext currentContext,
        IProjectRepository projectRepository,
        ISecretRepository secretRepository,
        ICreateSecretCommand createSecretCommand,
        IUpdateSecretCommand updateSecretCommand,
        IDeleteSecretCommand deleteSecretCommand,
        IUserService userService,
        IEventService eventService)
    {
        _currentContext = currentContext;
        _projectRepository = projectRepository;
        _secretRepository = secretRepository;
        _createSecretCommand = createSecretCommand;
        _updateSecretCommand = updateSecretCommand;
        _deleteSecretCommand = deleteSecretCommand;
        _userService = userService;
        _eventService = eventService;
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
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);

        var secrets = await _secretRepository.GetManyByOrganizationIdAsync(organizationId, userId, accessClient);

        return new SecretWithProjectsListResponseModel(secrets);
    }

    [HttpPost("organizations/{organizationId}/secrets")]
    public async Task<SecretResponseModel> CreateAsync([FromRoute] Guid organizationId, [FromBody] SecretCreateRequestModel createRequest)
    {
        if (!_currentContext.AccessSecretsManager(organizationId))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User).Value;
        var result = await _createSecretCommand.CreateAsync(createRequest.ToSecret(organizationId), userId);
        return new SecretResponseModel(result);
    }

    [HttpGet("secrets/{id}")]
    public async Task<SecretResponseModel> GetAsync([FromRoute] Guid id)
    {
        var secret = await _secretRepository.GetByIdAsync(id);

        if (secret == null || !_currentContext.AccessSecretsManager(secret.OrganizationId))
        {
            throw new NotFoundException();
        }

        if (!await UserHasReadAccessToSecret(secret))
        {
            throw new NotFoundException();
        }

        if (_currentContext.ClientType == ClientType.ServiceAccount)
        {
            var userId = _userService.GetProperUserId(User).Value;
            await _eventService.LogServiceAccountSecretEventAsync(userId, secret, EventType.Secret_Retrieved);
        }

        return new SecretResponseModel(secret);
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
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);

        var secrets = await _secretRepository.GetManyByProjectIdAsync(projectId, userId, accessClient);

        return new SecretWithProjectsListResponseModel(secrets);
    }

    [HttpPut("secrets/{id}")]
    public async Task<SecretResponseModel> UpdateSecretAsync([FromRoute] Guid id, [FromBody] SecretUpdateRequestModel updateRequest)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var secret = updateRequest.ToSecret(id);
        var result = await _updateSecretCommand.UpdateAsync(secret, userId);
        return new SecretResponseModel(result);
    }

    [HttpPost("secrets/delete")]
    public async Task<ListResponseModel<BulkDeleteResponseModel>> BulkDeleteAsync([FromBody] List<Guid> ids)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var results = await _deleteSecretCommand.DeleteSecrets(ids, userId);
        var responses = results.Select(r => new BulkDeleteResponseModel(r.Item1.Id, r.Item2, r.Item1.Key));
        return new ListResponseModel<BulkDeleteResponseModel>(responses);
    }

    public async Task<bool> UserHasReadAccessToSecret(Secret secret)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var orgAdmin = await _currentContext.OrganizationAdmin(secret.OrganizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);
        var hasAccess = orgAdmin;

        if (secret.Projects?.Count > 0)
        {
            Guid projectId = secret.Projects.FirstOrDefault().Id;
            hasAccess = accessClient switch
            {
                AccessClientType.NoAccessCheck => true,
                AccessClientType.User => await _projectRepository.UserHasReadAccessToProject(projectId, userId),
                AccessClientType.ServiceAccount => await _projectRepository.ServiceAccountHasReadAccessToProject(projectId, userId),
                _ => false,
            };
        }

        return hasAccess;
    }
}
