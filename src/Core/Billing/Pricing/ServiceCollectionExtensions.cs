using Bit.Core.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Billing.Pricing;

public static class ServiceCollectionExtensions
{
    public static void AddPricingClient(this IServiceCollection services)
    {
        services.AddHttpClient<IPricingClient, PricingClient>((serviceProvider, httpClient) =>
        {
            var globalSettings = serviceProvider.GetRequiredService<GlobalSettings>();
            if (string.IsNullOrEmpty(globalSettings.PricingUri))
            {
                return;
            }
            httpClient.BaseAddress = new Uri(globalSettings.PricingUri);
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        });
    }
}
