using Bit.Core.Entities;
using Bit.Core.Entities.Provider;
using Bit.Core.Models.Business;
using Bit.Core.Models.Business.Provider;

namespace Bit.Core.Services;

public interface IProviderService
{
    Task CreateAsync(string ownerEmail);
    Task<Provider> CompleteSetupAsync(Provider provider, Guid ownerUserId, string token, string key);
    Task UpdateAsync(Provider provider, bool updateBilling = false);

    Task<List<ProviderUser>> InviteUserAsync(ProviderUserInvite<string> invite);
    Task<List<Tuple<ProviderUser, string>>> ResendInvitesAsync(ProviderUserInvite<Guid> invite);
    Task<ProviderUser> AcceptUserAsync(Guid providerUserId, User user, string token);
    Task<List<Tuple<ProviderUser, string>>> ConfirmUsersAsync(Guid providerId, Dictionary<Guid, string> keys, Guid confirmingUserId);

    Task SaveUserAsync(ProviderUser user, Guid savingUserId);
    Task<List<Tuple<ProviderUser, string>>> DeleteUsersAsync(Guid providerId, IEnumerable<Guid> providerUserIds,
        Guid deletingUserId);

    Task AddOrganization(Guid providerId, Guid organizationId, Guid addingUserId, string key);
    Task<ProviderOrganization> CreateOrganizationAsync(Guid providerId, OrganizationSignup organizationSignup,
        string clientOwnerEmail, User user);
    Task RemoveOrganizationAsync(Guid providerId, Guid providerOrganizationId, Guid removingUserId);
    Task LogProviderAccessToOrganizationAsync(Guid organizationId);
    Task ResendProviderSetupInviteEmailAsync(Guid providerId, Guid ownerId);
}

