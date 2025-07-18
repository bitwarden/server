// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Api.Models.Response;
using Bit.Api.Utilities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Bit.Core.Vault.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers;

[Route("events")]
[Authorize("Application")]
public class EventsController : Controller
{
    private readonly IUserService _userService;
    private readonly ICipherRepository _cipherRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IProviderUserRepository _providerUserRepository;
    private readonly IEventRepository _eventRepository;
    private readonly ICurrentContext _currentContext;
    private readonly ISecretRepository _secretRepository;
    private readonly IProjectRepository _projectRepository;

    public EventsController(
        IUserService userService,
        ICipherRepository cipherRepository,
        IOrganizationUserRepository organizationUserRepository,
        IProviderUserRepository providerUserRepository,
        IEventRepository eventRepository,
        ICurrentContext currentContext,
        ISecretRepository secretRepository,
        IProjectRepository projectRepository)
    {
        _userService = userService;
        _cipherRepository = cipherRepository;
        _organizationUserRepository = organizationUserRepository;
        _providerUserRepository = providerUserRepository;
        _eventRepository = eventRepository;
        _currentContext = currentContext;
        _secretRepository = secretRepository;
        _projectRepository = projectRepository;
    }

    [HttpGet("")]
    public async Task<ListResponseModel<EventResponseModel>> GetUser(
        [FromQuery] DateTime? start = null, [FromQuery] DateTime? end = null, [FromQuery] string continuationToken = null)
    {
        var dateRange = ApiHelpers.GetDateRange(start, end);
        var userId = _userService.GetProperUserId(User).Value;
        var result = await _eventRepository.GetManyByUserAsync(userId, dateRange.Item1, dateRange.Item2,
            new PageOptions { ContinuationToken = continuationToken });
        var responses = result.Data.Select(e => new EventResponseModel(e));
        return new ListResponseModel<EventResponseModel>(responses, result.ContinuationToken);
    }

    [HttpGet("~/ciphers/{id}/events")]
    public async Task<ListResponseModel<EventResponseModel>> GetCipher(string id,
        [FromQuery] DateTime? start = null, [FromQuery] DateTime? end = null, [FromQuery] string continuationToken = null)
    {
        var cipher = await _cipherRepository.GetByIdAsync(new Guid(id));
        if (cipher == null)
        {
            throw new NotFoundException();
        }

        var canView = false;
        if (cipher.OrganizationId.HasValue)
        {
            canView = await _currentContext.AccessEventLogs(cipher.OrganizationId.Value);
        }
        else if (cipher.UserId.HasValue)
        {
            var userId = _userService.GetProperUserId(User).Value;
            canView = userId == cipher.UserId.Value;
        }

        if (!canView)
        {
            throw new NotFoundException();
        }

        var dateRange = ApiHelpers.GetDateRange(start, end);
        var result = await _eventRepository.GetManyByCipherAsync(cipher, dateRange.Item1, dateRange.Item2,
            new PageOptions { ContinuationToken = continuationToken });
        var responses = result.Data.Select(e => new EventResponseModel(e));
        return new ListResponseModel<EventResponseModel>(responses, result.ContinuationToken);
    }

    [HttpGet("~/organizations/{id}/events")]
    public async Task<ListResponseModel<EventResponseModel>> GetOrganization(string id,
        [FromQuery] DateTime? start = null, [FromQuery] DateTime? end = null, [FromQuery] string continuationToken = null)
    {
        var orgId = new Guid(id);
        if (!await _currentContext.AccessEventLogs(orgId))
        {
            throw new NotFoundException();
        }

        var dateRange = ApiHelpers.GetDateRange(start, end);
        var result = await _eventRepository.GetManyByOrganizationAsync(orgId, dateRange.Item1, dateRange.Item2,
            new PageOptions { ContinuationToken = continuationToken });
        var responses = result.Data.Select(e => new EventResponseModel(e));
        return new ListResponseModel<EventResponseModel>(responses, result.ContinuationToken);
    }

    [HttpGet("~/organization/{orgId}/secrets/{id}/events")]
    public async Task<ListResponseModel<EventResponseModel>> GetSecrets(
        string id, string orgId,
        [FromQuery] DateTime? start = null,
        [FromQuery] DateTime? end = null,
        [FromQuery] string continuationToken = null)
    {
        if (!Guid.TryParse(id, out var secretGuid) || !Guid.TryParse(orgId, out var orgGuid))
        {
            throw new NotFoundException();
        }

        var secret = await _secretRepository.GetByIdAsync(secretGuid);
        var orgIdForVerification = secret?.OrganizationId ?? orgGuid;
        var secretOrg = _currentContext.GetOrganization(orgIdForVerification);

        if (secretOrg == null || !await _currentContext.AccessEventLogs(secretOrg.Id))
        {
            throw new NotFoundException();
        }

        bool canViewLogs = false;

        if (secret == null)
        {
            secret = new Core.SecretsManager.Entities.Secret { Id = secretGuid, OrganizationId = orgGuid };
            canViewLogs = secretOrg.Type is Core.Enums.OrganizationUserType.Admin or Core.Enums.OrganizationUserType.Owner;
        }
        else
        {
            if (!_currentContext.AccessSecretsManager(secret.OrganizationId))
            {
                throw new NotFoundException();
            }

            var userId = _userService.GetProperUserId(User)!.Value;
            var isAdmin = await _currentContext.OrganizationAdmin(secret.OrganizationId);
            var accessClient = AccessClientHelper.ToAccessClient(_currentContext.IdentityClientType, isAdmin);
            var access = await _secretRepository.AccessToSecretAsync(secret.Id, userId, accessClient);
            canViewLogs = access.Read;
        }

        if (!canViewLogs)
        {
            throw new NotFoundException();
        }

        var (fromDate, toDate) = ApiHelpers.GetDateRange(start, end);
        var result = await _eventRepository.GetManyBySecretAsync(secret, fromDate, toDate, new PageOptions { ContinuationToken = continuationToken });
        var responses = result.Data.Select(e => new EventResponseModel(e));
        return new ListResponseModel<EventResponseModel>(responses, result.ContinuationToken);
    }


    [HttpGet("~/organization/{orgId}/projects/{id}/events")]
    public async Task<ListResponseModel<EventResponseModel>> GetProjects(
        string id,
        string orgId,
        [FromQuery] DateTime? start = null,
        [FromQuery] DateTime? end = null,
        [FromQuery] string continuationToken = null)
    {
        if (!Guid.TryParse(id, out var projectGuid) || !Guid.TryParse(orgId, out var orgGuid))
        {
            throw new NotFoundException();
        }

        // Try to get project
        var project = await _projectRepository.GetByIdAsync(projectGuid);
        var orgIdForVerification = project?.OrganizationId ?? orgGuid;
        var org = _currentContext.GetOrganization(orgIdForVerification);

        // Fallback project if it was deleted
        project ??= new Core.SecretsManager.Entities.Project
        {
            Id = projectGuid,
            OrganizationId = orgGuid
        };

        if (org == null || !await _currentContext.AccessEventLogs(org.Id))
        {
            throw new NotFoundException();
        }

        // Get logs
        var (fromDate, toDate) = ApiHelpers.GetDateRange(start, end);
        var result = await _eventRepository.GetManyByProjectAsync(
            project,
            fromDate,
            toDate,
            new PageOptions { ContinuationToken = continuationToken });

        var responses = result.Data.Select(e => new EventResponseModel(e));
        return new ListResponseModel<EventResponseModel>(responses, result.ContinuationToken);
    }

    [HttpGet("~/organizations/{orgId}/users/{id}/events")]
    public async Task<ListResponseModel<EventResponseModel>> GetOrganizationUser(string orgId, string id,
        [FromQuery] DateTime? start = null, [FromQuery] DateTime? end = null, [FromQuery] string continuationToken = null)
    {
        var organizationUser = await _organizationUserRepository.GetByIdAsync(new Guid(id));
        if (organizationUser == null || !organizationUser.UserId.HasValue ||
            !await _currentContext.AccessEventLogs(organizationUser.OrganizationId))
        {
            throw new NotFoundException();
        }

        var dateRange = ApiHelpers.GetDateRange(start, end);
        var result = await _eventRepository.GetManyByOrganizationActingUserAsync(organizationUser.OrganizationId,
            organizationUser.UserId.Value, dateRange.Item1, dateRange.Item2,
            new PageOptions { ContinuationToken = continuationToken });
        var responses = result.Data.Select(e => new EventResponseModel(e));
        return new ListResponseModel<EventResponseModel>(responses, result.ContinuationToken);
    }

    [HttpGet("~/providers/{providerId:guid}/events")]
    public async Task<ListResponseModel<EventResponseModel>> GetProvider(Guid providerId,
        [FromQuery] DateTime? start = null, [FromQuery] DateTime? end = null, [FromQuery] string continuationToken = null)
    {
        if (!_currentContext.ProviderAccessEventLogs(providerId))
        {
            throw new NotFoundException();
        }

        var dateRange = ApiHelpers.GetDateRange(start, end);
        var result = await _eventRepository.GetManyByProviderAsync(providerId, dateRange.Item1, dateRange.Item2,
            new PageOptions { ContinuationToken = continuationToken });
        var responses = result.Data.Select(e => new EventResponseModel(e));
        return new ListResponseModel<EventResponseModel>(responses, result.ContinuationToken);
    }

    [HttpGet("~/providers/{providerId:guid}/users/{id:guid}/events")]
    public async Task<ListResponseModel<EventResponseModel>> GetProviderUser(Guid providerId, Guid id,
        [FromQuery] DateTime? start = null, [FromQuery] DateTime? end = null, [FromQuery] string continuationToken = null)
    {
        var providerUser = await _providerUserRepository.GetByIdAsync(id);
        if (providerUser == null || !providerUser.UserId.HasValue ||
            !_currentContext.ProviderAccessEventLogs(providerUser.ProviderId))
        {
            throw new NotFoundException();
        }

        var dateRange = ApiHelpers.GetDateRange(start, end);
        var result = await _eventRepository.GetManyByProviderActingUserAsync(providerUser.ProviderId,
            providerUser.UserId.Value, dateRange.Item1, dateRange.Item2,
            new PageOptions { ContinuationToken = continuationToken });
        var responses = result.Data.Select(e => new EventResponseModel(e));
        return new ListResponseModel<EventResponseModel>(responses, result.ContinuationToken);
    }
}
