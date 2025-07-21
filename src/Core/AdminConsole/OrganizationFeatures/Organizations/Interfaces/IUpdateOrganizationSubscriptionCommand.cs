using Bit.Core.AdminConsole.Models.Data.Organizations;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;

public interface IUpdateOrganizationSubscriptionCommand
{
    Task UpdateOrganizationSubscriptionAsync(IEnumerable<OrganizationSubscriptionUpdate> subscriptionsToUpdate);
}
