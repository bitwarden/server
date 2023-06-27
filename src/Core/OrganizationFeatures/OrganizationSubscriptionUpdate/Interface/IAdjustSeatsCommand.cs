using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationSubscriptionUpdate.Interface;

public interface IAdjustSeatsCommand
{
    Task<string> AdjustSeatsAsync(Organization organization, int seatAdjustment,
        DateTime? prorationDate = null, IEnumerable<string> ownerEmails = null);
}
