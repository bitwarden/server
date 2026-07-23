using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Enums;
using Bit.Core.Utilities;
using Bit.Seeder.Services;
using Provider = Bit.Core.AdminConsole.Entities.Provider.Provider;

namespace Bit.Seeder.Factories;

internal static class ProviderSeeder
{
    internal static Provider Create(
        string name,
        string domain,
        ProviderType type,
        IManglerService manglerService)
    {
        return new Provider
        {
            Id = CombGuid.Generate(),
            Name = manglerService.Mangle(name),
            BillingEmail = BillingEmailSeeder.DeriveBillingEmail(domain),
            Type = type,
            Status = ProviderStatusType.Billable,
            Enabled = true,
            UseEvents = false,
            Gateway = GatewayType.Stripe
        };
    }

    /// <summary>
    /// Applies billing gateway identity to a provider so it resembles a real billed provider.
    /// Only non-null values are set; nulls leave the field unchanged.
    /// </summary>
    internal static void ApplyBilling(Provider provider, GatewayType? gateway, string? gatewayCustomerId, string? gatewaySubscriptionId)
    {
        provider.Gateway = gateway ?? provider.Gateway;
        provider.GatewayCustomerId = gatewayCustomerId ?? provider.GatewayCustomerId;
        provider.GatewaySubscriptionId = gatewaySubscriptionId ?? provider.GatewaySubscriptionId;
    }
}
