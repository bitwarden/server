using Bit.Core.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Billing.Pricing;

public static class ServiceCollectionExtensions
{
    public static void AddPricingClient(this IServiceCollection services)
    {
        services.AddHttpClient<PricingClient>((serviceProvider, client) =>
        {
            var globalSettings = serviceProvider.GetRequiredService<GlobalSettings>();
            client.BaseAddress = new Uri(globalSettings.PricingUri);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });
        services.AddTransient<IPricingClient, PricingClient>();
    }
}
