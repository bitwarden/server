using Bit.Api.Models.Response;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Api.SecretsManager.Models.Response;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.Projects.Interfaces;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.SecretsManager.Controllers;

[SecretsManager]
[Authorize("secrets")]
public class ProjectsController : Controller
{
    private readonly ICurrentContext _currentContext;
    private readonly IUserService _userService;
    private readonly IProjectRepository _projectRepository;
    private readonly ICreateProjectCommand _createProjectCommand;
    private readonly IUpdateProjectCommand _updateProjectCommand;
    private readonly IDeleteProjectCommand _deleteProjectCommand;

    public ProjectsController(
        ICurrentContext currentContext,
        IUserService userService,
        IProjectRepository projectRepository,
        ICreateProjectCommand createProjectCommand,
        IUpdateProjectCommand updateProjectCommand,
        IDeleteProjectCommand deleteProjectCommand)
    {
        _currentContext = currentContext;
        _userService = userService;
        _projectRepository = projectRepository;
        _createProjectCommand = createProjectCommand;
        _updateProjectCommand = updateProjectCommand;
        _deleteProjectCommand = deleteProjectCommand;
    }

    [HttpGet("organizations/{organizationId}/projects")]
    public async Task<ListResponseModel<ProjectResponseModel>> ListByOrganizationAsync([FromRoute] Guid organizationId)
    {
        if (!_currentContext.AccessSecretsManager(organizationId))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User).Value;
        var orgAdmin = await _currentContext.OrganizationAdmin(organizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);

        var projects = await _projectRepository.GetManyByOrganizationIdAsync(organizationId, userId, accessClient);

        var responses = projects.Select(project => new ProjectResponseModel(project));
        return new ListResponseModel<ProjectResponseModel>(responses);
    }

    [HttpPost("organizations/{organizationId}/projects")]
    public async Task<ProjectResponseModel> CreateAsync([FromRoute] Guid organizationId, [FromBody] ProjectCreateRequestModel createRequest)
    {
        if (!_currentContext.AccessSecretsManager(organizationId))
        {
            throw new NotFoundException();
        }

        var result = await _createProjectCommand.CreateAsync(createRequest.ToProject(organizationId));
        return new ProjectResponseModel(result);
    }

    [HttpPut("projects/{id}")]
    public async Task<ProjectResponseModel> UpdateAsync([FromRoute] Guid id, [FromBody] ProjectUpdateRequestModel updateRequest)
    {
        var userId = _userService.GetProperUserId(User).Value;

        var result = await _updateProjectCommand.UpdateAsync(updateRequest.ToProject(id), userId);
        return new ProjectResponseModel(result);
    }

    [HttpGet("projects/{id}")]
    public async Task<ProjectResponseModel> GetAsync([FromRoute] Guid id)
    {
        var project = await _projectRepository.GetByIdAsync(id);
        if (project == null)
        {
            throw new NotFoundException();
        }

        if (!_currentContext.AccessSecretsManager(project.OrganizationId))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User).Value;
        var orgAdmin = await _currentContext.OrganizationAdmin(project.OrganizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);

        var hasAccess = accessClient switch
        {
            AccessClientType.NoAccessCheck => true,
            AccessClientType.User => await _projectRepository.UserHasReadAccessToProject(id, userId),
            _ => false,
        };

        if (!hasAccess)
        {
            throw new NotFoundException();
        }

        return new ProjectResponseModel(project);
    }

    [HttpPost("projects/delete")]
    public async Task<ListResponseModel<BulkDeleteResponseModel>> BulkDeleteAsync([FromBody] List<Guid> ids)
    {
        var userId = _userService.GetProperUserId(User).Value;

        var results = await _deleteProjectCommand.DeleteProjects(ids, userId);
        var responses = results.Select(r => new BulkDeleteResponseModel(r.Item1.Id, r.Item2));
        return new ListResponseModel<BulkDeleteResponseModel>(responses);
    }
}
