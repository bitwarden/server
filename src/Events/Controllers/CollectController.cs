using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Events.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Events.Controllers
{
    [Route("collect")]
    [Authorize("Application")]
    public class CollectController : Controller
    {
        private readonly CurrentContext _currentContext;
        private readonly IEventService _eventService;
        private readonly ICipherRepository _cipherRepository;

        public CollectController(
            CurrentContext currentContext,
            IEventService eventService,
            ICipherRepository cipherRepository)
        {
            _currentContext = currentContext;
            _eventService = eventService;
            _cipherRepository = cipherRepository;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody]EventModel model)
        {
            if(await LogEventAsync(model))
            {
                return new OkResult();
            }
            else
            {
                return new BadRequestResult();
            }
        }

        [HttpPost("many")]
        public async Task<IActionResult> PostMany([FromBody]IEnumerable<EventModel> model)
        {
            if(model == null || !model.Any())
            {
                return new BadRequestResult();
            }
            foreach(var eventModel in model)
            {
                await LogEventAsync(eventModel);
            }
            return new OkResult();
        }

        private async Task<bool> LogEventAsync(EventModel model)
        {
            switch(model.Type)
            {
                // User events
                case EventType.User_ClientExportedVault:
                    await _eventService.LogUserEventAsync(_currentContext.UserId.Value, model.Type);
                    break;
                // Cipher events
                case EventType.Cipher_ClientAutofilled:
                case EventType.Cipher_ClientCopedHiddenField:
                case EventType.Cipher_ClientCopiedPassword:
                case EventType.Cipher_ClientToggledHiddenFieldVisible:
                case EventType.Cipher_ClientToggledPasswordVisible:
                case EventType.Cipher_ClientViewed:
                    if(!model.CipherId.HasValue)
                    {
                        return false;
                    }
                    var cipher = await _cipherRepository.GetByIdAsync(model.CipherId.Value,
                        _currentContext.UserId.Value);
                    if(cipher == null)
                    {
                        return false;
                    }
                    await _eventService.LogCipherEventAsync(cipher, model.Type);
                    break;
                default:
                    return false;
            }
            return true;
        }
    }
}
