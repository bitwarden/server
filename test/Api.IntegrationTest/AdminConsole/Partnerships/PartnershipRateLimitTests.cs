using System.Net;
using System.Text.Json;
using Bit.Api.AdminConsole.Partnerships.Controllers;
using Bit.Api.IntegrationTest.Factories;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Partnerships;

public class PartnershipRateLimitTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private const int Limit = 2;

    private readonly ApiApplicationFactory _factory;
    private HttpClient _client = null!;

    public PartnershipRateLimitTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.ConfigureServices(services =>
            services.AddMvcCore().AddApplicationPart(typeof(PartnershipRateLimitTestController).Assembly));
        _factory.UpdateConfiguration("globalSettings:partnerships:rateLimitRequests", Limit.ToString());
        // Long enough that the requests below can't straddle a window boundary.
        _factory.UpdateConfiguration("globalSettings:partnerships:rateLimitWindow", "1.00:00:00");
        PartnershipTestHelpers.EnablePartnerSponsorships(_factory);
    }

    public async Task InitializeAsync() =>
        _client = (await PartnershipTestHelpers.CreateOrganizationClientAsync(_factory, PartnershipStatus.Active)).Client;

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task OverLimit_Returns429EnvelopeWithRetryAfter()
    {
        for (var i = 0; i < Limit; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/partnerships/v1/rate-limit-test")).StatusCode);
        }

        var response = await _client.GetAsync("/partnerships/v1/rate-limit-test");

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        var retryAfter = response.Headers.RetryAfter?.Delta;
        Assert.NotNull(retryAfter);
        Assert.InRange(retryAfter.Value, TimeSpan.FromSeconds(1), TimeSpan.FromDays(1));
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(["object", "code", "message", "requestId"], body.EnumerateObject().Select(p => p.Name));
        Assert.Equal("rate_limited", body.GetProperty("code").GetString());
        Assert.Equal(response.Headers.GetValues("X-Request-Id").Single(), body.GetProperty("requestId").GetString());
    }
}

[Route("partnerships/v1/rate-limit-test")]
public class PartnershipRateLimitTestController : PartnershipControllerBase
{
    [HttpGet]
    public ActionResult<string> Get() => "ok";
}
