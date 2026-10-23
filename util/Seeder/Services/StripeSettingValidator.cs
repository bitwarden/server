using Bit.Core.Settings;

namespace Bit.Seeder.Services;

/// <summary>
/// The host checks every Seeder Stripe call must pass first: a test-mode key, a pricing service, and cloud mode.
/// </summary>
public static class StripeSettingValidator
{
    /// <summary>
    /// Only a test-mode secret key is ever acceptable — this tool must not touch live billing.
    /// </summary>
    public const string TestKeyPrefix = "sk_test_";

    /// <summary>
    /// Throws when the host is not configured for Stripe test-mode billing.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the configuration is unusable.</exception>
    public static void Validate(GlobalSettings globalSettings)
    {
        var apiKey = globalSettings.Stripe?.ApiKey;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Stripe billing was requested but no Stripe API key is configured. Set " +
                $"'globalSettings:stripe:apiKey' to a test-mode key ('{TestKeyPrefix}…') in the " +
                "host's user secrets ('bitwarden-seeder-utility' for the CLI, 'bitwarden-seeder-api' for the SeederApi).");
        }

        if (!apiKey.StartsWith(TestKeyPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Stripe billing requires a test-mode API key starting with '{TestKeyPrefix}'. The configured " +
                "key is not one, and the Seeder refuses to create billing against a live Stripe account.");
        }

        if (string.IsNullOrWhiteSpace(globalSettings.PricingUri))
        {
            throw new InvalidOperationException(
                "Stripe billing requires 'globalSettings:pricingUri' so plan pricing can be resolved. " +
                "Run the host with ASPNETCORE_ENVIRONMENT=Development so its appsettings.Development.json is " +
                "loaded, or set the value in user secrets.");
        }

        if (globalSettings.SelfHosted)
        {
            throw new InvalidOperationException(
                "Stripe billing requires cloud mode, but 'globalSettings:selfHosted' is true — the Pricing " +
                "Service is never called in self-hosted mode, so no plan can be resolved. Run this command " +
                "with 'globalSettings__selfHosted=false' in the environment.");
        }
    }
}
