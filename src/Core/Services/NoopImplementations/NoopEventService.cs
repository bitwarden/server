using System;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Models.Table;

namespace Bit.Core.Services
{
    public class NoopEventService : IEventService
    {
        public Task LogCipherEventAsync(Cipher cipher, EventType type)
        {
            return Task.FromResult(0);
        }

        public Task LogCollectionEventAsync(Collection collection, EventType type)
        {
            return Task.FromResult(0);
        }

        public Task LogGroupEventAsync(Group group, EventType type)
        {
            return Task.FromResult(0);
        }

        public Task LogOrganizationEventAsync(Organization organization, EventType type)
        {
            return Task.FromResult(0);
        }

        public Task LogOrganizationUserEventAsync(OrganizationUser organizationUser, EventType type)
        {
            return Task.FromResult(0);
        }

        public Task LogUserEventAsync(Guid userId, EventType type)
        {
            return Task.FromResult(0);
        }
    }
}
