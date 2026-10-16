using System.Net;
using System.Text.Json;
using Bit.Api.IntegrationTest.Factories;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Settings;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Partnerships;

public class PartnershipControllerTests : IClassFixture<ApiApplicationFactory>
{
    private const string Path = "/partnerships/v1/partnership";

    private readonly ApiApplicationFactory _factory;

    public PartnershipControllerTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        PartnershipTestHelpers.EnablePartnerSponsorships(_factory);
    }

    [Fact]
    public async Task Get_ReturnsTheAuthenticatedPartnershipsConfiguration()
    {
        var (client, partnership) =
            await PartnershipTestHelpers.CreateOrganizationClientAsync(_factory, PartnershipStatus.Active);
        partnership!.SetAllowedSponsoredPlanTypes([SponsoredPlanType.Premium]);
        partnership.SetRegisteredReturnOrigins(["https://partner.example.com"]);
        await _factory.GetService<IOrganizationPartnershipRepository>().ReplaceAsync(partnership);

        var response = await client.GetAsync(Path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(
            ["object", "name", "status", "allowedPlanTypes", "bindingMode", "registeredReturnOrigins", "rateLimit"],
            body.EnumerateObject().Select(p => p.Name));
        Assert.Equal("partnership", body.GetProperty("object").GetString());
        Assert.Equal(partnership.Name, body.GetProperty("name").GetString());
        Assert.Equal("active", body.GetProperty("status").GetString());
        Assert.Equal(["premium"], body.GetProperty("allowedPlanTypes").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal("token", body.GetProperty("bindingMode").GetString());
        Assert.Equal(["https://partner.example.com"],
            body.GetProperty("registeredReturnOrigins").EnumerateArray().Select(e => e.GetString()));
        var settings = _factory.GetService<GlobalSettings>().Partnerships;
        var rateLimit = body.GetProperty("rateLimit");
        Assert.Equal(settings.RateLimitRequests, rateLimit.GetProperty("requests").GetInt32());
        Assert.Equal((int)settings.RateLimitWindow.TotalSeconds, rateLimit.GetProperty("windowSeconds").GetInt32());
    }
}
