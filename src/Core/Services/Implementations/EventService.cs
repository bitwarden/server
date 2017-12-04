using System.Threading.Tasks;
using System;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Models.Data;
using System.Linq;
using System.Collections.Generic;
using Microsoft.WindowsAzure.Storage.Table;
using Bit.Core.Models.Table;

namespace Bit.Core.Services
{
    public class EventService : IEventService
    {
        private readonly IEventWriteService _eventWriteService;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly CurrentContext _currentContext;
        private readonly GlobalSettings _globalSettings;

        public EventService(
            IEventWriteService eventWriteService,
            IOrganizationUserRepository organizationUserRepository,
            CurrentContext currentContext,
            GlobalSettings globalSettings)
        {
            _eventWriteService = eventWriteService;
            _organizationUserRepository = organizationUserRepository;
            _currentContext = currentContext;
            _globalSettings = globalSettings;
        }

        public async Task LogUserEventAsync(Guid userId, EventType type)
        {
            var events = new List<ITableEntity> { new UserEvent(userId, type) };

            IEnumerable<UserEvent> orgEvents;
            if(_currentContext.UserId.HasValue)
            {
                orgEvents = _currentContext.Organizations.Select(o => new UserEvent(userId, o.Id, type));
            }
            else
            {
                var orgs = await _organizationUserRepository.GetManyByUserAsync(userId);
                orgEvents = orgs.Where(o => o.Status == OrganizationUserStatusType.Confirmed)
                    .Select(o => new UserEvent(userId, o.Id, type));
            }

            if(orgEvents.Any())
            {
                events.AddRange(orgEvents);
                await _eventWriteService.CreateManyAsync(events);
            }
            else
            {
                await _eventWriteService.CreateAsync(events.First());
            }
        }

        public async Task LogCipherEventAsync(Cipher cipher, EventType type)
        {
            if(!cipher.OrganizationId.HasValue || (!_currentContext?.UserId.HasValue ?? true))
            {
                return;
            }

            var e = new CipherEvent(cipher, _currentContext?.UserId, type);
            await _eventWriteService.CreateAsync(e);
        }

        public async Task LogCollectionEventAsync(Collection collection, EventType type)
        {
            var e = new CollectionEvent(collection, _currentContext.UserId.Value, type);
            await _eventWriteService.CreateAsync(e);
        }

        public async Task LogGroupEventAsync(Group group, EventType type)
        {
            var e = new GroupEvent(group, _currentContext.UserId.Value, type);
            await _eventWriteService.CreateAsync(e);
        }

        public async Task LogOrganizationUserEventAsync(OrganizationUser organizationUser, EventType type)
        {
            var e = new OrganizationUserEvent(organizationUser, _currentContext.UserId.Value, type);
            await _eventWriteService.CreateAsync(e);
        }

        public async Task LogOrganizationEventAsync(Organization organization, EventType type)
        {
            var e = new OrganizationEvent(organization, _currentContext.UserId.Value, type);
            await _eventWriteService.CreateAsync(e);
        }
    }
}
