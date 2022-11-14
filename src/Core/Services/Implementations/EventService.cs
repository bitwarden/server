using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Entities.Provider;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Core.Settings;

namespace Bit.Core.Services;

public class EventService : IEventService
{
    private readonly IEventWriteService _eventWriteService;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IProviderUserRepository _providerUserRepository;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly ICurrentContext _currentContext;
    private readonly GlobalSettings _globalSettings;

    public EventService(
        IEventWriteService eventWriteService,
        IOrganizationUserRepository organizationUserRepository,
        IProviderUserRepository providerUserRepository,
        IApplicationCacheService applicationCacheService,
        ICurrentContext currentContext,
        GlobalSettings globalSettings)
    {
        _eventWriteService = eventWriteService;
        _organizationUserRepository = organizationUserRepository;
        _providerUserRepository = providerUserRepository;
        _applicationCacheService = applicationCacheService;
        _currentContext = currentContext;
        _globalSettings = globalSettings;
    }

    public async Task LogUserEventAsync(Guid userId, EventType type, DateTime? date = null)
    {
        var events = new List<IEvent>
        {
            new EventMessage(_currentContext)
            {
                UserId = userId,
                ActingUserId = userId,
                Type = type,
                Date = date.GetValueOrDefault(DateTime.UtcNow)
            }
        };

        var orgAbilities = await _applicationCacheService.GetOrganizationAbilitiesAsync();
        var orgs = await _currentContext.OrganizationMembershipAsync(_organizationUserRepository, userId);
        var orgEvents = orgs.Where(o => CanUseEvents(orgAbilities, o.Id))
            .Select(o => new EventMessage(_currentContext)
            {
                OrganizationId = o.Id,
                UserId = userId,
                ActingUserId = userId,
                Type = type,
                Date = DateTime.UtcNow
            });

        var providerAbilities = await _applicationCacheService.GetProviderAbilitiesAsync();
        var providers = await _currentContext.ProviderMembershipAsync(_providerUserRepository, userId);
        var providerEvents = providers.Where(o => CanUseProviderEvents(providerAbilities, o.Id))
            .Select(p => new EventMessage(_currentContext)
            {
                ProviderId = p.Id,
                UserId = userId,
                ActingUserId = userId,
                Type = type,
                Date = DateTime.UtcNow
            });

        if (orgEvents.Any() || providerEvents.Any())
        {
            events.AddRange(orgEvents);
            events.AddRange(providerEvents);
            await _eventWriteService.CreateManyAsync(events);
        }
        else
        {
            await _eventWriteService.CreateAsync(events.First());
        }
    }

    public async Task LogCipherEventAsync(Cipher cipher, EventType type, DateTime? date = null)
    {
        var e = await BuildCipherEventMessageAsync(cipher, type, date);
        if (e != null)
        {
            await _eventWriteService.CreateAsync(e);
        }
    }

    public async Task LogCipherEventsAsync(IEnumerable<Tuple<Cipher, EventType, DateTime?>> events)
    {
        var cipherEvents = new List<IEvent>();
        foreach (var ev in events)
        {
            var e = await BuildCipherEventMessageAsync(ev.Item1, ev.Item2, ev.Item3);
            if (e != null)
            {
                cipherEvents.Add(e);
            }
        }
        await _eventWriteService.CreateManyAsync(cipherEvents);
    }

    private async Task<EventMessage> BuildCipherEventMessageAsync(Cipher cipher, EventType type, DateTime? date = null)
    {
        // Only logging organization cipher events for now.
        if (!cipher.OrganizationId.HasValue || (!_currentContext?.UserId.HasValue ?? true))
        {
            return null;
        }

        if (cipher.OrganizationId.HasValue)
        {
            var orgAbilities = await _applicationCacheService.GetOrganizationAbilitiesAsync();
            if (!CanUseEvents(orgAbilities, cipher.OrganizationId.Value))
            {
                return null;
            }
        }

        return new EventMessage(_currentContext)
        {
            OrganizationId = cipher.OrganizationId,
            UserId = cipher.OrganizationId.HasValue ? null : cipher.UserId,
            CipherId = cipher.Id,
            Type = type,
            ActingUserId = _currentContext?.UserId,
            ProviderId = await GetProviderIdAsync(cipher.OrganizationId),
            Date = date.GetValueOrDefault(DateTime.UtcNow)
        };
    }

    public async Task LogCollectionEventAsync(Collection collection, EventType type, DateTime? date = null)
    {
        var orgAbilities = await _applicationCacheService.GetOrganizationAbilitiesAsync();
        if (!CanUseEvents(orgAbilities, collection.OrganizationId))
        {
            return;
        }

        var e = new EventMessage(_currentContext)
        {
            OrganizationId = collection.OrganizationId,
            CollectionId = collection.Id,
            Type = type,
            ActingUserId = _currentContext?.UserId,
            ProviderId = await GetProviderIdAsync(collection.OrganizationId),
            Date = date.GetValueOrDefault(DateTime.UtcNow)
        };
        await _eventWriteService.CreateAsync(e);
    }

    public async Task LogGroupEventAsync(Group group, EventType type,
        DateTime? date = null)
    {
        await CreateLogGroupEventAsync(group, type, systemUser: null, date);
    }

    public async Task LogGroupEventAsync(Group group, EventType type, EventSystemUser systemUser,
        DateTime? date = null)
    {
        await CreateLogGroupEventAsync(group, type, systemUser, date);
    }

    private async Task CreateLogGroupEventAsync(Group group, EventType type, EventSystemUser? systemUser, DateTime? date = null)
    {
        var orgAbilities = await _applicationCacheService.GetOrganizationAbilitiesAsync();
        if (!CanUseEvents(orgAbilities, group.OrganizationId))
        {
            return;
        }

        var e = new EventMessage(_currentContext)
        {
            OrganizationId = group.OrganizationId,
            GroupId = group.Id,
            Type = type,
            ActingUserId = _currentContext?.UserId,
            ProviderId = await GetProviderIdAsync(@group.OrganizationId),
            Date = date.GetValueOrDefault(DateTime.UtcNow),
            SystemUser = systemUser
        };
        await _eventWriteService.CreateAsync(e);
    }

    public async Task LogPolicyEventAsync(Policy policy, EventType type, DateTime? date = null)
    {
        var orgAbilities = await _applicationCacheService.GetOrganizationAbilitiesAsync();
        if (!CanUseEvents(orgAbilities, policy.OrganizationId))
        {
            return;
        }

        var e = new EventMessage(_currentContext)
        {
            OrganizationId = policy.OrganizationId,
            PolicyId = policy.Id,
            Type = type,
            ActingUserId = _currentContext?.UserId,
            ProviderId = await GetProviderIdAsync(policy.OrganizationId),
            Date = date.GetValueOrDefault(DateTime.UtcNow)
        };
        await _eventWriteService.CreateAsync(e);
    }

    public async Task LogOrganizationUserEventAsync(OrganizationUser organizationUser, EventType type,
        DateTime? date = null) =>
        await CreateLogOrganizationUserEventsAsync(new (OrganizationUser, EventType, EventSystemUser?, DateTime?)[] { (organizationUser, type, null, date) });

    public async Task LogOrganizationUserEventAsync(OrganizationUser organizationUser, EventType type,
        EventSystemUser systemUser, DateTime? date = null) =>
        await CreateLogOrganizationUserEventsAsync(new (OrganizationUser, EventType, EventSystemUser?, DateTime?)[] { (organizationUser, type, systemUser, date) });

    public async Task LogOrganizationUserEventsAsync(
        IEnumerable<(OrganizationUser, EventType, DateTime?)> events)
    {
        await CreateLogOrganizationUserEventsAsync(events.Select(e => (e.Item1, e.Item2, (EventSystemUser?)null, e.Item3)));
    }

    public async Task LogOrganizationUserEventsAsync(
        IEnumerable<(OrganizationUser, EventType, EventSystemUser, DateTime?)> events)
    {
        await CreateLogOrganizationUserEventsAsync(events.Select(e => (e.Item1, e.Item2, (EventSystemUser?)e.Item3, e.Item4)));
    }

    private async Task CreateLogOrganizationUserEventsAsync(IEnumerable<(OrganizationUser, EventType, EventSystemUser?, DateTime?)> events)
    {
        var orgAbilities = await _applicationCacheService.GetOrganizationAbilitiesAsync();
        var eventMessages = new List<IEvent>();
        foreach (var (organizationUser, type, systemUser, date) in events)
        {
            if (!CanUseEvents(orgAbilities, organizationUser.OrganizationId))
            {
                continue;
            }

            eventMessages.Add(new EventMessage(_currentContext)
            {
                OrganizationId = organizationUser.OrganizationId,
                UserId = organizationUser.UserId,
                OrganizationUserId = organizationUser.Id,
                ProviderId = await GetProviderIdAsync(organizationUser.OrganizationId),
                Type = type,
                ActingUserId = _currentContext?.UserId,
                Date = date.GetValueOrDefault(DateTime.UtcNow),
                SystemUser = systemUser
            });
        }

        await _eventWriteService.CreateManyAsync(eventMessages);
    }

    public async Task LogOrganizationEventAsync(Organization organization, EventType type, DateTime? date = null)
    {
        if (!organization.Enabled || !organization.UseEvents)
        {
            return;
        }

        var e = new EventMessage(_currentContext)
        {
            OrganizationId = organization.Id,
            ProviderId = await GetProviderIdAsync(organization.Id),
            Type = type,
            ActingUserId = _currentContext?.UserId,
            Date = date.GetValueOrDefault(DateTime.UtcNow),
            InstallationId = GetInstallationId(),
        };
        await _eventWriteService.CreateAsync(e);
    }

    public async Task LogProviderUserEventAsync(ProviderUser providerUser, EventType type, DateTime? date = null)
    {
        await LogProviderUsersEventAsync(new[] { (providerUser, type, date) });
    }

    public async Task LogProviderUsersEventAsync(IEnumerable<(ProviderUser, EventType, DateTime?)> events)
    {
        var providerAbilities = await _applicationCacheService.GetProviderAbilitiesAsync();
        var eventMessages = new List<IEvent>();
        foreach (var (providerUser, type, date) in events)
        {
            if (!CanUseProviderEvents(providerAbilities, providerUser.ProviderId))
            {
                continue;
            }
            eventMessages.Add(new EventMessage(_currentContext)
            {
                ProviderId = providerUser.ProviderId,
                UserId = providerUser.UserId,
                ProviderUserId = providerUser.Id,
                Type = type,
                ActingUserId = _currentContext?.UserId,
                Date = date.GetValueOrDefault(DateTime.UtcNow)
            });
        }

        await _eventWriteService.CreateManyAsync(eventMessages);
    }

    public async Task LogProviderOrganizationEventAsync(ProviderOrganization providerOrganization, EventType type,
        DateTime? date = null)
    {
        var providerAbilities = await _applicationCacheService.GetProviderAbilitiesAsync();
        if (!CanUseProviderEvents(providerAbilities, providerOrganization.ProviderId))
        {
            return;
        }

        var e = new EventMessage(_currentContext)
        {
            ProviderId = providerOrganization.ProviderId,
            ProviderOrganizationId = providerOrganization.Id,
            Type = type,
            ActingUserId = _currentContext?.UserId,
            Date = date.GetValueOrDefault(DateTime.UtcNow)
        };
        await _eventWriteService.CreateAsync(e);
    }

    private async Task<Guid?> GetProviderIdAsync(Guid? orgId)
    {
        if (_currentContext == null || !orgId.HasValue)
        {
            return null;
        }

        return await _currentContext.ProviderIdForOrg(orgId.Value);
    }

    private Guid? GetInstallationId()
    {
        if (_currentContext == null)
        {
            return null;
        }

        return _currentContext.InstallationId;
    }

    private bool CanUseEvents(IDictionary<Guid, OrganizationAbility> orgAbilities, Guid orgId)
    {
        return orgAbilities != null && orgAbilities.ContainsKey(orgId) &&
            orgAbilities[orgId].Enabled && orgAbilities[orgId].UseEvents;
    }

    private bool CanUseProviderEvents(IDictionary<Guid, ProviderAbility> providerAbilities, Guid providerId)
    {
        return providerAbilities != null && providerAbilities.ContainsKey(providerId) &&
               providerAbilities[providerId].Enabled && providerAbilities[providerId].UseEvents;
    }
}
