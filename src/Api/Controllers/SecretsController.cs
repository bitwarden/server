using Bit.Api.Models.Response;
using Bit.Api.SecretManagerFeatures.Models.Request;
using Bit.Api.SecretManagerFeatures.Models.Response;
using Bit.Api.Utilities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Entities;
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
        var secrets = await _secretRepository.GetManyByOrganizationIdAsync(organizationId);
        
        foreach(Secret secret in secrets)
        {
           userHasReadAccessToProject(secret);
        }
 
        return new SecretWithProjectsListResponseModel(secrets);
    }

    [HttpGet("secrets/{id}")]
    public async Task<SecretResponseModel> GetSecretAsync([FromRoute] Guid id)
    {
        var secret = await _secretRepository.GetByIdAsync(id);
        if(userHasReadAccessToProject(secret)){
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
        var secrets = await _secretRepository.GetManyByProjectIdAsync(projectId);
        var firstSecret = secrets.FirstOrDefault();
        if(userHasReadAccessToProject(projectId, firstSecret.OrganizationId))
        {
            var responses = secrets.Select(s => new SecretResponseModel(s));
            return new SecretWithProjectsListResponseModel(secrets);
        }
    }

    [HttpPost("organizations/{organizationId}/secrets")]
    public async Task<SecretResponseModel> CreateSecretAsync([FromRoute] Guid organizationId, [FromBody] SecretCreateRequestModel createRequest)
    {
        var secret = createRequest.ToSecret(organizationId);
        if(userHasWriteAccessToProject(secret)){
            var result = await _createSecretCommand.CreateAsync(secret);
            return new SecretResponseModel(result);
        }
    }

    [HttpPut("secrets/{id}")]
    public async Task<SecretResponseModel> UpdateSecretAsync([FromRoute] Guid id, [FromBody] SecretUpdateRequestModel updateRequest)
    {
        var secret = updateRequest.ToSecret(id);
        if(userHasWriteAccessToProject(secret))
        {
            var result = await _updateSecretCommand.UpdateAsync(secret);
            return new SecretResponseModel(result);
        }
    }

    [HttpPost("secrets/delete")]
    public async Task<ListResponseModel<BulkDeleteResponseModel>> BulkDeleteAsync([FromBody] List<Guid> ids)
    {
        var userId = _userService.GetProperUserId(User).Value;
        //TODO get orgId
        //DeleteSecretCommand checks access
        Guid organizationId = new Guid();
        var results = await _deleteSecretCommand.DeleteSecrets(ids, userId, organizationId);
        var responses = results.Select(r => new BulkDeleteResponseModel(r.Item1.Id, r.Item2));
        return new ListResponseModel<BulkDeleteResponseModel>(responses);
    }

    public bool userHasReadAccessToProject(Guid projectId, Guid organizationId)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var orgAdmin = await _currentContext.OrganizationAdmin(organizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);
        var accessType = AccessType.Read;

        if(projectId)
        {
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
            throw new UnauthorizedAccessException("You don't have read access");
        }

        return true;
    }

    public bool userHasReadAccessToProject(Secret secret)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var orgAdmin = await _currentContext.OrganizationAdmin(secret.organizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);
        var accessType = AccessType.Read;

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
            throw new UnauthorizedAccessException("You don't have read access");
        }

        return true;
    }

    public bool userHasWriteAccessToProject(Secret secret)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var orgAdmin = await _currentContext.OrganizationAdmin(secret.organizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);
        var accessType = AccessType.Write;

        if(secret.Projects != null)
        {
            var projectId = secret.Projects.FirstOrDefault().Id;
            var hasAccess = accessClient switch
            {
                AccessClientType.NoAccessCheck => true,
                AccessClientType.User => _projectRepository.UserHasWriteAccessToProject(projectId, userId),
                AccessClientType.ServiceAccount => _projectRepository.ServiceAccountHasWriteAccessToProject(projectId, userId), 
                _ => throw new ArgumentOutOfRangeException(nameof(accessType), accessType, null),
            };
        } else 
        {
            hasAccess = orgAdmin;
        }

        if(!hasAccess)
        {
            throw new UnauthorizedAccessException("You don't have write access");
        }

        return true;
    }
}
