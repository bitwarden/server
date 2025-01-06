﻿using Bit.Api.Models.Response;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Api.SecretsManager.Models.Response;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.AuthorizationRequirements;
using Bit.Core.SecretsManager.Commands.Projects.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Queries.Projects.Interfaces;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.SecretsManager.Controllers;

[Authorize("secrets")]
public class ProjectsController : Controller
{
    private readonly ICurrentContext _currentContext;
    private readonly IUserService _userService;
    private readonly IProjectRepository _projectRepository;
    private readonly IMaxProjectsQuery _maxProjectsQuery;
    private readonly ICreateProjectCommand _createProjectCommand;
    private readonly IUpdateProjectCommand _updateProjectCommand;
    private readonly IDeleteProjectCommand _deleteProjectCommand;
    private readonly IAuthorizationService _authorizationService;

    public ProjectsController(
        ICurrentContext currentContext,
        IUserService userService,
        IProjectRepository projectRepository,
        IMaxProjectsQuery maxProjectsQuery,
        ICreateProjectCommand createProjectCommand,
        IUpdateProjectCommand updateProjectCommand,
        IDeleteProjectCommand deleteProjectCommand,
        IAuthorizationService authorizationService)
    {
        _currentContext = currentContext;
        _userService = userService;
        _projectRepository = projectRepository;
        _maxProjectsQuery = maxProjectsQuery;
        _createProjectCommand = createProjectCommand;
        _updateProjectCommand = updateProjectCommand;
        _deleteProjectCommand = deleteProjectCommand;
        _authorizationService = authorizationService;
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
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.IdentityClientType, orgAdmin);

        var projects = await _projectRepository.GetManyByOrganizationIdAsync(organizationId, userId, accessClient);

        var responses = projects.Select(project => new ProjectResponseModel(project));
        return new ListResponseModel<ProjectResponseModel>(responses);
    }

    [HttpPost("organizations/{organizationId}/projects")]
    public async Task<ProjectResponseModel> CreateAsync([FromRoute] Guid organizationId,
        [FromBody] ProjectCreateRequestModel createRequest)
    {
        var project = createRequest.ToProject(organizationId);
        var authorizationResult =
            await _authorizationService.AuthorizeAsync(User, project, ProjectOperations.Create);
        if (!authorizationResult.Succeeded)
        {
            throw new NotFoundException();
        }

        var (max, overMax) = await _maxProjectsQuery.GetByOrgIdAsync(organizationId, 1);
        if (overMax != null && overMax.Value)
        {
            throw new BadRequestException($"You have reached the maximum number of projects ({max}) for this plan.");
        }

        var userId = _userService.GetProperUserId(User).Value;
        var result = await _createProjectCommand.CreateAsync(project, userId, _currentContext.IdentityClientType);

        // Creating a project means you have read & write permission.
        return new ProjectResponseModel(result, true, true);
    }

    [HttpPut("projects/{id}")]
    public async Task<ProjectResponseModel> UpdateAsync([FromRoute] Guid id,
        [FromBody] ProjectUpdateRequestModel updateRequest)
    {
        var project = await _projectRepository.GetByIdAsync(id);
        var authorizationResult =
            await _authorizationService.AuthorizeAsync(User, project, ProjectOperations.Update);
        if (!authorizationResult.Succeeded)
        {
            throw new NotFoundException();
        }

        var result = await _updateProjectCommand.UpdateAsync(updateRequest.ToProject(id));

        // Updating a project means you have read & write permission.
        return new ProjectResponseModel(result, true, true);
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
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.IdentityClientType, orgAdmin);

        var access = await _projectRepository.AccessToProjectAsync(id, userId, accessClient);

        if (!access.Read)
        {
            throw new NotFoundException();
        }

        return new ProjectResponseModel(project, access.Read, access.Write);
    }

    [HttpPost("projects/delete")]
    public async Task<ListResponseModel<BulkDeleteResponseModel>> BulkDeleteAsync(
        [FromBody] List<Guid> ids)
    {
        var projects = (await _projectRepository.GetManyWithSecretsByIds(ids)).ToList();
        if (!projects.Any() || projects.Count != ids.Count)
        {
            throw new NotFoundException();
        }

        // Ensure all projects belongs to the same organization
        var organizationId = projects.First().OrganizationId;
        if (projects.Any(p => p.OrganizationId != organizationId) ||
            !_currentContext.AccessSecretsManager(organizationId))
        {
            throw new NotFoundException();
        }

        var projectsToDelete = new List<Project>();
        var results = new List<(Project Project, string Error)>();

        foreach (var project in projects)
        {
            var authorizationResult =
                await _authorizationService.AuthorizeAsync(User, project, ProjectOperations.Delete);
            if (authorizationResult.Succeeded)
            {
                projectsToDelete.Add(project);
                results.Add((project, ""));
            }
            else
            {
                results.Add((project, "access denied"));
            }
        }

        await _deleteProjectCommand.DeleteProjects(projectsToDelete);

        var responses = results.Select(r => new BulkDeleteResponseModel(r.Project.Id, r.Error));
        return new ListResponseModel<BulkDeleteResponseModel>(responses);
    }
}
