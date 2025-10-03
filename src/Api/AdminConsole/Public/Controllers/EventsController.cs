// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Net;
using Bit.Api.Models.Public.Request;
using Bit.Api.Models.Public.Response;
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

namespace Bit.Api.Public.Controllers;

[Route("public/events")]
[Authorize("Organization")]
public class EventsController : Controller
{
    private readonly IEventRepository _eventRepository;
    private readonly ICipherRepository _cipherRepository;
    private readonly ICurrentContext _currentContext;
    private readonly ISecretRepository _secretRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IUserService _userService;

    public EventsController(
        IEventRepository eventRepository,
        ICipherRepository cipherRepository,
        ICurrentContext currentContext,
        ISecretRepository secretRepository,
        IProjectRepository projectRepository,
        IUserService userService)
    {
        _eventRepository = eventRepository;
        _cipherRepository = cipherRepository;
        _currentContext = currentContext;
        _secretRepository = secretRepository;
        _projectRepository = projectRepository;
        _userService = userService;
    }

    /// <summary>
    /// List all events.
    /// </summary>
    /// <remarks>
    /// Returns a filtered list of your organization's event logs, paged by a continuation token.
    /// If no filters are provided, it will return the last 30 days of event for the organization.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PagedListResponseModel<EventResponseModel>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> List([FromQuery] EventFilterRequestModel request)
    {
        var dateRange = request.ToDateRange();
        var result = new PagedResult<IEvent>();
        if (request.ActingUserId.HasValue)
        {
            result = await _eventRepository.GetManyByOrganizationActingUserAsync(
                _currentContext.OrganizationId.Value, request.ActingUserId.Value, dateRange.Item1, dateRange.Item2,
                new PageOptions { ContinuationToken = request.ContinuationToken });
        }
        else if (request.ItemId.HasValue)
        {
            var cipher = await _cipherRepository.GetByIdAsync(request.ItemId.Value);
            if (cipher != null && cipher.OrganizationId == _currentContext.OrganizationId.Value)
            {
                result = await _eventRepository.GetManyByCipherAsync(
                    cipher, dateRange.Item1, dateRange.Item2,
                    new PageOptions { ContinuationToken = request.ContinuationToken });
            }
        }
        else if (request.SecretId.HasValue)
        {
            var secret = await _secretRepository.GetByIdAsync(request.SecretId.Value);
            bool canViewLogs = false;

            if (secret == null)
            {
                var currentContextOrg = _currentContext.OrganizationId.Value;
                var secretOrg = _currentContext.GetOrganization(currentContextOrg);
                secret = new Core.SecretsManager.Entities.Secret { Id = request.SecretId.Value, OrganizationId = currentContextOrg };
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

            if (secret != null && secret.OrganizationId == _currentContext.OrganizationId.Value)
            {
                result = await _eventRepository.GetManyBySecretAsync(
                    secret, dateRange.Item1, dateRange.Item2,
                    new PageOptions { ContinuationToken = request.ContinuationToken });
            }
        }
        else if (request.ProjectId.HasValue)
        {
            var project = await _projectRepository.GetByIdAsync(request.ProjectId.Value);
            if (project != null && project.OrganizationId == _currentContext.OrganizationId.Value)
            {
                result = await _eventRepository.GetManyByProjectAsync(
                    project, dateRange.Item1, dateRange.Item2,
                    new PageOptions { ContinuationToken = request.ContinuationToken });
            }
        }
        else
        {
            result = await _eventRepository.GetManyByOrganizationAsync(
                _currentContext.OrganizationId.Value, dateRange.Item1, dateRange.Item2,
                new PageOptions { ContinuationToken = request.ContinuationToken });
        }

        var eventResponses = result.Data.Select(e => new EventResponseModel(e));
        var response = new PagedListResponseModel<EventResponseModel>(eventResponses, result.ContinuationToken);
        return new JsonResult(response);
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
