using Bit.Api.Models.Response;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Api.SecretsManager.Models.Response;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Commands.Secrets.Interfaces;
using Bit.Core.SecretsManager.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Bit.Core.Services;
using Bit.Core.Context;
using Bit.Core.Enums;

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
    private readonly IUserService _userService;
    private readonly ICurrentContext _currentContext;

    public SecretsController(ISecretRepository secretRepository, IProjectRepository projectRepository, ICreateSecretCommand createSecretCommand, IUpdateSecretCommand updateSecretCommand, IDeleteSecretCommand deleteSecretCommand, IUserService userService, ICurrentContext currentContext)
    {
        _secretRepository = secretRepository;
        _projectRepository = projectRepository;
        _createSecretCommand = createSecretCommand;
        _updateSecretCommand = updateSecretCommand;
        _deleteSecretCommand = deleteSecretCommand;
        _userService = userService;
        _currentContext = currentContext;
    }

    [HttpGet("organizations/{organizationId}/secrets")]
    public async Task<SecretWithProjectsListResponseModel> GetSecretsByOrganizationAsync([FromRoute] Guid organizationId)
    {
        var secrets = await _secretRepository.GetManyByOrganizationIdAsync(organizationId);
        var secretsAllowedToRead = new List<Secret>();

        foreach(Secret secret in secrets)
        {
           if(await userHasReadAccessToProject(secret))
           {
               secretsAllowedToRead.Add(secret);
           }
        }

        //Dont show an error message just go ahead and show only allowed to read secrets 
        return new SecretWithProjectsListResponseModel(secretsAllowedToRead);
    }

    [HttpGet("secrets/{id}")]
    public async Task<SecretResponseModel> GetSecretAsync([FromRoute] Guid id)
    {
        var secret = await _secretRepository.GetByIdAsync(id);
        if(await userHasReadAccessToProject(secret))
        {
            if (secret == null)
            {
                throw new NotFoundException();
            }
            return new SecretResponseModel(secret);
        } 
        else 
        {
            throw new UnauthorizedAccessException("You don't have read access");
        }

        return null;
    }

    [HttpGet("projects/{projectId}/secrets")]
    public async Task<SecretWithProjectsListResponseModel> GetSecretsByProjectAsync([FromRoute] Guid projectId)
    {
        var secrets = await _secretRepository.GetManyByProjectIdAsync(projectId);
        
        if(secrets != null){
            var firstSecret = secrets.FirstOrDefault();
            if(await userHasReadAccessToProject(projectId, firstSecret.OrganizationId))
            {
                //TODO what is going on here?
                var responses = secrets.Select(s => new SecretResponseModel(s));
                return new SecretWithProjectsListResponseModel(secrets);
            } 
            else
            {
                throw new UnauthorizedAccessException("You don't have read access");
            }
        }

        return null;
    }

    [HttpPost("organizations/{organizationId}/secrets")]
    public async Task<SecretResponseModel> CreateSecretAsync([FromRoute] Guid organizationId, [FromBody] SecretCreateRequestModel createRequest)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var secret = createRequest.ToSecret(organizationId);
        if(await userHasWriteAccessToProject(secret))
        {
            var result = await _createSecretCommand.CreateAsync(createRequest.ToSecret(organizationId), userId);
            return new SecretResponseModel(result);
        }
        else
        {
            throw new UnauthorizedAccessException("You don't have read access");
        }

        return null;
    }

    [HttpPut("secrets/{id}")]
    public async Task<SecretResponseModel> UpdateSecretAsync([FromRoute] Guid id, [FromBody] SecretUpdateRequestModel updateRequest)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var secret = updateRequest.ToSecret(id);
        if(await userHasWriteAccessToProject(secret))
        {
            var result = await _updateSecretCommand.UpdateAsync(updateRequest.ToSecret(id), userId);
            return new SecretResponseModel(result);
        } else 
        {
            throw new UnauthorizedAccessException("You don't have read access");
        }

        return null;
    }

    [HttpPost("secrets/delete")]
    public async Task<ListResponseModel<BulkDeleteResponseModel>> BulkDeleteAsync([FromBody] List<Guid> ids)
    {
        var userId = _userService.GetProperUserId(User).Value;
        //TODO get orgId
        Guid organizationId = new Guid();
        var results = await _deleteSecretCommand.DeleteSecrets(ids, userId, organizationId);
        var responses = results.Select(r => new BulkDeleteResponseModel(r.Item1.Id, r.Item2));
        return new ListResponseModel<BulkDeleteResponseModel>(responses);
    }

    public async Task<bool> userHasReadAccessToProject(Guid projectId, Guid organizationId)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var orgAdmin = await _currentContext.OrganizationAdmin(organizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);
        var hasAccess = false;

        if(projectId != Guid.Empty)
        {
            hasAccess = accessClient switch
            {
                AccessClientType.NoAccessCheck => true,
                AccessClientType.User => await _projectRepository.UserHasReadAccessToProject(projectId, userId),
                AccessClientType.ServiceAccount => await _projectRepository.ServiceAccountHasReadAccessToProject(projectId, userId), 
                _ => false,
            };
        } 
        else 
        {
            hasAccess = orgAdmin;
        }

        return hasAccess;
    }

    public async Task<bool> userHasReadAccessToProject(Secret secret)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var orgAdmin = await _currentContext.OrganizationAdmin(secret.OrganizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);
        var hasAccess = false;

        if(secret.Projects?.Count > 0)
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
        else 
        {
            hasAccess = orgAdmin;
        }

        return hasAccess;
    }

    public async Task<bool> userHasWriteAccessToProject(Secret secret)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var orgAdmin = await _currentContext.OrganizationAdmin(secret.OrganizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);
        var hasAccess = false;

        if(secret.Projects?.Count > 0)
        {
            var projectId = secret.Projects.FirstOrDefault().Id;
            hasAccess = accessClient switch
            {
                AccessClientType.NoAccessCheck => true,
                AccessClientType.User => await _projectRepository.UserHasWriteAccessToProject(projectId, userId),
                AccessClientType.ServiceAccount => await _projectRepository.ServiceAccountHasWriteAccessToProject(projectId, userId), 
                _ => false,
            };
        } else 
        {
            hasAccess = orgAdmin;
        }

        return hasAccess;
    }
}
