using Bit.Core.AdminConsole.Entities.Provider;

namespace Bit.Core.AdminConsole.Providers.Interfaces;

public interface ICreateProviderCommand
{
    Task CreateMspAsync(Provider provider, string ownerEmail);
    Task CreateResellerAsync(Provider provider);
}
