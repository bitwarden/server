using System.Threading.Tasks;
using System;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Models.Data;

namespace Bit.Core.Services
{
    public class EventService : IEventService
    {
        private readonly IEventRepository _eventRepository;
        private readonly GlobalSettings _globalSettings;

        public EventService(
            IEventRepository eventRepository,
            GlobalSettings globalSettings)
        {
            _eventRepository = eventRepository;
            _globalSettings = globalSettings;
        }

        public async Task LogUserEventAsync(Guid userId, EventType type)
        {
            var userEvent = new UserEvent(userId, type);
            await _eventRepository.CreateAsync(userEvent);
        }
    }
}
