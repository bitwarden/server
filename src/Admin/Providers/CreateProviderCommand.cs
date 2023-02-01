using Bit.Admin.Models;
using Bit.Admin.Providers.Interfaces;
using Bit.Core.Entities.Provider;
using Bit.Core.Enums.Provider;
using Bit.Core.Services;

namespace Bit.Admin.Providers;

public class CreateProviderCommand : ICreateProviderCommand
{
    private readonly IProviderService _providerService;

    public CreateProviderCommand(IProviderService providerService)
    {
        _providerService = providerService;
    }

    public async Task<Provider> Create(CreateProviderModel model)
    {
        var provider = model.ToProvider();
        switch (provider.Type)
        {
            case ProviderType.Msp:
                await _providerService.CreateMspAsync(provider, model.OwnerEmail);
                break;
            case ProviderType.Reseller:
                await _providerService.CreateResellerAsync(provider);
                break;
        }

        return provider;
    }
}
