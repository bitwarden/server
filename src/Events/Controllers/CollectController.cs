using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core;
using Bit.Core.Enums;
using Bit.Core.Models.Table;
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
        public async Task<IActionResult> Post([FromBody]IEnumerable<EventModel> model)
        {
            if(model == null || !model.Any())
            {
                return new BadRequestResult();
            }
            var cipherEvents = new List<Tuple<Cipher, EventType, DateTime?>>();
            foreach(var eventModel in model)
            {
                switch(eventModel.Type)
                {
                    // User events
                    case EventType.User_ClientExportedVault:
                        await _eventService.LogUserEventAsync(_currentContext.UserId.Value, eventModel.Type, eventModel.Date);
                        break;
                    // Cipher events
                    case EventType.Cipher_ClientAutofilled:
                    case EventType.Cipher_ClientCopiedHiddenField:
                    case EventType.Cipher_ClientCopiedPassword:
                    case EventType.Cipher_ClientCopiedCardCode:
                    case EventType.Cipher_ClientToggledCardCodeVisible:
                    case EventType.Cipher_ClientToggledHiddenFieldVisible:
                    case EventType.Cipher_ClientToggledPasswordVisible:
                    case EventType.Cipher_ClientViewed:
                        if(!eventModel.CipherId.HasValue)
                        {
                            continue;
                        }
                        var cipher = await _cipherRepository.GetByIdAsync(eventModel.CipherId.Value,
                            _currentContext.UserId.Value);
                        if(cipher == null)
                        {
                            continue;
                        }
                        cipherEvents.Add(new Tuple<Cipher, EventType, DateTime?>(cipher, eventModel.Type, eventModel.Date));
                        break;
                    default:
                        continue;
                }
            }
            if(cipherEvents.Any())
            {
                await _eventService.LogCipherEventsAsync(cipherEvents);
            }
            return new OkResult();
        }
    }
}
