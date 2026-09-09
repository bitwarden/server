using Bit.Core.Enums;
using Bit.Core.Utilities;
using Bit.Seeder.Models;
using Bit.Seeder.Services;
using Provider = Bit.Core.AdminConsole.Entities.Provider.Provider;

namespace Bit.Seeder.Factories;

internal static class ProviderSeeder
{
    internal static Provider Create(ProviderSeed seed, IManglerService manglerService)
    {
        return new Provider
        {
            Id = CombGuid.Generate(),
            Name = manglerService.Mangle(seed.Name),
            BillingEmail = BillingEmailSeeder.DeriveBillingEmail(seed.Domain),
            BusinessName = seed.BusinessName,
            BusinessCountry = seed.BusinessCountry,
            BillingPhone = seed.BillingPhone,
            Type = seed.Type,
            Status = seed.Status,
            Enabled = seed.Enabled,
            UseEvents = seed.UseEvents,
            // Unlike User and Organization, Provider carries a gateway default — preserve it when
            // the caller supplies none, or seeded providers lose their billing surface.
            Gateway = seed.Gateway ?? GatewayType.Stripe,
            GatewayCustomerId = seed.GatewayCustomerId,
            GatewaySubscriptionId = seed.GatewaySubscriptionId
        };
    }
}
