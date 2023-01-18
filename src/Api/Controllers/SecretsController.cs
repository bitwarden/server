using Bit.Api.Models.Response;
using Bit.Api.SecretManagerFeatures.Models.Request;
using Bit.Api.SecretManagerFeatures.Models.Response;
using Bit.Api.Utilities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.SecretManagerFeatures.Secrets.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers;

[SecretsManager]
[Authorize("secrets")]
public class SecretsController : Controller
{
    private readonly ISecretRepository _secretRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly ICreateSecretCommand _createSecretCommand;
    private readonly IUpdateSecretCommand _updateSecretCommand;
    private readonly IDeleteSecretCommand _deleteSecretCommand;
    private readonly IUserRepository _userRepository;

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
        var userId = _userService.GetProperUserId(User).Value;
        var orgAdmin = await _currentContext.OrganizationAdmin(organizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);

        var secrets = await _secretRepository.GetManyByOrganizationIdAsync(organizationId);
        
        foreach(Secret secret in secrets)
        {
           hasAccessToProject(secret);
        }
 
        return new SecretWithProjectsListResponseModel(secrets);
    }

    [HttpGet("secrets/{id}")]
    public async Task<SecretResponseModel> GetSecretAsync([FromRoute] Guid id)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var orgAdmin = await _currentContext.OrganizationAdmin(organizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);

        var secret = await _secretRepository.GetByIdAsync(id);
        if(hasAccessToProject(secret)){
            if (secret == null)
            {
                throw new NotFoundException();
            }
            return new SecretResponseModel(secret);
        }
    }

    [HttpGet("projects/{projectId}/secrets")]
    public async Task<SecretWithProjectsListResponseModel> GetSecretsByProjectAsync([FromRoute] Guid projectId)
    {
     
        if(hasAccessToProject(projectId)){

        var secrets = await _secretRepository.GetManyByProjectIdAsync(projectId);
        var responses = secrets.Select(s => new SecretResponseModel(s));
        return new SecretWithProjectsListResponseModel(secrets);
    }

    [HttpPost("organizations/{organizationId}/secrets")]
    public async Task<SecretResponseModel> CreateSecretAsync([FromRoute] Guid organizationId, [FromBody] SecretCreateRequestModel createRequest)
    {
        var secret = createRequest.ToSecret(organizationId);
        if(hasAccessToProject(secret)){
            var result = await _createSecretCommand.CreateAsync(secret);
            return new SecretResponseModel(result);
        }
    }

    [HttpPut("secrets/{id}")]
    public async Task<SecretResponseModel> UpdateSecretAsync([FromRoute] Guid id, [FromBody] SecretUpdateRequestModel updateRequest)
    {
        var secret = updateRequest.ToSecret(id);
        if(hasAccessToProject(secret))
        {
            var result = await _updateSecretCommand.UpdateAsync(secret);
            return new SecretResponseModel(result);
        }
    }

    // TODO Once permissions are setup for Secrets Manager need to enforce them on delete.
    [HttpPost("secrets/delete")]
    public async Task<ListResponseModel<BulkDeleteResponseModel>> BulkDeleteAsync([FromBody] List<Guid> ids)
    {
        //hasAccessToProject(secret);
        var results = await _deleteSecretCommand.DeleteSecrets(ids);
        var responses = results.Select(r => new BulkDeleteResponseModel(r.Item1.Id, r.Item2));
        return new ListResponseModel<BulkDeleteResponseModel>(responses);
    }

    public bool hasAccessToProject(Secret secret)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var orgAdmin = await _currentContext.OrganizationAdmin(organizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);

        if(secret.Projects != null)
        {
            var projectId = secret.Projects.FirstOrDefault().Id;
            var hasAccess = accessClient switch
            {
                AccessClientType.NoAccessCheck => true,
                AccessClientType.User => _projectRepository.UserHasReadAccessToProject(projectId, userId),
                AccessClientType.ServiceAccount => _projectRepository.ServiceAccountHasReadAccessToProject(projectId, userId), 
                _ => throw new ArgumentOutOfRangeException(nameof(accessType), accessType, null),
            };
        } else 
        {
            hasAccess = orgAdmin;
        }

        if(!hasAccess)
        {
            throw new UnauthorizedAccessException("You don't have access");
        }

        return true;
    }
}
