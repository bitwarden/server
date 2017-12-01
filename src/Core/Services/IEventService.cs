using System;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Models.Table;

namespace Bit.Core.Services
{
    public interface IEventService
    {
        Task LogUserEventAsync(Guid userId, EventType type);
        Task LogCipherEventAsync(Cipher cipher, EventType type);
        Task LogCollectionEventAsync(Collection collection, EventType type);
        Task LogGroupEventAsync(Group group, EventType type);
        Task LogOrganizationUserEventAsync(OrganizationUser organizationUser, EventType type);
        Task LogOrganizationEventAsync(Organization organization, EventType type);
    }
}
