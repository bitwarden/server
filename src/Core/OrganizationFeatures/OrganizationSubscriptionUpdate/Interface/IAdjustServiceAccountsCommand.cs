using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationSubscriptionUpdate.Interface;

public interface IAdjustServiceAccountCommand
{
    Task<string> AdjustServiceAccountAsync(Organization organization, int serviceAccountAdjustment,
        IEnumerable<string> ownerEmails = null, DateTime? prorationDate = null);
}
