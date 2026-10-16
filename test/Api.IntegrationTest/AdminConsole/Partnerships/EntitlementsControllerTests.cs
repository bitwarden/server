using System.Net;
using System.Text.Json;
using Bit.Api.AdminConsole.Partnerships.Idempotency;
using Bit.Api.IntegrationTest.Factories;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Partnerships;

public class EntitlementsControllerTests : IClassFixture<ApiApplicationFactory>
{
    private const string Path = "/partnerships/v1/entitlements";

    private readonly ApiApplicationFactory _factory;

    public EntitlementsControllerTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.UpdateConfiguration("globalSettings:partnerships:externalIdHashKey", "integration-test-hash-key");
        PartnershipTestHelpers.EnablePartnerSponsorships(_factory);
    }

    [Fact]
    public async Task Post_NewEntitlement_Returns201WithActivationUrl()
    {
        var (client, _) = await CreatePartnerClientAsync();

        var response = await client.PostAsJsonAsync(Path,
            new { externalId = "customer-1", planType = "premium", metadata = new Dictionary<string, string> { ["tier"] = "gold" } });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadBodyAsync(response);
        Assert.Equal("entitlement", body.GetProperty("object").GetString());
        Assert.Equal("customer-1", body.GetProperty("externalId").GetString());
        Assert.Equal("provisioned", body.GetProperty("state").GetString());
        Assert.Equal("premium", body.GetProperty("planType").GetString());
        Assert.Equal("gold", body.GetProperty("metadata").GetProperty("tier").GetString());
        Assert.True(body.GetProperty("applied").GetBoolean());
        Assert.False(body.TryGetProperty("appliedReason", out _));
        Assert.False(body.TryGetProperty("accountRef", out _));
        var vault = _factory.GetService<GlobalSettings>().BaseServiceUri.Vault;
        Assert.StartsWith($"{vault}/#/partner-activation?token=", body.GetProperty("activationUrl").GetString());
        Assert.True(body.GetProperty("activationUrlExpiresAt").GetDateTime() > DateTime.UtcNow);
    }

    [Fact]
    public async Task Post_ExistingActiveEntitlement_Returns200UnchangedWithoutActivationUrl()
    {
        var (client, partnership) = await CreatePartnerClientAsync();
        await CreateEntitlementAsync(partnership, "customer-1", PartnershipEntitlementState.Active,
            (await CreateUserAsync()).Id);

        var response = await client.PostAsJsonAsync(Path, new { externalId = "customer-1" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadBodyAsync(response);
        Assert.Equal("active", body.GetProperty("state").GetString());
        Assert.False(body.GetProperty("applied").GetBoolean());
        Assert.False(body.TryGetProperty("activationUrl", out _));
        Assert.False(body.TryGetProperty("activationUrlExpiresAt", out _));
    }

    [Fact]
    public async Task Post_CanceledEntitlement_Returns200ResetToProvisioned()
    {
        var (client, partnership) = await CreatePartnerClientAsync();
        await CreateEntitlementAsync(partnership, "customer-1", PartnershipEntitlementState.Canceled,
            canceledDate: DateTime.UtcNow.AddDays(-1));

        var response = await client.PostAsJsonAsync(Path, new { externalId = "customer-1" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadBodyAsync(response);
        Assert.Equal("provisioned", body.GetProperty("state").GetString());
        Assert.True(body.GetProperty("applied").GetBoolean());
        Assert.False(body.TryGetProperty("canceledAt", out _));
        Assert.True(body.TryGetProperty("activationUrl", out _));
    }

    [Fact]
    public async Task Post_StaleRepostOfCanceledEntitlement_Returns200NotApplied()
    {
        var (client, partnership) = await CreatePartnerClientAsync();
        await CreateEntitlementAsync(partnership, "customer-1", PartnershipEntitlementState.Canceled,
            canceledDate: DateTime.UtcNow.AddDays(-1));

        var response = await client.PostAsJsonAsync(Path,
            new { externalId = "customer-1", effectiveAt = DateTime.UtcNow.AddDays(-30) });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadBodyAsync(response);
        Assert.Equal("canceled", body.GetProperty("state").GetString());
        Assert.False(body.GetProperty("applied").GetBoolean());
        Assert.Equal("stale_transition", body.GetProperty("appliedReason").GetString());
        Assert.False(body.TryGetProperty("activationUrl", out _));
    }

    [Fact]
    public async Task Post_PlanTypeNotAllowed_Returns400()
    {
        var (client, _) = await CreatePartnerClientAsync(allowedPlanTypes: []);

        var response = await client.PostAsJsonAsync(Path, new { externalId = "customer-1", planType = "premium" });

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "sponsored_plan_type_not_allowed");
    }

    [Fact]
    public async Task Post_UnknownPlanType_Returns400()
    {
        var (client, _) = await CreatePartnerClientAsync();

        var response = await client.PostAsJsonAsync(Path, new { externalId = "customer-1", planType = "families" });

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "invalid_request");
    }

    [Fact]
    public async Task Post_InvalidExternalId_Returns400()
    {
        var (client, _) = await CreatePartnerClientAsync();

        var response = await client.PostAsJsonAsync(Path, new { externalId = "P|customer-1" });

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "invalid_external_id");
    }

    [Fact]
    public async Task Post_FutureEffectiveAt_Returns422()
    {
        var (client, _) = await CreatePartnerClientAsync();

        var response = await client.PostAsJsonAsync(Path,
            new { externalId = "customer-1", effectiveAt = DateTime.UtcNow.AddHours(1) });

        await AssertErrorAsync(response, HttpStatusCode.UnprocessableEntity, "effective_at_in_future");
    }

    [Fact]
    public async Task Post_SameIdempotencyKey_ReplaysTheFirstResponse()
    {
        var (client, _) = await CreatePartnerClientAsync();
        var key = Guid.NewGuid().ToString();

        var first = await PostWithIdempotencyKeyAsync(client, key, new { externalId = "customer-1" });
        var replay = await PostWithIdempotencyKeyAsync(client, key, new { externalId = "customer-1" });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal("true", replay.Headers.GetValues(PartnershipIdempotencyFilter.ReplayedHeaderName).Single());
        Assert.Equal(await first.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Get_ExistingEntitlement_Returns200()
    {
        var (client, partnership) = await CreatePartnerClientAsync();
        await CreateEntitlementAsync(partnership, "customer-1", PartnershipEntitlementState.Provisioned);

        var response = await client.GetAsync($"{Path}/customer-1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadBodyAsync(response);
        Assert.Equal("customer-1", body.GetProperty("externalId").GetString());
        Assert.Equal("provisioned", body.GetProperty("state").GetString());
        Assert.False(body.TryGetProperty("applied", out _));
        Assert.False(body.TryGetProperty("activationUrl", out _));
    }

    [Fact]
    public async Task Get_UnknownExternalId_Returns404()
    {
        var (client, _) = await CreatePartnerClientAsync();

        var response = await client.GetAsync($"{Path}/unknown");

        await AssertErrorAsync(response, HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task Get_InvalidExternalId_Returns400()
    {
        var (client, _) = await CreatePartnerClientAsync();

        var response = await client.GetAsync($"{Path}/{Uri.EscapeDataString("P|customer-1")}");

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "invalid_external_id");
    }

    [Fact]
    public async Task Get_AnotherPartnershipsEntitlement_Returns404()
    {
        var (_, otherPartnership) = await CreatePartnerClientAsync();
        await CreateEntitlementAsync(otherPartnership, "shared-customer", PartnershipEntitlementState.Provisioned);
        var (client, _) = await CreatePartnerClientAsync();

        var response = await client.GetAsync($"{Path}/shared-customer");

        await AssertErrorAsync(response, HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task Get_BoundEntitlement_NeverExposesTheAccount()
    {
        var (client, partnership) = await CreatePartnerClientAsync();
        var user = await CreateUserAsync();
        var entitlement = await CreateEntitlementAsync(partnership, "customer-1", PartnershipEntitlementState.Active,
            user.Id);

        var response = await client.GetAsync($"{Path}/customer-1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("userId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(user.Id.ToString(), json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(user.Email, json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(entitlement.AccountRef!.Value.ToString(), json, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<(HttpClient Client, OrganizationPartnership Partnership)> CreatePartnerClientAsync(
        IEnumerable<SponsoredPlanType>? allowedPlanTypes = null)
    {
        var (client, partnership) =
            await PartnershipTestHelpers.CreateOrganizationClientAsync(_factory, PartnershipStatus.Active);
        partnership!.SetAllowedSponsoredPlanTypes(allowedPlanTypes ?? [SponsoredPlanType.Premium]);
        await _factory.GetService<IOrganizationPartnershipRepository>().ReplaceAsync(partnership);
        return (client, partnership);
    }

    private async Task<OrganizationPartnershipEntitlement> CreateEntitlementAsync(
        OrganizationPartnership partnership, string externalId, PartnershipEntitlementState state,
        Guid? userId = null, DateTime? canceledDate = null)
    {
        var now = DateTime.UtcNow;
        var entitlement = new OrganizationPartnershipEntitlement
        {
            OrganizationPartnershipId = partnership.Id,
            ExternalId = externalId,
            State = state,
            SponsoredPlanType = SponsoredPlanType.Premium,
            UserId = userId,
            CanceledDate = canceledDate,
            LastAppliedEffectiveDate = canceledDate ?? now,
            CreationDate = now,
            RevisionDate = now,
        };
        if (userId is not null)
        {
            entitlement.SetNewAccountRef();
            entitlement.BoundDate = now;
        }

        return await _factory.GetService<IOrganizationPartnershipEntitlementRepository>().CreateAsync(entitlement);
    }

    private async Task<User> CreateUserAsync()
    {
        var email = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(email);
        return (await _factory.GetService<IUserRepository>().GetByEmailAsync(email))!;
    }

    private static Task<HttpResponseMessage> PostWithIdempotencyKeyAsync(HttpClient client, string key, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Path) { Content = JsonContent.Create(body) };
        request.Headers.Add(PartnershipIdempotencyFilter.HeaderName, key);
        return client.SendAsync(request);
    }

    private static async Task<JsonElement> ReadBodyAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        var body = await ReadBodyAsync(response);
        Assert.Equal(code, body.GetProperty("code").GetString());
    }
}
