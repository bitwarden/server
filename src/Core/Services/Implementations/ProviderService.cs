using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Business.Provider;
using Bit.Core.Models.Table;
using Bit.Core.Models.Table.Provider;

namespace Bit.Core.Services
{
    public class ProviderService : IProviderService
    {

        public Task CreateAsync(Guid ownerUserId) => throw NotImplementedException();

        public Task CompleteSetup(Provider provider, Guid ownerUserId) => throw new NotImplementedException();

        public Task UpdateAsync(Provider provider, bool updateBilling = false) => throw new NotImplementedException();

        public Task<List<OrganizationUser>> InviteUserAsync(Guid providerId, Guid invitingUserId, ProviderUserInvite providerUserInvite) => throw new NotImplementedException();

        public Task ResendInvitesAsync(Guid providerId, Guid invitingUserId, IEnumerable<Guid> providerUsersId) => throw new NotImplementedException();

        public Task<OrganizationUser> AcceptUserAsync(string orgIdentifier, Guid acceptingUserId, string token) => throw new NotImplementedException();

        public Task<OrganizationUser> ConfirmUsersAsync(Guid providerId, Dictionary<Guid, string> keys, Guid confirmingUserId) => throw new NotImplementedException();

        public Task SaveUserAsync(ProviderUser user, Guid savingUserId) => throw new NotImplementedException();

        public Task UpdateUserAsync(ProviderUser user, Guid savingUserId) => throw new NotImplementedException();

        public Task DeleteUsersAsync(Guid providerId, IEnumerable<Guid> providerUserIds, Guid? deletingUserId) => throw new NotImplementedException();

        public Task AddOrganization(Guid providerId, Guid organizationId, Guid addingUserId, string key) => throw new NotImplementedException();

        public Task RemoveOrganization(Guid providerOrganizationId, Guid removingUserId) => throw new NotImplementedException();
    }
}
