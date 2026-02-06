#nullable enable
using Bit.Api.SecretsManager.Models.Response;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Queries.Interfaces;
using Bit.Core.SecretsManager.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.SecretsManager.Controllers;

[Authorize("secrets")]
public class CountsController : Controller
{
    private readonly ICurrentContext _currentContext;
    private readonly IAccessClientQuery _accessClientQuery;
    private readonly IProjectRepository _projectRepository;
    private readonly ISecretRepository _secretRepository;
    private readonly IServiceAccountRepository _serviceAccountRepository;

    public CountsController(
        ICurrentContext currentContext,
        IAccessClientQuery accessClientQuery,
        IProjectRepository projectRepository,
        ISecretRepository secretRepository,
        IServiceAccountRepository serviceAccountRepository)
    {
        _currentContext = currentContext;
        _accessClientQuery = accessClientQuery;
        _projectRepository = projectRepository;
        _secretRepository = secretRepository;
        _serviceAccountRepository = serviceAccountRepository;
    }

    [HttpGet("organizations/{organizationId}/sm-counts")]
    public async Task<OrganizationCountsResponseModel> GetByOrganizationAsync([FromRoute] Guid organizationId)
    {
        var (accessType, userId) = await GetAccessClientAsync(organizationId);

        var projectsCountTask = _projectRepository.GetProjectCountByOrganizationIdAsync(organizationId,
            userId, accessType);

        var secretsCountTask = _secretRepository.GetSecretsCountByOrganizationIdAsync(organizationId,
            userId, accessType);

        var serviceAccountsCountsTask = _serviceAccountRepository.GetServiceAccountCountByOrganizationIdAsync(
            organizationId, userId, accessType);

        var counts = await Task.WhenAll(projectsCountTask, secretsCountTask, serviceAccountsCountsTask);

        return new OrganizationCountsResponseModel
        {
            Projects = counts[0],
            Secrets = counts[1],
            ServiceAccounts = counts[2]
        };
    }


    [HttpGet("projects/{projectId}/sm-counts")]
    public async Task<ProjectCountsResponseModel> GetByProjectAsync([FromRoute] Guid projectId)
    {
        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
        {
            throw new NotFoundException();
        }

        var (accessType, userId) = await GetAccessClientAsync(project.OrganizationId);

        var projectsCounts = await _projectRepository.GetProjectCountsByIdAsync(projectId, userId, accessType);

        return new ProjectCountsResponseModel
        {
            Secrets = projectsCounts.Secrets,
            People = projectsCounts.People,
            ServiceAccounts = projectsCounts.ServiceAccounts
        };
    }

    [HttpGet("service-accounts/{serviceAccountId}/sm-counts")]
    public async Task<ServiceAccountCountsResponseModel> GetByServiceAccountAsync([FromRoute] Guid serviceAccountId)
    {
        var serviceAccount = await _serviceAccountRepository.GetByIdAsync(serviceAccountId);
        if (serviceAccount == null)
        {
            throw new NotFoundException();
        }

        var (accessType, userId) = await GetAccessClientAsync(serviceAccount.OrganizationId);

        var serviceAccountCounts =
            await _serviceAccountRepository.GetServiceAccountCountsByIdAsync(serviceAccountId, userId, accessType);

        return new ServiceAccountCountsResponseModel
        {
            Projects = serviceAccountCounts.Projects,
            People = serviceAccountCounts.People,
            AccessTokens = serviceAccountCounts.AccessTokens
        };
    }

    private async Task<(AccessClientType, Guid)> GetAccessClientAsync(Guid organizationId)
    {
        if (!_currentContext.AccessSecretsManager(organizationId))
        {
            throw new NotFoundException();
        }

        var (accessType, userId) = await _accessClientQuery.GetAccessClientAsync(User, organizationId);
        if (accessType == AccessClientType.ServiceAccount)
        {
            throw new NotFoundException();
        }

        return (accessType, userId);
    }
}
