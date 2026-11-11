using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Notifications.Test;

/// <summary>
/// <c>POST /send</c> only exists on self-hosted installations; cloud receives notifications from the Azure queue.
/// </summary>
public class SendEndpointAvailabilityTests
{
    [Fact]
    public async Task Cloud_SendIsNotFound()
    {
        await using var cloudFactory = new WebApplicationFactory<Bit.Notifications.Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "OpenTelemetry:Enabled", "false" },
                    { "globalSettings:selfHosted", "false" },
                    // Development settings point the queue reader at local storage; keep it from draining the
                    // developer's real queue.
                    { "globalSettings:notifications:connectionString", "" },
                });
            });
        });
        using var client = cloudFactory.CreateClient();

        var response = await client.PostAsJsonAsync("/send", new { });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
