using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationSubscriptionUpdate.Interface;

public interface IAdjustServiceAccountsCommand
{
    Task<string> AdjustServiceAccountsAsync(Organization organization, int serviceAccountAdjustment,
        IEnumerable<string> ownerEmails = null, DateTime? prorationDate = null);
}
