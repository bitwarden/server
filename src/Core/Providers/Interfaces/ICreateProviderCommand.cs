using Bit.Core.Billing.Enums;
using Bit.Core.Providers.Entities;

namespace Bit.Core.Providers.Interfaces;

public interface ICreateProviderCommand
{
    Task CreateMspAsync(Provider provider, string ownerEmail, int teamsMinimumSeats, int enterpriseMinimumSeats);
    Task CreateResellerAsync(Provider provider);
    Task CreateBusinessUnitAsync(Provider provider, string ownerEmail, PlanType plan, int minimumSeats);
}
