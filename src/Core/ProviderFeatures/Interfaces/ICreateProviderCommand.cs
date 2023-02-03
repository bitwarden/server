using Bit.Core.Entities.Provider;

namespace Bit.Core.ProviderFeatures.Interfaces;

public interface ICreateProviderCommand
{
    Task CreateMspAsync(Provider provider, string ownerEmail);
    Task CreateResellerAsync(Provider provider);
}
