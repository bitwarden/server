using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Models.Business.Provider;
using Bit.Core.Entities;
using Bit.Core.Models.Business;

namespace Bit.Core.AdminConsole.Services;

public interface IProviderService
{
    Task<Provider> CompleteSetupAsync(
        Provider provider,
        Guid ownerUserId,
        string token,
        string key,
        TaxInfo taxInfo = null
    );
    Task UpdateAsync(Provider provider, bool updateBilling = false);

    Task<List<ProviderUser>> InviteUserAsync(ProviderUserInvite<string> invite);
    Task<List<Tuple<ProviderUser, string>>> ResendInvitesAsync(ProviderUserInvite<Guid> invite);
    Task<ProviderUser> AcceptUserAsync(Guid providerUserId, User user, string token);
    Task<List<Tuple<ProviderUser, string>>> ConfirmUsersAsync(
        Guid providerId,
        Dictionary<Guid, string> keys,
        Guid confirmingUserId
    );

    Task SaveUserAsync(ProviderUser user, Guid savingUserId);
    Task<List<Tuple<ProviderUser, string>>> DeleteUsersAsync(
        Guid providerId,
        IEnumerable<Guid> providerUserIds,
        Guid deletingUserId
    );

    Task AddOrganization(Guid providerId, Guid organizationId, string key);
    Task AddOrganizationsToReseller(Guid providerId, IEnumerable<Guid> organizationIds);
    Task<ProviderOrganization> CreateOrganizationAsync(
        Guid providerId,
        OrganizationSignup organizationSignup,
        string clientOwnerEmail,
        User user
    );
    Task LogProviderAccessToOrganizationAsync(Guid organizationId);
    Task ResendProviderSetupInviteEmailAsync(Guid providerId, Guid ownerId);
    Task SendProviderSetupInviteEmailAsync(Provider provider, string ownerEmail);
    Task InitiateDeleteAsync(Provider provider, string providerAdminEmail);
    Task DeleteAsync(Provider provider, string token);
    Task DeleteAsync(Provider provider);
}
