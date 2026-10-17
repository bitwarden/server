using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Bit.Api.AdminConsole.Partnerships;
using Bit.Api.AdminConsole.Partnerships.Controllers;
using Bit.Api.IntegrationTest.Factories;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Partnerships;

public class PartnershipAuthenticationTests : IClassFixture<ApiApplicationFactory>
{
    private const string Path = "/partnerships/v1/auth-test";

    private readonly ApiApplicationFactory _factory;

    public PartnershipAuthenticationTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.ConfigureServices(services =>
            services.AddMvcCore().AddApplicationPart(typeof(PartnershipAuthTestController).Assembly));
        PartnershipTestHelpers.EnablePartnerSponsorships(_factory);
    }

    [Fact]
    public async Task NoToken_Returns401()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(Path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserToken_IsRejected()
    {
        var email = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        var (token, _) = await _factory.LoginWithNewAccount(email);
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync(Path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain("partnership_not_active", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task OrganizationWithoutPartnership_Returns404Envelope()
    {
        using var client = (await PartnershipTestHelpers.CreateOrganizationClientAsync(_factory, status: null)).Client;

        var response = await client.GetAsync(Path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertEnvelopeAsync(response, "not_found");
    }

    [Fact]
    public async Task OrganizationWithoutPartnership_InvalidModel_Returns404Not400()
    {
        using var client = (await PartnershipTestHelpers.CreateOrganizationClientAsync(_factory, status: null)).Client;

        using var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/partnerships/v1/envelope-test/model", content);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertEnvelopeAsync(response, "not_found");
    }

    [Fact]
    public async Task InactivePartnership_Returns403Envelope()
    {
        using var client = (await PartnershipTestHelpers.CreateOrganizationClientAsync(_factory,
            PartnershipStatus.Inactive)).Client;

        var response = await client.GetAsync(Path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertEnvelopeAsync(response, "partnership_not_active");
    }

    [Fact]
    public async Task ActivePartnership_Returns200WithResolvedPartnership()
    {
        var created = await PartnershipTestHelpers.CreateOrganizationClientAsync(_factory, PartnershipStatus.Active);
        using var client = created.Client;

        var response = await client.GetAsync(Path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(created.Partnership!.Id.ToString(), await response.Content.ReadAsStringAsync());
    }

    private static async Task AssertEnvelopeAsync(HttpResponseMessage response, string expectedCode)
    {
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("error", body.GetProperty("object").GetString());
        Assert.Equal(expectedCode, body.GetProperty("code").GetString());
        Assert.Equal(response.Headers.GetValues("X-Request-Id").Single(), body.GetProperty("requestId").GetString());
    }
}

[Route("partnerships/v1/auth-test")]
public class PartnershipAuthTestController(IPartnershipRequestContext partnershipRequestContext)
    : PartnershipControllerBase
{
    [HttpGet]
    public ActionResult<string> Get() => partnershipRequestContext.Partnership.Id.ToString();
}
