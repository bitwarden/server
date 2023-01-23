using Bit.Api.Models.Response;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Api.SecretsManager.Models.Response;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.Secrets.Interfaces;
using Bit.Core.SecretsManager.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.SecretsManager.Controllers;

[SecretsManager]
[Authorize("secrets")]
public class SecretsController : Controller
{
    private readonly ISecretRepository _secretRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly ICreateSecretCommand _createSecretCommand;
    private readonly IUpdateSecretCommand _updateSecretCommand;
    private readonly IDeleteSecretCommand _deleteSecretCommand;

    public SecretsController(ISecretRepository secretRepository, IProjectRepository projectRepository, ICreateSecretCommand createSecretCommand, IUpdateSecretCommand updateSecretCommand, IDeleteSecretCommand deleteSecretCommand)
    {
        _secretRepository = secretRepository;
        _projectRepository = projectRepository;
        _createSecretCommand = createSecretCommand;
        _updateSecretCommand = updateSecretCommand;
        _deleteSecretCommand = deleteSecretCommand;
    }

    [HttpGet("organizations/{organizationId}/secrets")]
    public async Task<SecretWithProjectsListResponseModel> GetSecretsByOrganizationAsync([FromRoute] Guid organizationId)
    {
        var secrets = await _secretRepository.GetManyByOrganizationIdAsync(organizationId);
        return new SecretWithProjectsListResponseModel(secrets);
    }

    [HttpGet("secrets/{id}")]
    public async Task<SecretResponseModel> GetSecretAsync([FromRoute] Guid id)
    {
        var secret = await _secretRepository.GetByIdAsync(id);
        if (secret == null)
        {
            throw new NotFoundException();
        }
        return new SecretResponseModel(secret);
    }

    [HttpGet("projects/{projectId}/secrets")]
    public async Task<SecretWithProjectsListResponseModel> GetSecretsByProjectAsync([FromRoute] Guid projectId)
    {
        var secrets = await _secretRepository.GetManyByProjectIdAsync(projectId);
        var responses = secrets.Select(s => new SecretResponseModel(s));
        return new SecretWithProjectsListResponseModel(secrets);
    }

    [HttpPost("organizations/{organizationId}/secrets")]
    public async Task<SecretResponseModel> CreateSecretAsync([FromRoute] Guid organizationId, [FromBody] SecretCreateRequestModel createRequest)
    {
        var result = await _createSecretCommand.CreateAsync(createRequest.ToSecret(organizationId));
        return new SecretResponseModel(result);
    }

    [HttpPut("secrets/{id}")]
    public async Task<SecretResponseModel> UpdateSecretAsync([FromRoute] Guid id, [FromBody] SecretUpdateRequestModel updateRequest)
    {
        var result = await _updateSecretCommand.UpdateAsync(updateRequest.ToSecret(id));
        return new SecretResponseModel(result);
    }

    // TODO Once permissions are setup for Secrets Manager need to enforce them on delete.
    [HttpPost("secrets/delete")]
    public async Task<ListResponseModel<BulkDeleteResponseModel>> BulkDeleteAsync([FromBody] List<Guid> ids)
    {
        var results = await _deleteSecretCommand.DeleteSecrets(ids);
        var responses = results.Select(r => new BulkDeleteResponseModel(r.Item1.Id, r.Item2));
        return new ListResponseModel<BulkDeleteResponseModel>(responses);
    }
}
