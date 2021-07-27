using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Business;
using Bit.Core.Models.Business.Provider;
using Bit.Core.Models.Table;
using Bit.Core.Models.Table.Provider;

namespace Bit.Core.Services
{
    public class NoopProviderService : IProviderService
    {
        public Task CreateAsync(string ownerEmail) => throw new NotImplementedException();

        public Task<Provider> CompleteSetupAsync(Provider provider, Guid ownerUserId, string token, string key) => throw new NotImplementedException();

        public Task UpdateAsync(Provider provider, bool updateBilling = false) => throw new NotImplementedException();

        public Task<List<ProviderUser>> InviteUserAsync(Guid providerId, Guid invitingUserId, ProviderUserInvite providerUserInvite) => throw new NotImplementedException();

        public Task<List<Tuple<ProviderUser, string>>> ResendInvitesAsync(Guid providerId, Guid invitingUserId, IEnumerable<Guid> providerUsersId) => throw new NotImplementedException();

        public Task<ProviderUser> AcceptUserAsync(Guid providerUserId, User user, string token) => throw new NotImplementedException();

        public Task<List<Tuple<ProviderUser, string>>> ConfirmUsersAsync(Guid providerId, Dictionary<Guid, string> keys, Guid confirmingUserId) => throw new NotImplementedException();

        public Task SaveUserAsync(ProviderUser user, Guid savingUserId) => throw new NotImplementedException();

        public Task<List<Tuple<ProviderUser, string>>> DeleteUsersAsync(Guid providerId, IEnumerable<Guid> providerUserIds, Guid deletingUserId) => throw new NotImplementedException();

        public Task AddOrganization(Guid providerId, Guid organizationId, Guid addingUserId, string key) => throw new NotImplementedException();
        public Task<ProviderOrganization> CreateOrganizationAsync(Guid providerId, OrganizationSignup organizationSignup, User user) => throw new NotImplementedException();

        public Task RemoveOrganization(Guid providerId, Guid providerOrganizationId, Guid removingUserId) => throw new NotImplementedException();
    }
}
