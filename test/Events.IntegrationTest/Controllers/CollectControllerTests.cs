using System.Net.Http.Json;
using Bit.Core.Enums;
using Bit.Events.Models;

namespace Bit.Events.IntegrationTest.Controllers;

public class CollectControllerTests
{
    // This is a very simple test, and should be updated to assert more things, but for now
    // it ensures that the events startup doesn't throw any errors with fairly basic configuration.
    [Fact]
    public async Task Post_Works()
    {
        var eventsApplicationFactory = new EventsApplicationFactory();
        var (accessToken, _) = await eventsApplicationFactory.LoginWithNewAccount();
        var client = eventsApplicationFactory.CreateAuthedClient(accessToken);

        var response = await client.PostAsJsonAsync<IEnumerable<EventModel>>("collect",
        [
            new EventModel
            {
                Type = EventType.User_ClientExportedVault,
                Date = DateTime.UtcNow,
            },
        ]);

        response.EnsureSuccessStatusCode();
    }
}
