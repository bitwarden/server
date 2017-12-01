using System.Threading.Tasks;
using System;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Models.Data;
using System.Linq;
using System.Collections.Generic;
using Microsoft.WindowsAzure.Storage.Table;

namespace Bit.Core.Services
{
    public class EventService : IEventService
    {
        private readonly IEventRepository _eventRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly GlobalSettings _globalSettings;

        public EventService(
            IEventRepository eventRepository,
            IOrganizationUserRepository organizationUserRepository,
            GlobalSettings globalSettings)
        {
            _eventRepository = eventRepository;
            _organizationUserRepository = organizationUserRepository;
            _globalSettings = globalSettings;
        }

        public async Task LogUserEventAsync(Guid userId, EventType type)
        {
            var events = new List<ITableEntity> { new UserEvent(userId, type) };
            var orgs = await _organizationUserRepository.GetManyByUserAsync(userId);
            var orgEvents = orgs.Where(o => o.Status == OrganizationUserStatusType.Confirmed)
                .Select(o => new UserEvent(userId, o.Id, type));
            if(orgEvents.Any())
            {
                events.AddRange(orgEvents);
                await _eventRepository.CreateManyAsync(events);
            }
            else
            {
                await _eventRepository.CreateAsync(events.First());
            }
        }

        public async Task LogUserEventAsync(Guid userId, CurrentContext currentContext, EventType type)
        {
            var events = new List<ITableEntity> { new UserEvent(userId, type) };
            var orgEvents = currentContext.Organizations.Select(o => new UserEvent(userId, o.Id, type));
            if(orgEvents.Any())
            {
                events.AddRange(orgEvents);
                await _eventRepository.CreateManyAsync(events);
            }
            else
            {
                await _eventRepository.CreateAsync(events.First());
            }
        }
    }
}
