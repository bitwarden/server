using Bit.Core.Models.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Services;

public class EventRouteService(
    [FromKeyedServices("broadcast")] IEventWriteService broadcastEventWriteService,
    [FromKeyedServices("storage")] IEventWriteService storageEventWriteService,
    IFeatureService _featureService) : IEventWriteService
{
    public async Task CreateAsync(IEvent e)
    {
        if (_featureService.IsEnabled(FeatureFlagKeys.EventBasedOrganizationIntegrations))
        {
            await broadcastEventWriteService.CreateAsync(e);
        }
        else
        {
            await storageEventWriteService.CreateAsync(e);
        }
    }

    public async Task CreateManyAsync(IEnumerable<IEvent> e)
    {
        if (_featureService.IsEnabled(FeatureFlagKeys.EventBasedOrganizationIntegrations))
        {
            await broadcastEventWriteService.CreateManyAsync(e);
        }
        else
        {
            await storageEventWriteService.CreateManyAsync(e);
        }
    }
}
