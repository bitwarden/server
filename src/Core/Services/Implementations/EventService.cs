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
        private readonly IApplicationCacheService _applicationCacheService;
        private readonly CurrentContext _currentContext;
        private readonly GlobalSettings _globalSettings;

        public EventService(
            IEventWriteService eventWriteService,
            IOrganizationUserRepository organizationUserRepository,
            IApplicationCacheService applicationCacheService,
            CurrentContext currentContext,
            GlobalSettings globalSettings)
        {
            _eventWriteService = eventWriteService;
            _organizationUserRepository = organizationUserRepository;
            _applicationCacheService = applicationCacheService;
            _currentContext = currentContext;
            _globalSettings = globalSettings;
        }

        public async Task LogUserEventAsync(Guid userId, EventType type)
        {
            var now = DateTime.UtcNow;
            var events = new List<IEvent>
            {
                new EventMessage(_currentContext)
                {
                    UserId = userId,
                    ActingUserId = userId,
                    Type = type,
                    Date = now
                }
            };

            var orgAbilities = await _applicationCacheService.GetOrganizationAbilitiesAsync();
            IEnumerable<IEvent> orgEvents;
            if(_currentContext.UserId.HasValue)
            {
                orgEvents = _currentContext.Organizations
                    .Where(o => CanUseEvents(orgAbilities, o.Id))
                    .Select(o => new EventMessage(_currentContext)
                    {
                        OrganizationId = o.Id,
                        UserId = userId,
                        ActingUserId = userId,
                        Type = type,
                        Date = DateTime.UtcNow
                    });
            }
            else
            {
                var orgs = await _organizationUserRepository.GetManyByUserAsync(userId);
                orgEvents = orgs
                    .Where(o => o.Status == OrganizationUserStatusType.Confirmed &&
                        CanUseEvents(orgAbilities, o.OrganizationId))
                    .Select(o => new EventMessage(_currentContext)
                    {
                        OrganizationId = o.OrganizationId,
                        UserId = userId,
                        ActingUserId = userId,
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

            if(cipher.OrganizationId.HasValue)
            {
                var orgAbilities = await _applicationCacheService.GetOrganizationAbilitiesAsync();
                if(!CanUseEvents(orgAbilities, cipher.OrganizationId.Value))
                {
                    return;
                }
            }

            var e = new EventMessage(_currentContext)
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
            var orgAbilities = await _applicationCacheService.GetOrganizationAbilitiesAsync();
            if(!CanUseEvents(orgAbilities, collection.OrganizationId))
            {
                return;
            }

            var e = new EventMessage(_currentContext)
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
            var orgAbilities = await _applicationCacheService.GetOrganizationAbilitiesAsync();
            if(!CanUseEvents(orgAbilities, group.OrganizationId))
            {
                return;
            }

            var e = new EventMessage(_currentContext)
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
            var orgAbilities = await _applicationCacheService.GetOrganizationAbilitiesAsync();
            if(!CanUseEvents(orgAbilities, organizationUser.OrganizationId))
            {
                return;
            }

            var e = new EventMessage(_currentContext)
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
            if(!organization.Enabled || !organization.UseEvents)
            {
                return;
            }

            var e = new EventMessage(_currentContext)
            {
                OrganizationId = organization.Id,
                Type = type,
                ActingUserId = _currentContext?.UserId,
                Date = DateTime.UtcNow
            };
            await _eventWriteService.CreateAsync(e);
        }

        private bool CanUseEvents(IDictionary<Guid, OrganizationAbility> orgAbilities, Guid orgId)
        {
            return orgAbilities != null && orgAbilities.ContainsKey(orgId) &&
                orgAbilities[orgId].Enabled && orgAbilities[orgId].UseEvents;
        }
    }
}
