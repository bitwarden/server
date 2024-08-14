using Bit.Core.AdminConsole.Entities.Provider;

namespace Bit.Core.AdminConsole.Providers.Interfaces;

public interface ICreateProviderCommand
{
    Task CreateMspAsync(Provider provider, string ownerEmail, int teamsMinimumSeats, int enterpriseMinimumSeats);
    Task CreateResellerAsync(Provider provider);
}
