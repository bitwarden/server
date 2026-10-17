using System.Net;
using System.Text.Json;
using Bit.Api.AdminConsole.Partnerships.Idempotency;
using Bit.Api.IntegrationTest.Factories;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships.Interfaces;
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

    [Fact]
    public async Task List_MoreThanOnePage_WalksEveryEntitlementOnce()
    {
        var (client, partnership) = await CreatePartnerClientAsync();
        // All rows share one RevisionDate, so paging relies on the Id tiebreak.
        var revisionDate = DateTime.UtcNow.AddMinutes(-1);
        var count = ListPartnershipEntitlementsQuery.PageSize + 1;
        for (var i = 0; i < count; i++)
        {
            await CreateEntitlementAsync(partnership, $"customer-{i}", PartnershipEntitlementState.Provisioned,
                revisionDate: revisionDate);
        }

        var first = await ReadBodyAsync(await client.GetAsync(Path));
        var token = first.GetProperty("continuationToken").GetString();
        var secondResponse = await client.GetAsync($"{Path}?continuationToken={Uri.EscapeDataString(token!)}");

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var second = await ReadBodyAsync(secondResponse);
        Assert.Equal("list", first.GetProperty("object").GetString());
        Assert.Equal(ListPartnershipEntitlementsQuery.PageSize, first.GetProperty("data").GetArrayLength());
        Assert.Equal(1, second.GetProperty("data").GetArrayLength());
        Assert.False(second.TryGetProperty("continuationToken", out _));
        var externalIds = ExternalIds(first).Concat(ExternalIds(second)).ToList();
        Assert.Equal(Enumerable.Range(0, count).Select(i => $"customer-{i}").Order(), externalIds.Order());
    }

    [Fact]
    public async Task List_StateFilter_ReturnsOnlyThatState()
    {
        var (client, partnership) = await CreatePartnerClientAsync();
        await CreateEntitlementAsync(partnership, "customer-1", PartnershipEntitlementState.Provisioned);
        await CreateEntitlementAsync(partnership, "customer-2", PartnershipEntitlementState.Canceled,
            canceledDate: DateTime.UtcNow.AddDays(-1));

        var response = await client.GetAsync($"{Path}?state=canceled");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadBodyAsync(response);
        Assert.Equal(["customer-2"], ExternalIds(body));
        Assert.Equal("canceled", body.GetProperty("data")[0].GetProperty("state").GetString());
    }

    [Fact]
    public async Task List_RevisedAfter_ReturnsOnlyLaterRevisionsInOrder()
    {
        var (client, partnership) = await CreatePartnerClientAsync();
        var cutoff = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        await CreateEntitlementAsync(partnership, "at-cutoff", PartnershipEntitlementState.Provisioned,
            revisionDate: cutoff);
        await CreateEntitlementAsync(partnership, "latest", PartnershipEntitlementState.Provisioned,
            revisionDate: cutoff.AddHours(2));
        await CreateEntitlementAsync(partnership, "later", PartnershipEntitlementState.Provisioned,
            revisionDate: cutoff.AddHours(1));

        // 08:00 at -04:00 is the cutoff in UTC.
        var response = await client.GetAsync($"{Path}?revisedAfter={Uri.EscapeDataString("2026-06-01T08:00:00-04:00")}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["later", "latest"], ExternalIds(await ReadBodyAsync(response)));
    }

    [Fact]
    public async Task List_UnknownState_Returns400()
    {
        var (client, _) = await CreatePartnerClientAsync();

        var response = await client.GetAsync($"{Path}?state=pending");

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "invalid_request");
    }

    [Fact]
    public async Task List_InvalidRevisedAfter_Returns400()
    {
        var (client, _) = await CreatePartnerClientAsync();

        var response = await client.GetAsync($"{Path}?revisedAfter=yesterday");

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "invalid_request");
    }

    [Fact]
    public async Task List_TamperedToken_Returns400()
    {
        var (client, partnership) = await CreatePartnerClientAsync();
        var token = IssueToken(partnership.Id);
        var tampered = token[..^2] + (token[^2] == 'A' ? 'B' : 'A') + token[^1];

        var response = await client.GetAsync($"{Path}?continuationToken={tampered}");

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "invalid_continuation_token");
    }

    [Fact]
    public async Task List_TokenFromAnotherPartnership_Returns400()
    {
        var (_, otherPartnership) = await CreatePartnerClientAsync();
        var (client, _) = await CreatePartnerClientAsync();

        var response = await client.GetAsync($"{Path}?continuationToken={IssueToken(otherPartnership.Id)}");

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "invalid_continuation_token");
    }

    [Fact]
    public async Task List_TokenSentWithDifferentFilters_Returns400()
    {
        var (client, partnership) = await CreatePartnerClientAsync();

        var response = await client.GetAsync($"{Path}?state=active&continuationToken={IssueToken(partnership.Id)}");

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "invalid_continuation_token");
    }

    [Fact]
    public async Task List_BoundEntitlement_NeverExposesTheAccount()
    {
        var (client, partnership) = await CreatePartnerClientAsync();
        var user = await CreateUserAsync();
        var entitlement = await CreateEntitlementAsync(partnership, "customer-1", PartnershipEntitlementState.Active,
            user.Id);

        var response = await client.GetAsync(Path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("userId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(user.Id.ToString(), json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(user.Email, json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(entitlement.AccountRef!.Value.ToString(), json, StringComparison.OrdinalIgnoreCase);
    }

    private string IssueToken(Guid organizationPartnershipId) =>
        _factory.GetService<IPartnershipEntitlementContinuationTokenProtector>().Protect(
            new ListPartnershipEntitlementsRequest { OrganizationPartnershipId = organizationPartnershipId },
            new PartnershipEntitlementKeyset(DateTime.UtcNow, Guid.NewGuid()));

    private static List<string> ExternalIds(JsonElement listBody) =>
        listBody.GetProperty("data").EnumerateArray().Select(e => e.GetProperty("externalId").GetString()!).ToList();

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
        Guid? userId = null, DateTime? canceledDate = null, DateTime? revisionDate = null)
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
            RevisionDate = revisionDate ?? now,
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

    private static async Task<HttpResponseMessage> PostWithIdempotencyKeyAsync(HttpClient client, string key, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Path) { Content = JsonContent.Create(body) };
        request.Headers.Add(PartnershipIdempotencyFilter.HeaderName, key);
        return await client.SendAsync(request);
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
