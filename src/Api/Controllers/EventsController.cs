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
        private readonly ICipherRepository _cipherRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly IEventRepository _eventRepository;
        private readonly CurrentContext _currentContext;

        public EventsController(
            IUserService userService,
            ICipherRepository cipherRepository,
            IOrganizationUserRepository organizationUserRepository,
            IEventRepository eventRepository,
            CurrentContext currentContext)
        {
            _userService = userService;
            _cipherRepository = cipherRepository;
            _organizationUserRepository = organizationUserRepository;
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

        [HttpGet("~/ciphers/{id}/events")]
        public async Task<ListResponseModel<EventResponseModel>> GetCipher(string id,
            [FromQuery]DateTime? start = null, [FromQuery]DateTime? end = null, [FromQuery]string continuationToken = null)
        {
            var cipher = await _cipherRepository.GetByIdAsync(new Guid(id));
            if(cipher == null)
            {
                throw new NotFoundException();
            }

            var canView = false;
            if(cipher.OrganizationId.HasValue)
            {
                canView = _currentContext.OrganizationAdmin(cipher.OrganizationId.Value);
            }
            else if(cipher.UserId.HasValue)
            {
                var userId = _userService.GetProperUserId(User).Value;
                canView = userId == cipher.UserId.Value;
            }

            if(!canView)
            {
                throw new NotFoundException();
            }

            var dateRange = GetDateRange(start, end);
            var result = await _eventRepository.GetManyByCipherAsync(cipher, dateRange.Item1, dateRange.Item2,
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

        [HttpGet("~/organizations/{orgId}/users/{id}/events")]
        public async Task<ListResponseModel<EventResponseModel>> GetOrganizationUser(string orgId, string id,
            [FromQuery]DateTime? start = null, [FromQuery]DateTime? end = null, [FromQuery]string continuationToken = null)
        {
            var organizationUser = await _organizationUserRepository.GetByIdAsync(new Guid(id));
            if(organizationUser == null || !organizationUser.UserId.HasValue ||
                !_currentContext.OrganizationAdmin(organizationUser.OrganizationId))
            {
                throw new NotFoundException();
            }

            var dateRange = GetDateRange(start, end);
            var result = await _eventRepository.GetManyByOrganizationActingUserAsync(organizationUser.OrganizationId,
                organizationUser.UserId.Value, dateRange.Item1, dateRange.Item2,
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
