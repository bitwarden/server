using Bit.Api.SecretsManager.Models.Response;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.SecretsManager.Controllers;

[Authorize("secrets")]
[Route("organizations/{organizationId}")]
public class CountsController : Controller
{
    private readonly ICurrentContext _currentContext;
    private readonly IUserService _userService;
    private readonly IProjectRepository _projectRepository;
    private readonly ISecretRepository _secretRepository;
    private readonly IServiceAccountRepository _serviceAccountRepository;

    public CountsController(
        ICurrentContext currentContext,
        IUserService userService,
        IProjectRepository projectRepository,
        ISecretRepository secretRepository,
        IServiceAccountRepository serviceAccountRepository)
    {
        _currentContext = currentContext;
        _userService = userService;
        _projectRepository = projectRepository;
        _secretRepository = secretRepository;
        _serviceAccountRepository = serviceAccountRepository;
    }

    [HttpGet("counts")]
    public async Task<OrganizationCountsResponseModel> GetByOrganizationAsync([FromRoute] Guid organizationId)
    {
        if (!_currentContext.AccessSecretsManager(organizationId))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User).Value;
        var orgAdmin = await _currentContext.OrganizationAdmin(organizationId);
        var accessType = AccessClientHelper.ToAccessClient(_currentContext.ClientType, orgAdmin);

        var projectsCountTask = _projectRepository.GetProjectCountByOrganizationIdAsync(organizationId,
            userId, accessType);

        var secretsCountTask = _secretRepository.GetSecretsCountByOrganizationIdAsync(organizationId,
            userId, accessType);

        var machineAccountsCountsTask = _serviceAccountRepository.GetServiceAccountCountByOrganizationIdAsync(
            organizationId, userId, accessType);

        return new OrganizationCountsResponseModel
        {
            Projects = await projectsCountTask,
            Secrets = await secretsCountTask,
            MachineAccounts = await machineAccountsCountsTask
        };
    }
}
