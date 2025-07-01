using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.Repositories;

public interface IOrganizationSubscriptionUpdateRepository
{
    Task SetToUpdateSubscriptionAsync(Guid organizationId, DateTime seatsUpdatedAt);
    Task<IEnumerable<OrganizationSubscriptionUpdate>> GetUpdatesToSubscriptionAsync();

    Task UpdateSubscriptionStatusAsync(IEnumerable<Guid> successfulOrganizations,
        IEnumerable<Guid> failedOrganizations);
}
