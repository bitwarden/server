using Bit.Api.Models.Response;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Api.SecretsManager.Models.Response;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Identity;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Commands.Secrets.Interfaces;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.SecretsManager.Controllers;

[Authorize("secrets")]
[SelfHosted(NotSelfHostedOnly = true)]
public class SecretsController : Controller
{
    private readonly ICurrentContext _currentContext;
    private readonly IProjectRepository _projectRepository;
    private readonly ISecretRepository _secretRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ICreateSecretCommand _createSecretCommand;
    private readonly IUpdateSecretCommand _updateSecretCommand;
    private readonly IDeleteSecretCommand _deleteSecretCommand;
    private readonly IUserService _userService;
    private readonly IEventService _eventService;
    private readonly IReferenceEventService _referenceEventService;

    public SecretsController(
        ICurrentContext currentContext,
        IProjectRepository projectRepository,
        ISecretRepository secretRepository,
        IOrganizationRepository organizationRepository,
        ICreateSecretCommand createSecretCommand,
        IUpdateSecretCommand updateSecretCommand,
        IDeleteSecretCommand deleteSecretCommand,
        IUserService userService,
        IEventService eventService,
        IReferenceEventService referenceEventService)
    {
        _currentContext = currentContext;
        _projectRepository = projectRepository;
        _secretRepository = secretRepository;
        _organizationRepository = organizationRepository;
        _createSecretCommand = createSecretCommand;
        _updateSecretCommand = updateSecretCommand;
        _deleteSecretCommand = deleteSecretCommand;
        _userService = userService;
        _eventService = eventService;
        _referenceEventService = referenceEventService;

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

        if (createRequest.ProjectIds != null && createRequest.ProjectIds.Length > 1)
        {
            throw new BadRequestException();
        }

        var userId = _userService.GetProperUserId(User).Value;
        var result = await _createSecretCommand.CreateAsync(createRequest.ToSecret(organizationId), userId);

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
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);

        var access = await _secretRepository.AccessToSecretAsync(id, userId, accessClient);

        if (!access.Read)
        {
            throw new NotFoundException();
        }

        if (_currentContext.ClientType == ClientType.ServiceAccount)
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
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);

        var secrets = await _secretRepository.GetManyByProjectIdAsync(projectId, userId, accessClient);

        return new SecretWithProjectsListResponseModel(secrets);
    }

    [HttpPut("secrets/{id}")]
    public async Task<SecretResponseModel> UpdateSecretAsync([FromRoute] Guid id, [FromBody] SecretUpdateRequestModel updateRequest)
    {
        if (updateRequest.ProjectIds != null && updateRequest.ProjectIds.Length > 1)
        {
            throw new BadRequestException();
        }

        var userId = _userService.GetProperUserId(User).Value;
        var secret = updateRequest.ToSecret(id);
        var result = await _updateSecretCommand.UpdateAsync(secret, userId);

        // Updating a secret means you have read & write permission.
        return new SecretResponseModel(result, true, true);
    }

    [HttpPost("secrets/delete")]
    public async Task<ListResponseModel<BulkDeleteResponseModel>> BulkDeleteAsync([FromBody] List<Guid> ids)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var results = await _deleteSecretCommand.DeleteSecrets(ids, userId);
        var responses = results.Select(r => new BulkDeleteResponseModel(r.Item1.Id, r.Item2));
        return new ListResponseModel<BulkDeleteResponseModel>(responses);
    }
}
