﻿// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Api.SecretsManager.Models.Request;
using Bit.Api.SecretsManager.Models.Response;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.Porting.Interfaces;
using Bit.Core.SecretsManager.Queries.Projects.Interfaces;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.SecretsManager.Controllers;

[Authorize("secrets")]
public class SecretsManagerPortingController : Controller
{
    private readonly ISecretRepository _secretRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IUserService _userService;
    private readonly IMaxProjectsQuery _maxProjectsQuery;
    private readonly IImportCommand _importCommand;
    private readonly ICurrentContext _currentContext;

    public SecretsManagerPortingController(ISecretRepository secretRepository, IProjectRepository projectRepository,
        IUserService userService, IMaxProjectsQuery maxProjectsQuery, IImportCommand importCommand,
        ICurrentContext currentContext)
    {
        _secretRepository = secretRepository;
        _projectRepository = projectRepository;
        _userService = userService;
        _maxProjectsQuery = maxProjectsQuery;
        _importCommand = importCommand;
        _currentContext = currentContext;
    }

    [HttpGet("sm/{organizationId}/export")]
    public async Task<SMExportResponseModel> Export([FromRoute] Guid organizationId)
    {
        if (!await _currentContext.OrganizationAdmin(organizationId) || !_currentContext.AccessSecretsManager(organizationId))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User).Value;
        var projects = await _projectRepository.GetManyByOrganizationIdAsync(organizationId, userId, AccessClientType.NoAccessCheck);
        var secrets = await _secretRepository.GetManyDetailsByOrganizationIdAsync(organizationId, userId, AccessClientType.NoAccessCheck);

        if (projects == null && secrets == null)
        {
            throw new NotFoundException();
        }

        return new SMExportResponseModel(projects.Select(p => p.Project), secrets.Select(s => s.Secret));
    }

    [HttpPost("sm/{organizationId}/import")]
    public async Task Import([FromRoute] Guid organizationId, [FromBody] SMImportRequestModel importRequest)
    {
        if (!await _currentContext.OrganizationAdmin(organizationId) || !_currentContext.AccessSecretsManager(organizationId))
        {
            throw new NotFoundException();
        }

        if (importRequest.Projects?.Count() > 1000 || importRequest.Secrets?.Count() > 6000)
        {
            throw new BadRequestException("You cannot import this much data at once, the limit is 1000 projects and 6000 secrets.");
        }

        if (importRequest.Secrets.Any(s => s.ProjectIds.Count() > 1))
        {
            throw new BadRequestException("A secret can only be in one project at a time.");
        }

        var projectsToAdd = importRequest.Projects?.Count();
        if (projectsToAdd is > 0)
        {
            var (max, overMax) = await _maxProjectsQuery.GetByOrgIdAsync(organizationId, projectsToAdd.Value);
            if (overMax != null && overMax.Value)
            {
                throw new BadRequestException($"The maximum number of projects for this plan is ({max}).");
            }
        }

        await _importCommand.ImportAsync(organizationId, importRequest.ToSMImport());
    }
}
