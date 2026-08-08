using System.Diagnostics;
using System.Globalization;
using Bit.Core.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Bit.Core.Billing.Pricing;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers an <see cref="IPricingClient"/> used to resolve Bitwarden plan data.
    /// </summary>
    /// <remarks>
    /// The implementation that gets resolved is selected from <see cref="GlobalSettings.SelfHosted"/>
    /// and <see cref="GlobalSettings.PricingUri"/>:
    /// <list type="bullet">
    ///   <item>
    ///     When <c>SelfHosted</c> is true, the registered client returns no plan data — null from
    ///     single-plan lookups and empty lists from list lookups — since self-hosted instances do not
    ///     run their own Pricing Service. <c>PricingUri</c> is ignored in this mode.
    ///   </item>
    ///   <item>
    ///     When <c>PricingUri</c> is set, the registered client calls the remote Bitwarden Pricing Service
    ///     at that base address for every plan lookup.
    ///   </item>
    ///   <item>
    ///     When <c>PricingUri</c> is null or empty and the host environment is Development, the registered
    ///     client serves plan data from a built-in static set so the server can run without a Pricing Service
    ///     deployment. Plan data is expected to drift from the remote service over time.
    ///   </item>
    ///   <item>
    ///     When <c>PricingUri</c> is null or empty and the host environment is not Development,
    ///     resolving <see cref="IPricingClient"/> throws <see cref="InvalidOperationException"/> —
    ///     a misconfigured cloud deployment should fail fast rather than serve stale local data.
    ///   </item>
    /// </list>
    /// </remarks>
    public static IServiceCollection AddPricingClient(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpClient<HttpPricingClient>((serviceProvider, httpClient) =>
        {
            var globalSettings = serviceProvider.GetRequiredService<GlobalSettings>();
            Debug.Assert(globalSettings.PricingUri is not null);
            httpClient.BaseAddress = new Uri(globalSettings.PricingUri);
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            httpClient.DefaultRequestHeaders.Add(
                "Bitwarden-Region",
                globalSettings.BaseServiceUri.CloudRegion?.ToLower(CultureInfo.InvariantCulture) ?? "us"
            );
        });

        services.TryAddSingleton<NoopPricingClient>();
        services.TryAddSingleton<LocalPricingClient>();

        services.TryAddTransient<IPricingClient>(serviceProvider =>
        {
            var globalSettings = serviceProvider.GetRequiredService<GlobalSettings>();

            if (globalSettings.SelfHosted)
            {
                return serviceProvider.GetRequiredService<NoopPricingClient>();
            }

            if (string.IsNullOrEmpty(globalSettings.PricingUri))
            {
                var environment = serviceProvider.GetRequiredService<IHostEnvironment>();
                if (!environment.IsDevelopment())
                {
                    throw new InvalidOperationException("No configured pricing service.");
                }

                return serviceProvider.GetRequiredService<LocalPricingClient>();
            }

            return serviceProvider.GetRequiredService<HttpPricingClient>();
        });

        return services;
    }
}
