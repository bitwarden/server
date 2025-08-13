// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Interfaces;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Identity;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.Settings;
using Bit.Core.Vault.Entities;

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

    public async Task LogCollectionEventAsync(Collection collection, EventType type, DateTime? date = null) =>
        await LogCollectionEventsAsync(new[] { (collection, type, date) });


    public async Task LogCollectionEventsAsync(IEnumerable<(Collection collection, EventType type, DateTime? date)> events)
    {
        var orgAbilities = await _applicationCacheService.GetOrganizationAbilitiesAsync();
        var eventMessages = new List<IEvent>();
        foreach (var (collection, type, date) in events)
        {
            if (!CanUseEvents(orgAbilities, collection.OrganizationId))
            {
                continue;
            }

            eventMessages.Add(new EventMessage(_currentContext)
            {
                OrganizationId = collection.OrganizationId,
                CollectionId = collection.Id,
                Type = type,
                ActingUserId = _currentContext?.UserId,
                ProviderId = await GetProviderIdAsync(collection.OrganizationId),
                Date = date.GetValueOrDefault(DateTime.UtcNow)
            });
        }

        await _eventWriteService.CreateManyAsync(eventMessages);
    }

    public async Task LogGroupEventAsync(Group group, EventType type, DateTime? date = null) =>
        await LogGroupEventsAsync(new[] { (group, type, (EventSystemUser?)null, date) });

    public async Task LogGroupEventAsync(Group group, EventType type, EventSystemUser systemUser, DateTime? date = null) =>
        await LogGroupEventsAsync(new[] { (group, type, (EventSystemUser?)systemUser, date) });

    public async Task LogGroupEventsAsync(IEnumerable<(Group group, EventType type, EventSystemUser? systemUser, DateTime? date)> events)
    {
        var orgAbilities = await _applicationCacheService.GetOrganizationAbilitiesAsync();
        var eventMessages = new List<IEvent>();
        foreach (var (group, type, systemUser, date) in events)
        {
            if (!CanUseEvents(orgAbilities, group.OrganizationId))
            {
                continue;
            }

            var e = new EventMessage(_currentContext)
            {
                OrganizationId = group.OrganizationId,
                GroupId = group.Id,
                Type = type,
                ActingUserId = _currentContext?.UserId,
                ProviderId = await GetProviderIdAsync(group.OrganizationId),
                SystemUser = systemUser,
                Date = date.GetValueOrDefault(DateTime.UtcNow)
            };

            if (systemUser is EventSystemUser.SCIM)
            {
                // System user only used for SCIM logs in this method
                // and we want event logs to report server instead of unknown
                e.DeviceType = DeviceType.Server;
            }

            eventMessages.Add(e);
        }
        await _eventWriteService.CreateManyAsync(eventMessages);
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

    public async Task LogOrganizationUserEventAsync<T>(T organizationUser, EventType type,
        DateTime? date = null) where T : IOrganizationUser =>
        await CreateLogOrganizationUserEventsAsync(new (T, EventType, EventSystemUser?, DateTime?)[] { (organizationUser, type, null, date) });

    public async Task LogOrganizationUserEventAsync<T>(T organizationUser, EventType type,
        EventSystemUser systemUser, DateTime? date = null) where T : IOrganizationUser =>
        await CreateLogOrganizationUserEventsAsync(new (T, EventType, EventSystemUser?, DateTime?)[] { (organizationUser, type, systemUser, date) });

    public async Task LogOrganizationUserEventsAsync<T>(
        IEnumerable<(T, EventType, DateTime?)> events) where T : IOrganizationUser
    {
        await CreateLogOrganizationUserEventsAsync(events.Select(e => (e.Item1, e.Item2, (EventSystemUser?)null, e.Item3)));
    }

    public async Task LogOrganizationUserEventsAsync<T>(
        IEnumerable<(T, EventType, EventSystemUser, DateTime?)> events) where T : IOrganizationUser
    {
        await CreateLogOrganizationUserEventsAsync(events.Select(e => (e.Item1, e.Item2, (EventSystemUser?)e.Item3, e.Item4)));
    }

    private async Task CreateLogOrganizationUserEventsAsync<T>(IEnumerable<(T, EventType, EventSystemUser?, DateTime?)> events) where T : IOrganizationUser
    {
        var orgAbilities = await _applicationCacheService.GetOrganizationAbilitiesAsync();
        var eventMessages = new List<IEvent>();
        foreach (var (organizationUser, type, systemUser, date) in events)
        {
            if (!CanUseEvents(orgAbilities, organizationUser.OrganizationId))
            {
                continue;
            }

            var e = new EventMessage(_currentContext)
            {
                OrganizationId = organizationUser.OrganizationId,
                UserId = organizationUser.UserId,
                OrganizationUserId = organizationUser.Id,
                ProviderId = await GetProviderIdAsync(organizationUser.OrganizationId),
                Type = type,
                ActingUserId = _currentContext?.UserId,
                Date = date.GetValueOrDefault(DateTime.UtcNow),
                SystemUser = systemUser
            };

            if (systemUser is EventSystemUser.SCIM)
            {
                // System user only used for SCIM logs in this method
                // and we want event logs to report server instead of unknown
                e.DeviceType = DeviceType.Server;
            }

            eventMessages.Add(e);
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
        await LogProviderOrganizationEventsAsync(new[] { (providerOrganization, type, date) });
    }

    public async Task LogProviderOrganizationEventsAsync(IEnumerable<(ProviderOrganization, EventType, DateTime?)> events)
    {
        var providerAbilities = await _applicationCacheService.GetProviderAbilitiesAsync();
        var eventMessages = new List<IEvent>();
        foreach (var (providerOrganization, type, date) in events)
        {
            if (!CanUseProviderEvents(providerAbilities, providerOrganization.ProviderId))
            {
                continue;
            }

            var e = new EventMessage(_currentContext)
            {
                ProviderId = providerOrganization.ProviderId,
                ProviderOrganizationId = providerOrganization.Id,
                Type = type,
                ActingUserId = _currentContext?.UserId,
                Date = date.GetValueOrDefault(DateTime.UtcNow)
            };

            eventMessages.Add(e);
        }

        await _eventWriteService.CreateManyAsync(eventMessages);
    }

    public async Task LogOrganizationDomainEventAsync(OrganizationDomain organizationDomain, EventType type,
            DateTime? date = null)
    {
        var orgAbilities = await _applicationCacheService.GetOrganizationAbilitiesAsync();
        if (!CanUseEvents(orgAbilities, organizationDomain.OrganizationId))
        {
            return;
        }

        var e = new EventMessage(_currentContext)
        {
            OrganizationId = organizationDomain.OrganizationId,
            Type = type,
            ActingUserId = _currentContext?.UserId,
            DomainName = organizationDomain.DomainName,
            Date = date.GetValueOrDefault(DateTime.UtcNow)
        };
        await _eventWriteService.CreateAsync(e);
    }

    public async Task LogOrganizationDomainEventAsync(OrganizationDomain organizationDomain, EventType type,
        EventSystemUser systemUser,
        DateTime? date = null)
    {
        var orgAbilities = await _applicationCacheService.GetOrganizationAbilitiesAsync();
        if (!CanUseEvents(orgAbilities, organizationDomain.OrganizationId))
        {
            return;
        }

        var e = new EventMessage(_currentContext)
        {
            OrganizationId = organizationDomain.OrganizationId,
            Type = type,
            ActingUserId = _currentContext?.UserId,
            DomainName = organizationDomain.DomainName,
            SystemUser = systemUser,
            Date = date.GetValueOrDefault(DateTime.UtcNow),
            DeviceType = DeviceType.Server
        };
        await _eventWriteService.CreateAsync(e);
    }

    public async Task LogUserSecretsEventAsync(Guid userId, IEnumerable<Secret> secrets, EventType type, DateTime? date = null)
    {
        var orgAbilities = await _applicationCacheService.GetOrganizationAbilitiesAsync();
        var eventMessages = new List<IEvent>();

        foreach (var secret in secrets)
        {
            if (!CanUseEvents(orgAbilities, secret.OrganizationId))
            {
                continue;
            }

            var e = new EventMessage(_currentContext)
            {
                OrganizationId = secret.OrganizationId,
                Type = type,
                SecretId = secret.Id,
                UserId = userId,
                Date = date.GetValueOrDefault(DateTime.UtcNow)
            };
            eventMessages.Add(e);
        }

        await _eventWriteService.CreateManyAsync(eventMessages);
    }

    public async Task LogServiceAccountSecretsEventAsync(Guid serviceAccountId, IEnumerable<Secret> secrets, EventType type, DateTime? date = null)
    {
        var orgAbilities = await _applicationCacheService.GetOrganizationAbilitiesAsync();
        var eventMessages = new List<IEvent>();

        foreach (var secret in secrets)
        {
            if (!CanUseEvents(orgAbilities, secret.OrganizationId))
            {
                continue;
            }

            var e = new EventMessage(_currentContext)
            {
                OrganizationId = secret.OrganizationId,
                Type = type,
                SecretId = secret.Id,
                ServiceAccountId = serviceAccountId,
                Date = date.GetValueOrDefault(DateTime.UtcNow)
            };
            eventMessages.Add(e);
        }

        await _eventWriteService.CreateManyAsync(eventMessages);
    }

    public async Task LogUserProjectsEventAsync(Guid userId, IEnumerable<Project> projects, EventType type, DateTime? date = null)
    {
        var orgAbilities = await _applicationCacheService.GetOrganizationAbilitiesAsync();
        var eventMessages = new List<IEvent>();

        foreach (var project in projects)
        {
            if (!CanUseEvents(orgAbilities, project.OrganizationId))
            {
                continue;
            }

            var e = new EventMessage(_currentContext)
            {
                OrganizationId = project.OrganizationId,
                Type = type,
                ProjectId = project.Id,
                UserId = userId,
                Date = date.GetValueOrDefault(DateTime.UtcNow)
            };
            eventMessages.Add(e);
        }

        await _eventWriteService.CreateManyAsync(eventMessages);
    }

    public async Task LogServiceAccountProjectsEventAsync(Guid serviceAccountId, IEnumerable<Project> projects, EventType type, DateTime? date = null)
    {
        var orgAbilities = await _applicationCacheService.GetOrganizationAbilitiesAsync();
        var eventMessages = new List<IEvent>();

        foreach (var project in projects)
        {
            if (!CanUseEvents(orgAbilities, project.OrganizationId))
            {
                continue;
            }

            var e = new EventMessage(_currentContext)
            {
                OrganizationId = project.OrganizationId,
                Type = type,
                ProjectId = project.Id,
                ServiceAccountId = serviceAccountId,
                Date = date.GetValueOrDefault(DateTime.UtcNow)
            };
            eventMessages.Add(e);
        }

        await _eventWriteService.CreateManyAsync(eventMessages);
    }

    public async Task LogServiceAccountPeopleEventAsync(Guid userId, UserServiceAccountAccessPolicy policy, EventType type, IdentityClientType identityClientType, DateTime? date = null)
    {
        var orgAbilities = await _applicationCacheService.GetOrganizationAbilitiesAsync();
        var eventMessages = new List<IEvent>();
        var orgUser = await _organizationUserRepository.GetByIdAsync((Guid)policy.OrganizationUserId);

        if (!CanUseEvents(orgAbilities, orgUser.OrganizationId))
        {
            throw new NotFoundException();
        }

        var (actingUserId, serviceAccountId) = MapIdentityClientType(userId, identityClientType);

        if (actingUserId is null && serviceAccountId is null)
        {
            return;
        }

        if (policy.OrganizationUserId != null)
        {
            var e = new EventMessage(_currentContext)
            {
                OrganizationId = orgUser.OrganizationId,
                Type = type,
                GrantedServiceAccountId = policy.GrantedServiceAccountId,
                ServiceAccountId = serviceAccountId,
                UserId = policy.OrganizationUserId,
                ActingUserId = actingUserId,
                Date = date.GetValueOrDefault(DateTime.UtcNow)
            };
            eventMessages.Add(e);

            await _eventWriteService.CreateManyAsync(eventMessages);
        }
        else
        {
            throw new NotFoundException();
        }
    }

    public async Task LogServiceAccountGroupEventAsync(Guid userId, GroupServiceAccountAccessPolicy policy, EventType type, IdentityClientType identityClientType, DateTime? date = null)
    {
        var orgAbilities = await _applicationCacheService.GetOrganizationAbilitiesAsync();
        var eventMessages = new List<IEvent>();

        if (!CanUseEvents(orgAbilities, policy.Group.OrganizationId))
        {
            throw new NotFoundException();
        }

        var (actingUserId, serviceAccountId) = MapIdentityClientType(userId, identityClientType);

        if (actingUserId is null && serviceAccountId is null)
        {
            return;
        }

        if (policy.GroupId != null)
        {
            var e = new EventMessage(_currentContext)
            {
                OrganizationId = policy.Group.OrganizationId,
                Type = type,
                GrantedServiceAccountId = policy.GrantedServiceAccountId,
                ServiceAccountId = serviceAccountId,
                GroupId = policy.GroupId,
                ActingUserId = actingUserId,
                Date = date.GetValueOrDefault(DateTime.UtcNow)
            };
            eventMessages.Add(e);

            await _eventWriteService.CreateManyAsync(eventMessages);
        }
        else
        {
            throw new NotFoundException();
        }
    }

    public async Task LogServiceAccountEventAsync(Guid userId, List<ServiceAccount> serviceAccounts, EventType type, IdentityClientType identityClientType, DateTime? date = null)
    {
        var orgAbilities = await _applicationCacheService.GetOrganizationAbilitiesAsync();
        var eventMessages = new List<IEvent>();

        foreach (var serviceAccount in serviceAccounts)
        {
            if (!CanUseEvents(orgAbilities, serviceAccount.OrganizationId))
            {
                return;
            }

            var (actingUserId, serviceAccountId) = MapIdentityClientType(userId, identityClientType);

            if (actingUserId is null && serviceAccountId is null)
            {
                return;
            }

            if (serviceAccount != null)
            {
                var e = new EventMessage(_currentContext)
                {
                    OrganizationId = serviceAccount.OrganizationId,
                    Type = type,
                    GrantedServiceAccountId = serviceAccount.Id,
                    ServiceAccountId = serviceAccountId,
                    ActingUserId = actingUserId,
                    Date = date.GetValueOrDefault(DateTime.UtcNow) //TODO do we need userID here?
                };
                eventMessages.Add(e);

                await _eventWriteService.CreateManyAsync(eventMessages);
            }
            else
            {
                throw new NotFoundException();
            }
        }
    }

    private (Guid? actingUserId, Guid? serviceAccountId) MapIdentityClientType(
           Guid userId, IdentityClientType identityClientType)
    {
        if (identityClientType == IdentityClientType.Organization)
        {
            return (null, null);
        }

        return identityClientType switch
        {
            IdentityClientType.User => (userId, null),
            IdentityClientType.ServiceAccount => (null, userId),
            _ => throw new InvalidOperationException("Unknown identity client type.")
        };
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
        return orgAbilities != null && orgAbilities.TryGetValue(orgId, out var orgAbility) &&
               orgAbility.Enabled && orgAbility.UseEvents;
    }

    private bool CanUseProviderEvents(IDictionary<Guid, ProviderAbility> providerAbilities, Guid providerId)
    {
        return providerAbilities != null && providerAbilities.TryGetValue(providerId, out var providerAbility) &&
               providerAbility.Enabled && providerAbility.UseEvents;
    }
}
