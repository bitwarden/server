using System.Threading.Tasks;
using System;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Models.Data;
using System.Linq;
using System.Collections.Generic;
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
            var now = DateTime.UtcNow;
            var events = new List<IEvent>
            {
                new EventMessage
                {
                    UserId = userId,
                    Type = type,
                    Date = now
                }
            };

            IEnumerable<IEvent> orgEvents;
            if(_currentContext.UserId.HasValue)
            {
                orgEvents = _currentContext.Organizations.Select(o => new EventMessage
                {
                    OrganizationId = o.Id,
                    UserId = userId,
                    Type = type,
                    Date = DateTime.UtcNow
                });
            }
            else
            {
                var orgs = await _organizationUserRepository.GetManyByUserAsync(userId);
                orgEvents = orgs.Where(o => o.Status == OrganizationUserStatusType.Confirmed)
                    .Select(o => new EventMessage
                    {
                        OrganizationId = o.Id,
                        UserId = userId,
                        Type = type,
                        Date = DateTime.UtcNow
                    });
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
            // Only logging organization cipher events for now.
            if(!cipher.OrganizationId.HasValue || (!_currentContext?.UserId.HasValue ?? true))
            {
                return;
            }

            var e = new EventMessage
            {
                OrganizationId = cipher.OrganizationId,
                UserId = cipher.OrganizationId.HasValue ? null : cipher.UserId,
                CipherId = cipher.Id,
                Type = type,
                ActingUserId = _currentContext?.UserId,
                Date = DateTime.UtcNow
            };
            await _eventWriteService.CreateAsync(e);
        }

        public async Task LogCollectionEventAsync(Collection collection, EventType type)
        {
            var e = new EventMessage
            {
                OrganizationId = collection.OrganizationId,
                CollectionId = collection.Id,
                Type = type,
                ActingUserId = _currentContext?.UserId,
                Date = DateTime.UtcNow
            };
            await _eventWriteService.CreateAsync(e);
        }

        public async Task LogGroupEventAsync(Group group, EventType type)
        {
            var e = new EventMessage
            {
                OrganizationId = group.OrganizationId,
                GroupId = group.Id,
                Type = type,
                ActingUserId = _currentContext?.UserId,
                Date = DateTime.UtcNow
            };
            await _eventWriteService.CreateAsync(e);
        }

        public async Task LogOrganizationUserEventAsync(OrganizationUser organizationUser, EventType type)
        {
            var e = new EventMessage
            {
                OrganizationId = organizationUser.OrganizationId,
                UserId = organizationUser.UserId,
                OrganizationUserId = organizationUser.Id,
                Type = type,
                ActingUserId = _currentContext?.UserId,
                Date = DateTime.UtcNow
            };
            await _eventWriteService.CreateAsync(e);
        }

        public async Task LogOrganizationEventAsync(Organization organization, EventType type)
        {
            var e = new EventMessage
            {
                OrganizationId = organization.Id,
                Type = type,
                ActingUserId = _currentContext?.UserId,
                Date = DateTime.UtcNow
            };
            await _eventWriteService.CreateAsync(e);
        }
    }
}
