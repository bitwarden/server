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
        var userId = _userService.GetProperUserId(User).Value;
        var orgAdmin = await _currentContext.OrganizationAdmin(organizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);
        
        var secrets = await _secretRepository.GetManyByOrganizationIdAsync(organizationId, userId, accessClient);
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
        
        if(!await userHasReadAccessToProject(secret))
        {
            throw new UnauthorizedAccessException();
        }

        return new SecretResponseModel(secret);
    }

    [HttpGet("projects/{projectId}/{organizationId}/secrets")]
    public async Task<SecretWithProjectsListResponseModel> GetSecretsByProjectAsync([FromRoute] Guid projectId, [FromRoute] Guid organizationId)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var orgAdmin = await _currentContext.OrganizationAdmin(organizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);

        var secrets = await _secretRepository.GetManyByProjectIdAsync(projectId, userId, accessClient);
        
        if(secrets != null){
            if(!await userHasReadAccessToProject(projectId, organizationId))
            {
                throw new UnauthorizedAccessException("You don't have read access");
            }
        }

        var responses = secrets.Select(s => new SecretResponseModel(s));
        return new SecretWithProjectsListResponseModel(secrets);
    }

    [HttpPost("organizations/{organizationId}/secrets")]
    public async Task<SecretResponseModel> CreateSecretAsync([FromRoute] Guid organizationId, [FromBody] SecretCreateRequestModel createRequest)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var secret = createRequest.ToSecret(organizationId);
        if(!await userHasWriteAccessToProject(secret))
        {
            throw new UnauthorizedAccessException("You don't have read access");
        }

        var result = await _createSecretCommand.CreateAsync(createRequest.ToSecret(organizationId), userId);
        return new SecretResponseModel(result);
    }

    [HttpPut("secrets/{organizationId}/{id}")]
    public async Task<SecretResponseModel> UpdateSecretAsync([FromRoute] Guid organizationId, [FromRoute] Guid id, [FromBody] SecretUpdateRequestModel updateRequest)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var secret = updateRequest.ToSecret(id, organizationId);

        if(!await userHasWriteAccessToProject(secret))
        {
            throw new UnauthorizedAccessException("You don't have read access");
        }

        var result = await _updateSecretCommand.UpdateAsync(secret, userId);
        return new SecretResponseModel(result);
    }

    [HttpPost("secrets/{organizationId}/delete")]
    public async Task<ListResponseModel<BulkDeleteResponseModel>> BulkDeleteAsync([FromBody] List<Guid> ids, [FromRoute] Guid organizationId)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var results = await _deleteSecretCommand.DeleteSecrets(ids, userId, organizationId);
        var responses = results.Select(r => new BulkDeleteResponseModel(r.Item1.Id, r.Item2));
        return new ListResponseModel<BulkDeleteResponseModel>(responses);
    }

    public async Task<bool> userHasReadAccessToProject(Guid projectId, Guid organizationId)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var orgAdmin = await _currentContext.OrganizationAdmin(organizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);

        var hasAccess = accessClient switch
        {
            AccessClientType.NoAccessCheck => true,
            AccessClientType.User => await _projectRepository.UserHasReadAccessToProject(projectId, userId),
            AccessClientType.ServiceAccount => await _projectRepository.ServiceAccountHasReadAccessToProject(projectId, userId), 
            _ => false,
        };
        
        return hasAccess;
    }

    public async Task<bool> userHasReadAccessToProject(Secret secret)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var orgAdmin = await _currentContext.OrganizationAdmin(secret.OrganizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);
        var hasAccess = orgAdmin;

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

        return hasAccess;
    }

    public async Task<bool> userHasWriteAccessToProject(Secret secret)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var orgAdmin = await _currentContext.OrganizationAdmin(secret.OrganizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);
        var hasAccess = orgAdmin;

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
        } 

        return hasAccess;
    }
}
