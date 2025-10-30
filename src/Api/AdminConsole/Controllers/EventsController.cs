// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Api.Models.Response;
using Bit.Api.Utilities;
using Bit.Api.Utilities.DiagnosticTools;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Entities;
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
    private readonly IServiceAccountRepository _serviceAccountRepository;
    private readonly ILogger<EventsController> _logger;
    private readonly IFeatureService _featureService;


    public EventsController(IUserService userService,
        ICipherRepository cipherRepository,
        IOrganizationUserRepository organizationUserRepository,
        IProviderUserRepository providerUserRepository,
        IEventRepository eventRepository,
        ICurrentContext currentContext,
        ISecretRepository secretRepository,
        IProjectRepository projectRepository,
        IServiceAccountRepository serviceAccountRepository,
        ILogger<EventsController> logger,
        IFeatureService featureService)
    {
        _userService = userService;
        _cipherRepository = cipherRepository;
        _organizationUserRepository = organizationUserRepository;
        _providerUserRepository = providerUserRepository;
        _eventRepository = eventRepository;
        _currentContext = currentContext;
        _secretRepository = secretRepository;
        _projectRepository = projectRepository;
        _serviceAccountRepository = serviceAccountRepository;
        _logger = logger;
        _featureService = featureService;
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

        _logger.LogAggregateData(_featureService, orgId, continuationToken, responses, start, end);

        return new ListResponseModel<EventResponseModel>(responses, result.ContinuationToken);
    }

    [HttpGet("~/organization/{orgId}/secrets/{id}/events")]
    public async Task<ListResponseModel<EventResponseModel>> GetSecrets(
        Guid id, Guid orgId,
        [FromQuery] DateTime? start = null,
        [FromQuery] DateTime? end = null,
        [FromQuery] string continuationToken = null)
    {
        if (id == Guid.Empty || orgId == Guid.Empty)
        {
            throw new NotFoundException();
        }

        var secret = await _secretRepository.GetByIdAsync(id);
        var orgIdForVerification = secret?.OrganizationId ?? orgId;
        var secretOrg = _currentContext.GetOrganization(orgIdForVerification);

        if (secretOrg == null || !await _currentContext.AccessEventLogs(secretOrg.Id))
        {
            throw new NotFoundException();
        }

        bool canViewLogs = false;

        if (secret == null)
        {
            secret = new Core.SecretsManager.Entities.Secret { Id = id, OrganizationId = orgId };
            canViewLogs = secretOrg.Type is Core.Enums.OrganizationUserType.Admin or Core.Enums.OrganizationUserType.Owner;
        }
        else
        {
            canViewLogs = await CanViewSecretsLogs(secret);
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
        Guid id,
        Guid orgId,
        [FromQuery] DateTime? start = null,
        [FromQuery] DateTime? end = null,
        [FromQuery] string continuationToken = null)
    {
        if (id == Guid.Empty || orgId == Guid.Empty)
        {
            throw new NotFoundException();
        }

        var project = await GetProject(id, orgId);
        await ValidateOrganization(project);

        var (fromDate, toDate) = ApiHelpers.GetDateRange(start, end);
        var result = await _eventRepository.GetManyByProjectAsync(
            project,
            fromDate,
            toDate,
            new PageOptions { ContinuationToken = continuationToken });

        var responses = result.Data.Select(e => new EventResponseModel(e));
        return new ListResponseModel<EventResponseModel>(responses, result.ContinuationToken);
    }

    [HttpGet("~/organization/{orgId}/service-account/{id}/events")]
    public async Task<ListResponseModel<EventResponseModel>> GetServiceAccounts(
       Guid orgId,
       Guid id,
       [FromQuery] DateTime? start = null,
       [FromQuery] DateTime? end = null,
       [FromQuery] string continuationToken = null)
    {
        if (id == Guid.Empty || orgId == Guid.Empty)
        {
            throw new NotFoundException();
        }

        var serviceAccount = await GetServiceAccount(id, orgId);
        var org = _currentContext.GetOrganization(orgId);

        if (org == null || !await _currentContext.AccessEventLogs(org.Id))
        {
            throw new NotFoundException();
        }

        var (fromDate, toDate) = ApiHelpers.GetDateRange(start, end);
        var result = await _eventRepository.GetManyByOrganizationServiceAccountAsync(
            serviceAccount.OrganizationId,
            serviceAccount.Id,
            fromDate,
            toDate,
            new PageOptions { ContinuationToken = continuationToken });

        var responses = result.Data.Select(e => new EventResponseModel(e));
        return new ListResponseModel<EventResponseModel>(responses, result.ContinuationToken);
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    private async Task<ServiceAccount> GetServiceAccount(Guid serviceAccountId, Guid orgId)
    {
        var serviceAccount = await _serviceAccountRepository.GetByIdAsync(serviceAccountId);
        if (serviceAccount != null)
        {
            return serviceAccount;
        }

        var fallbackServiceAccount = new ServiceAccount
        {
            Id = serviceAccountId,
            OrganizationId = orgId
        };

        return fallbackServiceAccount;
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

    [ApiExplorerSettings(IgnoreApi = true)]
    private async Task ValidateOrganization(Project project)
    {
        var org = _currentContext.GetOrganization(project.OrganizationId);

        if (org == null || !await _currentContext.AccessEventLogs(org.Id))
        {
            throw new NotFoundException();
        }
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    private async Task<Project> GetProject(Guid projectGuid, Guid orgGuid)
    {
        var project = await _projectRepository.GetByIdAsync(projectGuid);
        if (project != null)
        {
            return project;
        }

        var fallbackProject = new Project
        {
            Id = projectGuid,
            OrganizationId = orgGuid
        };

        return fallbackProject;
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    private async Task<bool> CanViewSecretsLogs(Secret secret)
    {
        if (!_currentContext.AccessSecretsManager(secret.OrganizationId))
        {
            throw new NotFoundException();
        }

        var userId = _userService.GetProperUserId(User)!.Value;
        var isAdmin = await _currentContext.OrganizationAdmin(secret.OrganizationId);
        var accessClient = AccessClientHelper.ToAccessClient(_currentContext.IdentityClientType, isAdmin);
        var access = await _secretRepository.AccessToSecretAsync(secret.Id, userId, accessClient);
        return access.Read;
    }
}
