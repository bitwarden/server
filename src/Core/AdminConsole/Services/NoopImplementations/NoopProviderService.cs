﻿using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Models.Business.Provider;
using Bit.Core.Billing.Models;
using Bit.Core.Entities;
using Bit.Core.Models.Business;

namespace Bit.Core.AdminConsole.Services.NoopImplementations;

public class NoopProviderService : IProviderService
{
    public Task<Provider> CompleteSetupAsync(Provider provider, Guid ownerUserId, string token, string key, TaxInfo taxInfo, TokenizedPaymentSource tokenizedPaymentSource = null) => throw new NotImplementedException();

    public Task UpdateAsync(Provider provider, bool updateBilling = false) => throw new NotImplementedException();

    public Task<List<ProviderUser>> InviteUserAsync(ProviderUserInvite<string> invite) => throw new NotImplementedException();

    public Task<List<Tuple<ProviderUser, string>>> ResendInvitesAsync(ProviderUserInvite<Guid> invite) => throw new NotImplementedException();

    public Task<ProviderUser> AcceptUserAsync(Guid providerUserId, User user, string token) => throw new NotImplementedException();

    public Task<List<Tuple<ProviderUser, string>>> ConfirmUsersAsync(Guid providerId, Dictionary<Guid, string> keys, Guid confirmingUserId) => throw new NotImplementedException();

    public Task SaveUserAsync(ProviderUser user, Guid savingUserId) => throw new NotImplementedException();

    public Task<List<Tuple<ProviderUser, string>>> DeleteUsersAsync(Guid providerId, IEnumerable<Guid> providerUserIds, Guid deletingUserId) => throw new NotImplementedException();

    public Task AddOrganization(Guid providerId, Guid organizationId, string key) => throw new NotImplementedException();

    public Task AddOrganizationsToReseller(Guid providerId, IEnumerable<Guid> organizationIds) => throw new NotImplementedException();

    public Task<ProviderOrganization> CreateOrganizationAsync(Guid providerId, OrganizationSignup organizationSignup, string clientOwnerEmail, User user) => throw new NotImplementedException();

    public Task RemoveOrganizationAsync(Guid providerId, Guid providerOrganizationId, Guid removingUserId) => throw new NotImplementedException();

    public Task LogProviderAccessToOrganizationAsync(Guid organizationId) => throw new NotImplementedException();

    public Task ResendProviderSetupInviteEmailAsync(Guid providerId, Guid userId) => throw new NotImplementedException();
    public Task SendProviderSetupInviteEmailAsync(Provider provider, string ownerEmail) => throw new NotImplementedException();
    public Task InitiateDeleteAsync(Provider provider, string providerAdminEmail) => throw new NotImplementedException();
    public Task DeleteAsync(Provider provider, string token) => throw new NotImplementedException();
    public Task DeleteAsync(Provider provider) => throw new NotImplementedException();
}
