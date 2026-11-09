using Bit.Core.Settings;

namespace Bit.Seeder.Extensions;

internal static class SeederEnvironmentGuardExtensions
{
    internal static void ThrowIfSelfHosted(this IGlobalSettings globalSettings, string subject)
    {
        if (globalSettings.SelfHosted)
        {
            throw new InvalidOperationException(
                $"{subject} cannot exist on a self-hosted instance, but this Seeder API is running in self-hosted mode " +
                "('globalSettings:selfHosted' is true). Target a cloud Seeder API.");
        }
    }

    internal static void ThrowIfSelfHostedWithGatewayIds(
        this IGlobalSettings globalSettings, string? gatewayCustomerId, string? gatewaySubscriptionId)
    {
        if (!string.IsNullOrEmpty(gatewayCustomerId) || !string.IsNullOrEmpty(gatewaySubscriptionId))
        {
            globalSettings.ThrowIfSelfHosted("Stripe gateway IDs");
        }
    }

    internal static void ThrowIfNotSelfHosted(this IGlobalSettings globalSettings, string subject)
    {
        if (!globalSettings.SelfHosted)
        {
            throw new InvalidOperationException(
                $"{subject} requires a self-hosted instance, but this Seeder API is running in cloud mode " +
                "('globalSettings:selfHosted' is false). Target a self-hosted Seeder API.");
        }
    }
}
