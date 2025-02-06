using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.Billing.Enums;

namespace Bit.Core.AdminConsole.Providers.Interfaces;

public interface ICreateProviderCommand
{
    Task CreateMspAsync(Provider provider, string ownerEmail, int teamsMinimumSeats, int enterpriseMinimumSeats);
    Task CreateResellerAsync(Provider provider);
    Task CreateMultiOrganizationEnterpriseAsync(Provider provider, string ownerEmail, PlanType plan, int minimumSeats);
}
