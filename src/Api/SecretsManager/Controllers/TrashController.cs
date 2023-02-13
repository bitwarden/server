using Bit.Api.SecretsManager.Models.Response;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.Secrets.Interfaces;
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
    private readonly ICreateSecretCommand _createSecretCommand;
    private readonly IUpdateSecretCommand _updateSecretCommand;
    private readonly IDeleteSecretCommand _deleteSecretCommand;

    public TrashController(
        ICurrentContext currentContext,
        ISecretRepository secretRepository,
        ICreateSecretCommand createSecretCommand,
        IUpdateSecretCommand updateSecretCommand,
        IDeleteSecretCommand deleteSecretCommand)
    {
        _currentContext = currentContext;
        _secretRepository = secretRepository;
        _createSecretCommand = createSecretCommand;
        _updateSecretCommand = updateSecretCommand;
        _deleteSecretCommand = deleteSecretCommand;
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
}
