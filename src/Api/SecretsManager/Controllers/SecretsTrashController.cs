using Bit.Api.Models.Response;
using Bit.Api.SecretsManager.Models.Response;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.Secrets.Interfaces;
using Bit.Core.SecretsManager.Commands.Trash.Interfaces;
using Bit.Core.SecretsManager.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.SecretsManager.Controllers;

[SecretsManager]
[Authorize("secrets")]
public class TrashController : Controller
{
    private readonly ICurrentContext _currentContext;
    private readonly ISecretRepository _secretRepository;
    private readonly IEmptyTrashCommand _emptyTrashCommand;
    private readonly IRestoreTrashCommand _restoreTrashCommand;

    public TrashController(
        ICurrentContext currentContext,
        ISecretRepository secretRepository,
        IEmptyTrashCommand emptyTrashCommand,
        IRestoreTrashCommand restoreTrashCommand)
    {
        _currentContext = currentContext;
        _secretRepository = secretRepository;
        _emptyTrashCommand = emptyTrashCommand;
        _restoreTrashCommand = restoreTrashCommand;
    }

    [HttpGet("secrets/{organizationId}/trash")]
    public async Task<SecretWithProjectsListResponseModel> ListByOrganizationAsync([FromRoute] Guid organizationId)
    {
        if (!_currentContext.AccessSecretsManager(organizationId))
        {
            throw new NotFoundException();
        }
        if (!await _currentContext.OrganizationAdmin(organizationId))
        {
            throw new UnauthorizedAccessException();
        }

        var secrets = await _secretRepository.GetManyByOrganizationIdInTrashAsync(organizationId);
        return new SecretWithProjectsListResponseModel(secrets);
    }

    [HttpPost("secrets/{organizationId}/trash/empty")]
    public async Task<ListResponseModel<BulkDeleteResponseModel>> EmptyTrash([FromRoute] Guid organizationId, [FromBody] List<Guid> ids)
    {
        if (!_currentContext.AccessSecretsManager(organizationId))
        {
            throw new NotFoundException();
        }
        if (!await _currentContext.OrganizationAdmin(organizationId))
        {
            throw new UnauthorizedAccessException();
        }

        var results = await _emptyTrashCommand.EmptyTrash(organizationId, ids);
        var responses = results.Select(r => new BulkDeleteResponseModel(r.Item1.Id, r.Item2));
        return new ListResponseModel<BulkDeleteResponseModel>(responses);
    }

    [HttpPost("secrets/{organizationId}/trash/restore")]
    public async Task<ListResponseModel<BulkRestoreResponseModel>> RestoreTrash([FromRoute] Guid organizationId, [FromBody] List<Guid> ids)
    {
        if (!_currentContext.AccessSecretsManager(organizationId))
        {
            throw new NotFoundException();
        }
        if (!await _currentContext.OrganizationAdmin(organizationId))
        {
            throw new UnauthorizedAccessException();
        }

        var results = await _restoreTrashCommand.RestoreTrash(organizationId, ids);
        var responses = results.Select(r => new BulkRestoreResponseModel(r.Item1.Id, r.Item2));
        return new ListResponseModel<BulkRestoreResponseModel>(responses);
    }
}
