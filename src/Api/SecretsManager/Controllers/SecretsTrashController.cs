using Bit.Api.SecretsManager.Models.Response;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Identity;
using Bit.Core.SecretsManager.Commands.Trash.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.SecretsManager.Controllers;

[Authorize("secrets")]
public class TrashController : Controller
{
    private readonly ICurrentContext _currentContext;
    private readonly ISecretRepository _secretRepository;
    private readonly IEmptyTrashCommand _emptyTrashCommand;
    private readonly IRestoreTrashCommand _restoreTrashCommand;
    private readonly IUserService _userService;
    private readonly IEventService _eventService;

    public TrashController(
        ICurrentContext currentContext,
        ISecretRepository secretRepository,
        IEmptyTrashCommand emptyTrashCommand,
        IRestoreTrashCommand restoreTrashCommand,
        IUserService userService,
        IEventService eventService)
    {
        _currentContext = currentContext;
        _secretRepository = secretRepository;
        _emptyTrashCommand = emptyTrashCommand;
        _restoreTrashCommand = restoreTrashCommand;
        _userService = userService;
        _eventService = eventService;
    }

    [HttpGet("secrets/{organizationId}/trash")]
    public async Task<SecretWithProjectsListResponseModel> ListByOrganizationAsync(Guid organizationId)
    {
        if (!_currentContext.AccessSecretsManager(organizationId))
        {
            throw new NotFoundException();
        }

        if (!await _currentContext.OrganizationAdmin(organizationId))
        {
            throw new UnauthorizedAccessException();
        }

        var secrets = await _secretRepository.GetManyDetailsByOrganizationIdInTrashAsync(organizationId);
        return new SecretWithProjectsListResponseModel(secrets);
    }

    [HttpPost("secrets/{organizationId}/trash/empty")]
    public async Task EmptyTrashAsync(Guid organizationId, [FromBody] List<Guid> ids)
    {
        if (!_currentContext.AccessSecretsManager(organizationId))
        {
            throw new NotFoundException();
        }

        if (!await _currentContext.OrganizationAdmin(organizationId))
        {
            throw new UnauthorizedAccessException();
        }

        var deletedSecrets = await _secretRepository.GetManyTrashedSecretsByIds(ids);
        await _emptyTrashCommand.EmptyTrash(organizationId, ids);
        await LogSecretsTrashEventAsync(deletedSecrets, EventType.Secret_Permanently_Deleted);
    }

    [HttpPost("secrets/{organizationId}/trash/restore")]
    public async Task RestoreTrashAsync(Guid organizationId, [FromBody] List<Guid> ids)
    {
        if (!_currentContext.AccessSecretsManager(organizationId))
        {
            throw new NotFoundException();
        }

        if (!await _currentContext.OrganizationAdmin(organizationId))
        {
            throw new UnauthorizedAccessException();
        }

        await _restoreTrashCommand.RestoreTrash(organizationId, ids);
        await LogSecretsTrashEventAsync(ids, EventType.Secret_Restored);
    }

    private async Task LogSecretsTrashEventAsync(IEnumerable<Guid> secretIds, EventType eventType)
    {
        var secrets = await _secretRepository.GetManyByIds(secretIds);
        await LogSecretsTrashEventAsync(secrets, eventType);
    }

    private async Task LogSecretsTrashEventAsync(IEnumerable<Secret> secrets, EventType eventType)
    {
        var userId = _userService.GetProperUserId(User)!.Value;

        switch (_currentContext.IdentityClientType)
        {
            case IdentityClientType.ServiceAccount:
                await _eventService.LogServiceAccountSecretsEventAsync(userId, secrets, eventType);
                break;
            case IdentityClientType.User:
                await _eventService.LogUserSecretsEventAsync(userId, secrets, eventType);
                break;
        }
    }
}
