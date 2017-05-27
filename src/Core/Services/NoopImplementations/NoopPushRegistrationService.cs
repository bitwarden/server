using System;
using System.Threading.Tasks;
using Bit.Core.Models.Table;

namespace Bit.Core.Services
{
    public class NoopPushRegistrationService : IPushRegistrationService
    {
        public Task AddUserRegistrationOrganizationAsync(Guid userId, Guid organizationId)
        {
            return Task.FromResult(0);
        }

        public Task CreateOrUpdateRegistrationAsync(Device device)
        {
            return Task.FromResult(0);
        }

        public Task DeleteRegistrationAsync(Guid deviceId)
        {
            return Task.FromResult(0);
        }

        public Task DeleteUserRegistrationOrganizationAsync(Guid userId, Guid organizationId)
        {
            return Task.FromResult(0);
        }
    }
}
