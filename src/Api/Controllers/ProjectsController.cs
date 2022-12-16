using Bit.Api.Models.Response;
using Bit.Api.SecretManagerFeatures.Models.Request;
using Bit.Api.SecretManagerFeatures.Models.Response;
using Bit.Api.Utilities;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.SecretManagerFeatures.Projects.Interfaces;
using Bit.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers;

[SecretsManager]
public class ProjectsController : Controller
{
    private readonly IUserService _userService;
    private readonly IProjectRepository _projectRepository;
    private readonly ICreateProjectCommand _createProjectCommand;
    private readonly IUpdateProjectCommand _updateProjectCommand;
    private readonly IDeleteProjectCommand _deleteProjectCommand;
    private readonly ICurrentContext _currentContext;

    public ProjectsController(
        IUserService userService,
        IProjectRepository projectRepository,
        ICreateProjectCommand createProjectCommand,
        IUpdateProjectCommand updateProjectCommand,
        IDeleteProjectCommand deleteProjectCommand,
        ICurrentContext currentContext)
    {
        _userService = userService;
        _projectRepository = projectRepository;
        _createProjectCommand = createProjectCommand;
        _updateProjectCommand = updateProjectCommand;
        _deleteProjectCommand = deleteProjectCommand;
        _currentContext = currentContext;
    }

    [HttpPost("organizations/{organizationId}/projects")]
    public async Task<ProjectResponseModel> CreateAsync([FromRoute] Guid organizationId, [FromBody] ProjectCreateRequestModel createRequest)
    {
        var result = await _createProjectCommand.CreateAsync(createRequest.ToProject(organizationId));
        return new ProjectResponseModel(result);
    }

    [HttpPut("projects/{id}")]
    public async Task<ProjectResponseModel> UpdateProjectAsync([FromRoute] Guid id, [FromBody] ProjectUpdateRequestModel updateRequest)
    {
        var result = await _updateProjectCommand.UpdateAsync(updateRequest.ToProject(id));
        return new ProjectResponseModel(result);
    }

    [HttpGet("organizations/{organizationId}/projects")]
    public async Task<ListResponseModel<ProjectResponseModel>> GetProjectsByOrganizationAsync(
        [FromRoute] Guid organizationId)
    {
        var userId = _userService.GetProperUserId(User).Value;

        IEnumerable<Project> projects;
        if (await _currentContext.OrganizationAdmin(organizationId))
        {
            projects = await _projectRepository.GetManyByOrganizationIdAsync(organizationId, null, null);
        }
        else if (_currentContext.ServiceAccount())
        {
            projects = await _projectRepository.GetManyByOrganizationIdAsync(organizationId, null, userId);
        }
        else
        {
            projects = await _projectRepository.GetManyByOrganizationIdAsync(organizationId, userId, null);
        }

        var responses = projects.Select(project => new ProjectResponseModel(project));
        return new ListResponseModel<ProjectResponseModel>(responses);
    }

    [HttpGet("projects/{id}")]
    public async Task<ProjectResponseModel> GetProjectAsync([FromRoute] Guid id)
    {
        var project = await _projectRepository.GetByIdAsync(id);
        if (project == null)
        {
            throw new NotFoundException();
        }
        return new ProjectResponseModel(project);
    }

    [HttpPost("projects/delete")]
    public async Task<ListResponseModel<BulkDeleteResponseModel>> BulkDeleteProjectsAsync([FromBody] List<Guid> ids)
    {
        var results = await _deleteProjectCommand.DeleteProjects(ids);
        var responses = results.Select(r => new BulkDeleteResponseModel(r.Item1.Id, r.Item2));
        return new ListResponseModel<BulkDeleteResponseModel>(responses);
    }
}
