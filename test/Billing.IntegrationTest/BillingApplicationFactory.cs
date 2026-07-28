using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Bit.IntegrationTestCommon;
using Bit.Test.Common.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Bit.Billing.IntegrationTest;

/// <summary>
/// Application factory for the Bit.Billing webhook host. Holds an inner
/// <see cref="WebApplicationFactory{TEntryPoint}"/> privately, shares the
/// API host's database, and exposes a single intent method
/// (<see cref="SendStripeWebhookAsync"/>) that builds a valid signed Stripe
/// event payload and posts it to the controller — tests interact only through
/// that method.
/// </summary>
public sealed class BillingApplicationFactory : IAsyncDisposable
{
    public const string WebhookKey = "test-webhook-key";
    public const string WebhookSecret = "whsec_billing_integration_test_secret_value";

    /// <summary>
    /// Matches the API version the SDK is pinned to (see
    /// <see cref="Stripe.StripeConfiguration.ApiVersion"/>). The webhook controller
    /// rejects events whose <c>api_version</c> doesn't match this value.
    /// </summary>
    public static string SupportedStripeApiVersion => Stripe.StripeConfiguration.ApiVersion;

    private readonly WebApplicationFactory<Bit.Billing.Program> _factory;

    public BillingApplicationFactory(ITestDatabase testDatabase)
    {
        _factory = new WebApplicationFactory<Bit.Billing.Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(Environments.Development);

            builder.ConfigureAppConfiguration((_, config) =>
            {
                var configValues = new Dictionary<string, string?>
                {
                    ["BillingSettings:StripeWebhookKey"] = WebhookKey,
                    ["BillingSettings:StripeWebhookSecret20250827Basil"] = WebhookSecret,
                };
                testDatabase.ModifyGlobalSettings(configValues);
                config.AddInMemoryCollection(configValues);
            });

            builder.ConfigureServices(services =>
            {
                // Drop the Quartz-backed jobs hosted service — it spins up a scheduler we
                // don't need (and that throws under parallel test execution).
                var jobsHostedService = services.FirstOrDefault(sd =>
                    sd.ServiceType == typeof(IHostedService)
                    && sd.ImplementationType?.FullName == "Bit.Billing.Jobs.JobsHostedService");
                if (jobsHostedService != null)
                {
                    services.Remove(jobsHostedService);
                }

                services.RemoveAll<Quartz.IScheduler>();

                testDatabase.AddDatabase(services);
            });
        });
    }

    /// <summary>
    /// Builds a Stripe-signed event with the given <paramref name="eventType"/> and
    /// <paramref name="dataObject"/>, posts it to <c>POST /stripe/webhook</c>, and
    /// asserts a successful response. The handler for the event type re-fetches the
    /// underlying object from Stripe with the production Expand list — that fetch is
    /// what the test exercises.
    /// </summary>
    public async Task SendStripeWebhookAsync(string eventType, JsonObject dataObject, string eventId)
    {
        var payload = new JsonObject
        {
            ["id"] = eventId,
            ["object"] = "event",
            ["api_version"] = SupportedStripeApiVersion,
            ["created"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ["livemode"] = false,
            ["pending_webhooks"] = 0,
            ["type"] = eventType,
            ["data"] = new JsonObject
            {
                ["object"] = dataObject,
            },
            ["request"] = new JsonObject
            {
                ["id"] = $"req_{Guid.NewGuid():N}",
                ["idempotency_key"] = null,
            },
        };

        var json = payload.ToJsonString();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = ComputeStripeSignature(timestamp, json, WebhookSecret);

        using var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"/stripe/webhook?key={WebhookKey}")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("Stripe-Signature", $"t={timestamp},v1={signature}");

        var response = await client.SendAsync(request);
        await Assert.SuccessResponseAsync(response);
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private static string ComputeStripeSignature(long timestamp, string payload, string secret)
    {
        // Stripe webhook signature scheme (v1): HMAC-SHA256(secret, "{timestamp}.{payload}") as lowercase hex.
        var signedPayload = $"{timestamp}.{payload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload));
        return Convert.ToHexStringLower(hash);
    }
}
