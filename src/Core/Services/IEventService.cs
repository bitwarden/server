using Bit.Core.Entities;
using Bit.Core.Entities.Provider;
using Bit.Core.Enums;

namespace Bit.Core.Services;

public interface IEventService
{
    Task LogUserEventAsync(Guid userId, EventType type, DateTime? date = null);
    Task LogCipherEventAsync(Cipher cipher, EventType type, DateTime? date = null);
    Task LogCipherEventsAsync(IEnumerable<Tuple<Cipher, EventType, DateTime?>> events);
    Task LogCollectionEventAsync(Collection collection, EventType type, DateTime? date = null);
    Task LogGroupEventAsync(Group group, EventType type, DateTime? date = null);
    Task LogGroupEventAsync(Group group, EventType type, EventSystemUser systemUser, DateTime? date = null);
    Task LogPolicyEventAsync(Policy policy, EventType type, DateTime? date = null);
    Task LogOrganizationUserEventAsync(OrganizationUser organizationUser, EventType type, DateTime? date = null);
    Task LogOrganizationUserEventAsync(OrganizationUser organizationUser, EventType type, EventSystemUser systemUser, DateTime? date = null);
    Task LogOrganizationUserEventsAsync(IEnumerable<(OrganizationUser, EventType, DateTime?)> events);
    Task LogOrganizationUserEventsAsync(IEnumerable<(OrganizationUser, EventType, EventSystemUser, DateTime?)> events);
    Task LogOrganizationEventAsync(Organization organization, EventType type, DateTime? date = null);
    Task LogProviderUserEventAsync(ProviderUser providerUser, EventType type, DateTime? date = null);
    Task LogProviderUsersEventAsync(IEnumerable<(ProviderUser, EventType, DateTime?)> events);
    Task LogProviderOrganizationEventAsync(ProviderOrganization providerOrganization, EventType type, DateTime? date = null);
}
