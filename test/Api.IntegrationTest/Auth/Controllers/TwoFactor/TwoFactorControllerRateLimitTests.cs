using System.Text;
using Bit.Api.IntegrationTest.Factories;
using Bit.IntegrationTestCommon.Factories;
using Xunit;

namespace Bit.Api.IntegrationTest.Auth.Controllers.TwoFactor;

public class TwoFactorControllerRateLimitTests : IClassFixture<ApiApplicationFactory>
{
    // The configured IP rate limit rules allow 3 requests per minute to each of these routes.
    private const int RequestsPerMinuteAllowed = 3;

    private readonly ApiApplicationFactory _factory;

    public TwoFactorControllerRateLimitTests(ApiApplicationFactory factory)
    {
        _factory = factory;
    }

    // Each case uses its own IP so that the rate limit counters of one case do not affect another.
    [Theory]
    [InlineData("/two-factor/authenticator", "203.0.113.10")]
    [InlineData("/two-factor/email", "203.0.113.11")]
    public async Task Put_ExceedsPerMinuteLimit_BlocksRequest(string route, string ip)
    {
        // Rate limiting runs before authentication, so the requests do not need a signed-in user.
        // The requests are sent one at a time to stay under the 5 requests per second limit on all PUT routes.
        var responses = new List<HttpContext>();
        for (var i = 0; i < RequestsPerMinuteAllowed + 1; i++)
        {
            responses.Add(await _factory.Server.PutAsync(route,
                new StringContent("{}", Encoding.UTF8, "application/json"),
                context => context.SetIp(ip)));
        }

        Assert.All(responses.Take(RequestsPerMinuteAllowed),
            r => Assert.NotEqual(StatusCodes.Status429TooManyRequests, r.Response.StatusCode));
        Assert.Equal(StatusCodes.Status429TooManyRequests, responses.Last().Response.StatusCode);
    }
}
