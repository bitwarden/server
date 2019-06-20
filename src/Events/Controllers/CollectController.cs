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

        [HttpGet]
        public Task<IActionResult> Get([FromQuery]EventModel model)
        {
            return Post(model);
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody]EventModel model)
        {
            switch(model.Type)
            {
                // User events
                case EventType.User_ExportedVault:
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
