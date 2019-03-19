using System.Threading.Tasks;
using Bit.Core;
using Bit.Core.Enums;
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

        [HttpGet("~/collect")]
        public Task<IActionResult> GetCollect([FromQuery]EventModel model)
        {
            return PostCollect(model);
        }

        [HttpPost("~/collect")]
        public async Task<IActionResult> PostCollect([FromBody]EventModel model)
        {
            switch(model.Type)
            {
                // User events
                case EventType.User_LoggedIn:
                    await _eventService.LogUserEventAsync(_currentContext.UserId.Value, model.Type);
                    break;
                // Cipher events
                case EventType.Cipher_Created:
                    if(!model.CipherId.HasValue)
                    {
                        return new BadRequestResult();
                    }
                    var cipher = await _cipherRepository.GetByIdAsync(model.CipherId.Value,
                        _currentContext.UserId.Value);
                    if(cipher == null)
                    {
                        return new BadRequestResult();
                    }
                    await _eventService.LogCipherEventAsync(cipher, model.Type);
                    break;
                default:
                    return new BadRequestResult();
            }
            return new OkResult();
        }
    }
}
