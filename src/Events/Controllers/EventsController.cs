using System;
using System.Threading.Tasks;
using Bit.Core;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Events.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Events.Controllers
{
    [Authorize("Application")]
    public class EventsController : Controller
    {
        private readonly CurrentContext _currentContext;
        private readonly IEventService _eventService;
        private readonly ICipherRepository _cipherRepository;

        public EventsController(
            CurrentContext currentContext,
            IEventService eventService,
            ICipherRepository cipherRepository)
        {
            _currentContext = currentContext;
            _eventService = eventService;
            _cipherRepository = cipherRepository;
        }

        [HttpPost("~/cipher/{id}")]
        public async Task PostCipher(Guid id, [FromBody]EventModel model)
        {
            var cipher = await _cipherRepository.GetByIdAsync(id, _currentContext.UserId.Value);
            if(cipher != null)
            {
                await _eventService.LogCipherEventAsync(cipher, model.Type);
            }
        }

        [HttpPost("~/user")]
        public async Task PostUser([FromBody]EventModel model)
        {
            await _eventService.LogUserEventAsync(_currentContext.UserId.Value, model.Type);
        }
    }
}
