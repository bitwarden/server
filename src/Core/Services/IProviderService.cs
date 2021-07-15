using System.Threading.Tasks;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using System;
using Bit.Core.Models.Business;
using Bit.Core.Models.Business.Provider;
using Bit.Core.Models.Table.Provider;

namespace Bit.Core.Services
{
    public interface IProviderService
    {
        Task CreateAsync(string ownerEmail);
        Task<Provider> CompleteSetupAsync(Provider provider, Guid ownerUserId, string token, string key);
        Task UpdateAsync(Provider provider, bool updateBilling = false);

        Task<List<ProviderUser>> InviteUserAsync(Guid providerId, Guid invitingUserId, ProviderUserInvite providerUserInvite);
        Task<List<Tuple<ProviderUser, string>>> ResendInvitesAsync(Guid providerId, Guid invitingUserId,
            IEnumerable<Guid> providerUsersId);
        Task<ProviderUser> AcceptUserAsync(Guid providerUserId, User user, string token);
        Task<List<Tuple<ProviderUser, string>>> ConfirmUsersAsync(Guid providerId, Dictionary<Guid, string> keys, Guid confirmingUserId);

        Task SaveUserAsync(ProviderUser user, Guid savingUserId);
        Task<List<Tuple<ProviderUser, string>>> DeleteUsersAsync(Guid providerId, IEnumerable<Guid> providerUserIds,
            Guid deletingUserId);

        Task AddOrganization(Guid providerId, Guid organizationId, Guid addingUserId, string key);
        Task<ProviderOrganization> CreateOrganizationAsync(Guid providerId, OrganizationSignup organizationSignup, User user);
        Task RemoveOrganization(Guid providerId, Guid providerOrganizationId, Guid removingUserId);
    }
}
