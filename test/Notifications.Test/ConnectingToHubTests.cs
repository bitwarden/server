using System.Net;

namespace Notifications.Test;

public class ConnectingToHubTests : IClassFixture<NotificationsApplicationFactory>
{
    private readonly NotificationsApplicationFactory _factory;

    public ConnectingToHubTests(NotificationsApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Browsers can't set headers on WebSocket and server-sent event requests, so SignalR clients send the
    /// access token in the query string instead.
    /// </summary>
    [Fact]
    public async Task Negotiate_AccessTokenInQueryString_IsAuthenticated()
    {
        var token = await _factory.LoginWithNewAccountAsync("connecting-to-hub@bitwarden.com");
        using var client = _factory.CreateClient();

        var response = await client.PostAsync(
            $"/hub/negotiate?negotiateVersion=1&access_token={Uri.EscapeDataString(token)}", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
