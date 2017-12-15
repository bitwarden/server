using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authorization;
using Bit.Core.Exceptions;
using Bit.Core.Models.Api;
using Bit.Core.Services;
using Bit.Core;
using Bit.Core.Models.Data;

namespace Bit.Api.Controllers
{
    [Route("events")]
    [Authorize("Application")]
    public class EventsController : Controller
    {
        private readonly IUserService _userService;
        private readonly IEventRepository _eventRepository;
        private readonly CurrentContext _currentContext;

        public EventsController(
            IUserService userService,
            IEventRepository eventRepository,
            CurrentContext currentContext)
        {
            _userService = userService;
            _eventRepository = eventRepository;
            _currentContext = currentContext;
        }

        [HttpGet("")]
        public async Task<ListResponseModel<EventResponseModel>> GetUser(
            [FromQuery]DateTime? start = null, [FromQuery]DateTime? end = null, [FromQuery]string continuationToken = null)
        {
            var dateRange = GetDateRange(start, end);
            var userId = _userService.GetProperUserId(User).Value;
            var result = await _eventRepository.GetManyByUserAsync(userId, dateRange.Item1, dateRange.Item2,
                new PageOptions { ContinuationToken = continuationToken });
            var responses = result.Data.Select(e => new EventResponseModel(e));
            return new ListResponseModel<EventResponseModel>(responses, result.ContinuationToken);
        }

        [HttpGet("~/organizations/{id}/events")]
        public async Task<ListResponseModel<EventResponseModel>> GetOrganization(string id,
            [FromQuery]DateTime? start = null, [FromQuery]DateTime? end = null, [FromQuery]string continuationToken = null)
        {
            var orgId = new Guid(id);
            if(!_currentContext.OrganizationAdmin(orgId))
            {
                throw new NotFoundException();
            }

            var dateRange = GetDateRange(start, end);
            var result = await _eventRepository.GetManyByOrganizationAsync(orgId, dateRange.Item1, dateRange.Item2,
                new PageOptions { ContinuationToken = continuationToken });
            var responses = result.Data.Select(e => new EventResponseModel(e));
            return new ListResponseModel<EventResponseModel>(responses, result.ContinuationToken);
        }

        private Tuple<DateTime, DateTime> GetDateRange(DateTime? start, DateTime? end)
        {
            if(!end.HasValue || !start.HasValue)
            {
                end = DateTime.UtcNow.Date.AddDays(1).AddMilliseconds(-1);
                start = DateTime.UtcNow.Date.AddDays(-30);
            }
            else if(start.Value > end.Value)
            {
                var newEnd = start;
                start = end;
                end = newEnd;
            }

            if((end.Value - start.Value) > TimeSpan.FromDays(367))
            {
                throw new BadRequestException("Range too large.");
            }

            return new Tuple<DateTime, DateTime>(start.Value, end.Value);
        }
    }
}
