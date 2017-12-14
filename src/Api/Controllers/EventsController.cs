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

        [HttpGet("user")]
        public async Task<ListResponseModel<EventResponseModel>> GetUser(
            [FromQuery]DateTime? start = null, [FromQuery]DateTime? end = null)
        {
            var dateRange = GetDateRange(start, end);
            var userId = _userService.GetProperUserId(User).Value;
            var events = await _eventRepository.GetManyByUserAsync(userId, dateRange.Item1, dateRange.Item2);
            var responses = events.Select(e => new EventResponseModel(e));
            return new ListResponseModel<EventResponseModel>(responses);
        }

        [HttpGet("organization/{id}")]
        public async Task<ListResponseModel<EventResponseModel>> GetOrganization(string id,
            [FromQuery]DateTime? start = null, [FromQuery]DateTime? end = null)
        {
            var orgId = new Guid(id);
            if(!_currentContext.OrganizationAdmin(orgId))
            {
                throw new NotFoundException();
            }

            var dateRange = GetDateRange(start, end);
            var events = await _eventRepository.GetManyByOrganizationAsync(orgId, dateRange.Item1, dateRange.Item2);
            var responses = events.Select(e => new EventResponseModel(e));
            return new ListResponseModel<EventResponseModel>(responses);
        }

        private Tuple<DateTime, DateTime> GetDateRange(DateTime? start, DateTime? end)
        {
            var endSet = false;
            if(!end.HasValue)
            {
                endSet = true;
                end = DateTime.UtcNow.Date.AddDays(1).AddMilliseconds(-1);
            }

            if(!start.HasValue)
            {
                start = end.Value.AddDays(-30);
                if(endSet)
                {
                    start = end.Value.AddMilliseconds(1);
                }
            }

            if(start.Value > end.Value || (end.Value - start.Value) > TimeSpan.FromDays(32))
            {
                throw new BadRequestException("Invalid date range.");
            }

            return new Tuple<DateTime, DateTime>(start.Value, end.Value);
        }
    }
}
