using Bit.IntegrationTestCommon.Factories;
using static Bit.Identity.IntegrationTest.Helpers.TokenEndpointTestHelpers;

namespace Bit.Identity.IntegrationTest.Middleware;

public class RateLimitTests : IClassFixture<IdentityApplicationFactory>
{
    private readonly IdentityApplicationFactory _factory;

    public RateLimitTests(IdentityApplicationFactory factory)
    {
        _factory = factory;
        InitializeSharedFactory(_factory);
    }

    [Fact]
    public async Task TokenEndpoint_TooQuickInOneSecond_BlockRequest()
    {
        // The configured IP rate limit rules allow 5 requests per second to any endpoint and 10 requests per
        // minute to POST /connect/token. Sending 11 token requests at once from one IP exceeds both rules.
        const int TokenRequestsPerMinuteAllowed = 10;

        // Rate limiting runs before login, so the requests do not need an existing user.
        const string Email = "test+ratelimiting@email.com";

        var tasks = new Task<HttpContext>[TokenRequestsPerMinuteAllowed + 1];

        for (var i = 0; i < TokenRequestsPerMinuteAllowed + 1; i++)
        {
            // Queue all the amount of calls allowed plus 1
            tasks[i] = MakeRequest();
        }

        var responses = (await Task.WhenAll(tasks)).ToList();
        var blockResponses = responses.Where(c => c.Response.StatusCode == StatusCodes.Status429TooManyRequests);

        Assert.True(blockResponses.Count() > 0);

        Task<HttpContext> MakeRequest()
        {
            return _factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "scope", "api offline_access" },
                { "client_id", "web" },
                { "deviceType", DeviceTypeAsString(DeviceType.FirefoxBrowser) },
                { "deviceIdentifier", IdentityApplicationFactory.DefaultDeviceIdentifier },
                { "deviceName", "firefox" },
                { "grant_type", "password" },
                { "username", Email },
                { "password", "master_password_hash" },
            }), context => context.SetIp("1.1.1.2"));
        }
    }
}
