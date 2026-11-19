using System.Net.Http.Json;

namespace Notifications.Test;

public class HealthCheckTests : IClassFixture<NotificationsApplicationFactory>
{
    private readonly NotificationsApplicationFactory _factory;

    public HealthCheckTests(NotificationsApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/alive")]
    [InlineData("/now")]
    public async Task Get_ReturnsCurrentUtcTime(string path)
    {
        using var client = _factory.CreateClient();
        var before = DateTime.UtcNow;

        var now = await client.GetFromJsonAsync<DateTime>(path);

        Assert.InRange(now, before.AddSeconds(-1), DateTime.UtcNow.AddSeconds(1));
    }
}
