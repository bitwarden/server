using System.Threading.Tasks;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using System;
using Bit.Core.Models.Business.Provider;
using Bit.Core.Models.Table.Provider;

namespace Bit.Core.Services
{
    public interface IProviderService
    {
        Task CreateAsync(Guid ownerUserId);
        Task CompleteSetupAsync(Provider provider, Guid ownerUserId, string token, string key);
        Task UpdateAsync(Provider provider, bool updateBilling = false);

        Task<List<ProviderUser>> InviteUserAsync(Guid providerId, Guid invitingUserId, ProviderUserInvite providerUserInvite);
        Task<List<Tuple<ProviderUser, string>>> ResendInvitesAsync(Guid providerId, Guid invitingUserId,
            IEnumerable<Guid> providerUsersId);
        Task<ProviderUser> AcceptUserAsync(string orgIdentifier, Guid acceptingUserId, string token);
        Task<ProviderUser> ConfirmUsersAsync(Guid providerId, Dictionary<Guid, string> keys, Guid confirmingUserId);

        Task SaveUserAsync(ProviderUser user, Guid savingUserId);
        Task UpdateUserAsync(ProviderUser user, Guid savingUserId);
        Task DeleteUsersAsync(Guid providerId, IEnumerable<Guid> providerUserIds, Guid? deletingUserId);

        Task AddOrganization(Guid providerId, Guid organizationId, Guid addingUserId, string key);
        Task RemoveOrganization(Guid providerOrganizationId, Guid removingUserId);
        
        // TODO: Figure out how ProviderOrganizationProviderUsers should be managed
    }
}
