using System;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Models.Table;

namespace Bit.Core.Services
{
    public class NoopEventService : IEventService
    {
        public Task LogCipherEventAsync(Cipher cipher, EventType type, DateTime? date = null)
        {
            return Task.FromResult(0);
        }

        public Task LogCollectionEventAsync(Collection collection, EventType type, DateTime? date = null)
        {
            return Task.FromResult(0);
        }

        public Task LogGroupEventAsync(Group group, EventType type, DateTime? date = null)
        {
            return Task.FromResult(0);
        }

        public Task LogOrganizationEventAsync(Organization organization, EventType type, DateTime? date = null)
        {
            return Task.FromResult(0);
        }

        public Task LogOrganizationUserEventAsync(OrganizationUser organizationUser, EventType type,
            DateTime? date = null)
        {
            return Task.FromResult(0);
        }

        public Task LogUserEventAsync(Guid userId, EventType type, DateTime? date = null)
        {
            return Task.FromResult(0);
        }
    }
}
