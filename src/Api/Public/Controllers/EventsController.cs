using System.Net;
using Bit.Api.Models.Public.Request;
using Bit.Api.Models.Public.Response;
using Bit.Core.Context;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
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

    public EventsController(
        IEventRepository eventRepository,
        ICipherRepository cipherRepository,
        ICurrentContext currentContext)
    {
        _eventRepository = eventRepository;
        _cipherRepository = cipherRepository;
        _currentContext = currentContext;
    }

    /// <summary>
    /// List all events.
    /// </summary>
    /// <remarks>
    /// Returns a filtered list of your organization's event logs, paged by a continuation token.
    /// If no filters are provided, it will return the last 30 days of event for the organization.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(ListResponseModel<EventResponseModel>), (int)HttpStatusCode.OK)]
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
        else
        {
            result = await _eventRepository.GetManyByOrganizationAsync(
                _currentContext.OrganizationId.Value, dateRange.Item1, dateRange.Item2,
                new PageOptions { ContinuationToken = request.ContinuationToken });
        }

        var eventResponses = result.Data.Select(e => new EventResponseModel(e));
        var response = new ListResponseModel<EventResponseModel>(eventResponses, result.ContinuationToken);
        return new JsonResult(response);
    }
}
