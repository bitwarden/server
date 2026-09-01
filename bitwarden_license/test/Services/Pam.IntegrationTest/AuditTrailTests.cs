using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Bit.Api.IntegrationTest.Factories;
using Bit.Core;
using Bit.Core.Repositories;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.IntegrationTest;

/// <summary>
/// The access-audit trail over the real request pipeline: the append-only store's write and read paths, the
/// AccessEventLogs authorization at the endpoint, and the before/after collapse — all round-tripping through SQLite.
/// </summary>
/// <remarks>
/// The unit tests mock the store away, so this is where the store itself is exercised: that the display names are
/// snapshotted into the row at write time (the EF path resolves them in C#, the Dapper path with LEFT JOINs, and both
/// have to agree), and that a written event comes back through the endpoint in the documented shape.
/// </remarks>
public class AuditTrailTests(ApiApplicationFactory factory)
    : AccessRuleIntegrationTestBase(factory, "pam-audit-trail")
{
    private string AuditUrl => $"organizations/{Organization.Id}/audit";

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        // The substituted feature service is class-scoped, so the kill-switch test's flip outlives it and would
        // withdraw the trail from every test ordered after it. Reset here, alongside the base's own Pam flag, rather
        // than in that one test: the next test to flip a flag would otherwise reintroduce the same leak.
        FeatureService.IsEnabled(FeatureFlagKeys.PamDisableSqlAuditLogging).Returns(false);
        await LoginHelper.LoginAsync(OwnerEmail);
    }

    [Fact]
    public async Task Audit_AnEmptyStore_ReturnsAnEmptyList()
    {
        var trail = await GetJsonAsync(AuditUrl);

        Assert.Equal("list", trail["object"]!.GetValue<string>());
        Assert.Empty(trail["data"]!.AsArray());
    }

    [Fact]
    public async Task Audit_AWrittenEvent_ComesBackWithTheActorNameSnapshotted()
    {
        var owner = await Factory.GetService<IUserRepository>().GetByEmailAsync(OwnerEmail);
        var occurredAt = DateTime.UtcNow.AddMinutes(-1);
        await Factory.GetService<IAccessAuditEventRepository>().CreateAsync(new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.RequestApproved,
            Phase = AccessAuditEventPhase.Outcome,
            OccurredAt = occurredAt,
            OrganizationId = Organization.Id,
            ActorId = owner!.Id,
            RequesterId = owner.Id,
            Detail = "approved for the incident",
        });

        var trail = await GetJsonAsync(AuditUrl);

        var row = Assert.Single(trail["data"]!.AsArray())!;
        Assert.Equal("accessAuditEvent", row["object"]!.GetValue<string>());
        Assert.Equal("requestApproved", row["kind"]!.GetValue<string>());
        Assert.Equal(Organization.Id, row["organizationId"]!.GetValue<Guid>());
        Assert.Equal(owner.Id, row["actorId"]!.GetValue<Guid>());
        Assert.Equal("approved for the incident", row["detail"]!.GetValue<string>());
        // Resolved from the User row at write time and frozen into the event, not joined on read.
        Assert.Equal(owner.Email, row["actorEmail"]!.GetValue<string>());
        Assert.Equal(owner.Email, row["requesterEmail"]!.GetValue<string>());
        // A human actor performed it, and the outcome landed.
        Assert.False(row["automated"]!.GetValue<bool>());
        Assert.False(row["incomplete"]!.GetValue<bool>());
        // Serialized as UTC, so a client east or west of UTC reads the same instant.
        Assert.EndsWith("Z", row["occurredAt"]!.GetValue<string>());
    }

    // An action whose Outcome never landed collapses to its lone Attempt, flagged in-doubt rather than dropped.
    [Fact]
    public async Task Audit_APairAndAnOrphanAttempt_CollapseToOneRowEach()
    {
        var repository = Factory.GetService<IAccessAuditEventRepository>();
        var completed = new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.LeaseActivated,
            OccurredAt = DateTime.UtcNow.AddMinutes(-2),
            OrganizationId = Organization.Id,
        };
        await repository.CreateAsync(completed with { Phase = AccessAuditEventPhase.Attempt });
        await repository.CreateAsync(completed with { Phase = AccessAuditEventPhase.Outcome });
        await repository.CreateAsync(new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.LeaseRevoked,
            Phase = AccessAuditEventPhase.Attempt,
            OccurredAt = DateTime.UtcNow.AddMinutes(-1),
            OrganizationId = Organization.Id,
        });

        var rows = (await GetJsonAsync(AuditUrl))["data"]!.AsArray();

        Assert.Equal(2, rows.Count);
        // Newest first: the orphaned revoke attempt, then the completed activation.
        Assert.Equal("leaseRevoked", rows[0]!["kind"]!.GetValue<string>());
        Assert.True(rows[0]!["incomplete"]!.GetValue<bool>());
        Assert.Equal("leaseActivated", rows[1]!["kind"]!.GetValue<string>());
        Assert.False(rows[1]!["incomplete"]!.GetValue<bool>());
    }

    // Rule administration goes through the same store as the request/lease lifecycle. This is the end-to-end proof
    // that the three rule commands emit: create, rename, and delete one rule over HTTP and read the trail back.
    [Fact]
    public async Task Audit_RuleCreateUpdateDelete_AreRecordedWithTheRuleName()
    {
        var created = await Client.PostAsJsonAsync(
            $"organizations/{Organization.Id}/access-rules",
            new
            {
                name = "Production database",
                enabled = true,
                conditions = JsonNode.Parse("""[{"kind":"human_approval","approverCount":1}]"""),
                collections = Array.Empty<Guid>(),
            });
        created.EnsureSuccessStatusCode();
        var ruleId = (await created.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<Guid>();

        var renamed = await Client.PutAsJsonAsync(
            $"organizations/{Organization.Id}/access-rules/{ruleId}",
            new
            {
                name = "Production database (paused)",
                enabled = false,
                conditions = JsonNode.Parse("""[{"kind":"human_approval","approverCount":1}]"""),
                collections = Array.Empty<Guid>(),
            });
        renamed.EnsureSuccessStatusCode();

        var deleted = await Client.DeleteAsync($"organizations/{Organization.Id}/access-rules/{ruleId}");
        deleted.EnsureSuccessStatusCode();

        var rows = (await GetJsonAsync(AuditUrl))["data"]!.AsArray();

        var owner = await Factory.GetService<IUserRepository>().GetByEmailAsync(OwnerEmail);
        var byKind = rows.ToDictionary(row => row!["kind"]!.GetValue<string>(), row => row!);
        Assert.Equal(3, rows.Count);

        // The name is snapshotted per event, so the create and the rename each read as they were at the time.
        Assert.Equal("Production database", byKind["ruleCreated"]["ruleName"]!.GetValue<string>());
        Assert.Equal("Production database (paused)", byKind["ruleUpdated"]["ruleName"]!.GetValue<string>());
        // The rule row is gone, so the delete event is the only thing that still knows what it was called.
        Assert.Equal("Production database (paused)", byKind["ruleDeleted"]["ruleName"]!.GetValue<string>());

        Assert.All(byKind.Values, row =>
        {
            Assert.Equal(ruleId, row["ruleId"]!.GetValue<Guid>());
            Assert.Equal(owner!.Id, row["actorId"]!.GetValue<Guid>());
            Assert.False(row["automated"]!.GetValue<bool>());
            Assert.False(row["incomplete"]!.GetValue<bool>());
        });
    }

    // The trail is authorized by AccessEventLogs on the route organization; a caller outside it learns nothing.
    [Fact]
    public async Task Audit_AnOrganizationTheCallerIsNotIn_Returns404()
    {
        var response = await Client.GetAsync($"organizations/{Guid.NewGuid()}/audit");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // PM-42480: the kill switch, end to end. A rule round-trip that normally leaves three events behind leaves none,
    // and the trail itself is withdrawn rather than served as a record with a hole in it. Both halves matter — shedding
    // only the writes would keep serving a trail that quietly stopped being complete.
    [Fact]
    public async Task Audit_WithSqlAuditLoggingDisabled_WritesNothingAndWithdrawsTheTrail()
    {
        FeatureService.IsEnabled(FeatureFlagKeys.PamDisableSqlAuditLogging).Returns(true);

        var created = await Client.PostAsJsonAsync(
            $"organizations/{Organization.Id}/access-rules",
            new
            {
                name = "Production database",
                enabled = true,
                conditions = JsonNode.Parse("""[{"kind":"human_approval","approverCount":1}]"""),
                collections = Array.Empty<Guid>(),
            });
        // The rule write itself is untouched by the switch; only its audit side channel is.
        created.EnsureSuccessStatusCode();

        var stored = await Factory.GetService<IAccessAuditEventRepository>()
            .GetPageByOrganizationIdAsync(Organization.Id, new AccessAuditTrailFilter
            {
                Since = DateTime.UtcNow.AddDays(-1),
                Until = DateTime.UtcNow.AddMinutes(1),
                PageSize = 50,
            });
        Assert.Empty(stored);

        var trail = await Client.GetAsync(AuditUrl);
        Assert.Equal(HttpStatusCode.NotFound, trail.StatusCode);
    }

    // The filters reach the server now, so a narrowed request comes back narrowed rather than being sifted in the
    // browser. Applied to the row that survived the before/after collapse, which is why the refused activation below
    // answers to "leaseActivationRejected" and not to "leaseActivated".
    [Fact]
    public async Task Audit_WithAKindFilter_ReturnsOnlyTheMatchingRows()
    {
        var repository = Factory.GetService<IAccessAuditEventRepository>();
        var refused = new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.LeaseActivated,
            OccurredAt = DateTime.UtcNow.AddMinutes(-2),
            OrganizationId = Organization.Id,
        };
        await repository.CreateAsync(refused with { Phase = AccessAuditEventPhase.Attempt });
        await repository.CreateAsync(refused with
        {
            Kind = AccessAuditEventKind.LeaseActivationRejected,
            Phase = AccessAuditEventPhase.Outcome,
        });
        await repository.CreateAsync(new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.RequestSubmitted,
            Phase = AccessAuditEventPhase.Outcome,
            OccurredAt = DateTime.UtcNow.AddMinutes(-1),
            OrganizationId = Organization.Id,
        });

        var rejected = (await GetJsonAsync($"{AuditUrl}?kind=leaseActivationRejected"))["data"]!.AsArray();
        var activated = (await GetJsonAsync($"{AuditUrl}?kind=leaseActivated"))["data"]!.AsArray();
        var either = (await GetJsonAsync(
            $"{AuditUrl}?kind=leaseActivationRejected&kind=requestSubmitted"))["data"]!.AsArray();

        Assert.Equal("leaseActivationRejected", Assert.Single(rejected)!["kind"]!.GetValue<string>());
        Assert.Empty(activated);
        // Values within a dimension are OR-ed, because the chip driving them is multi-select.
        Assert.Equal(2, either.Count);
    }

    [Fact]
    public async Task Audit_WithARangeThatExcludesTheEvent_ReturnsNothing()
    {
        await Factory.GetService<IAccessAuditEventRepository>().CreateAsync(new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.RequestSubmitted,
            Phase = AccessAuditEventPhase.Outcome,
            OccurredAt = DateTime.UtcNow.AddDays(-10),
            OrganizationId = Organization.Id,
        });

        var inRange = (await GetJsonAsync($"{AuditUrl}?start={Iso(DateTime.UtcNow.AddDays(-11))}"))["data"]!.AsArray();
        var outOfRange = (await GetJsonAsync($"{AuditUrl}?start={Iso(DateTime.UtcNow.AddDays(-2))}"))["data"]!.AsArray();

        Assert.Single(inRange);
        Assert.Empty(outOfRange);
    }

    // A page that filled carries the position to resume from; the last one does not. A caller walking every page --
    // the CSV export does -- needs both halves of that to be true, or it stops early or never stops.
    [Fact]
    public async Task Audit_MoreThanOnePage_CarriesAContinuationTokenAndPagesThroughExactlyOnce()
    {
        var repository = Factory.GetService<IAccessAuditEventRepository>();
        var occurredAt = DateTime.UtcNow.AddMinutes(-5);
        var requestIds = new List<Guid>();
        for (var i = 0; i < 51; i++)
        {
            var requestId = Guid.NewGuid();
            requestIds.Add(requestId);
            await repository.CreateAsync(new AccessAuditEventData
            {
                Kind = AccessAuditEventKind.CredentialAccessed,
                Phase = AccessAuditEventPhase.Outcome,
                // Deliberately the same instant for all of them: the pathological case for a position keyed on time
                // alone, and an ordinary one here, since an action writes both its halves at one instant.
                OccurredAt = occurredAt,
                OrganizationId = Organization.Id,
                AccessRequestId = requestId,
            });
        }

        var seen = new List<Guid>();
        string? token = null;
        for (var page = 0; page < 10; page++)
        {
            var url = token == null ? AuditUrl : $"{AuditUrl}?continuationToken={Uri.EscapeDataString(token)}";
            var body = await GetJsonAsync(url);
            seen.AddRange(body["data"]!.AsArray().Select(row => row!["requestId"]!.GetValue<Guid>()));
            token = body["continuationToken"]?.GetValue<string>();
            if (token == null)
            {
                break;
            }
        }

        Assert.Null(token);
        Assert.Equal(requestIds.Count, seen.Count);
        Assert.Equal(requestIds.Order(), seen.Order());
    }

    [Fact]
    public async Task Audit_ASinglePage_CarriesNoContinuationToken()
    {
        await Factory.GetService<IAccessAuditEventRepository>().CreateAsync(new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.RequestSubmitted,
            Phase = AccessAuditEventPhase.Outcome,
            OccurredAt = DateTime.UtcNow.AddMinutes(-1),
            OrganizationId = Organization.Id,
        });

        var body = await GetJsonAsync(AuditUrl);

        Assert.Single(body["data"]!.AsArray());
        Assert.True(body["continuationToken"] == null || body["continuationToken"]!.GetValue<string?>() == null);
    }

    // Refused rather than ignored. A filter the server did not understand, answered with an unfiltered trail, reads
    // as "this never happened"; a token it did not issue, answered with the first page, loops a paging caller forever.
    [Theory]
    [InlineData("?kind=notAKind")]
    [InlineData("?continuationToken=forged")]
    public async Task Audit_WithAParameterItCannotHonour_Returns400(string query)
    {
        var response = await Client.GetAsync($"{AuditUrl}{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Nothing may reach past the retention window, so a range wider than it is a request the store cannot answer.
    [Fact]
    public async Task Audit_WithARangeWiderThanRetention_Returns400()
    {
        var response = await Client.GetAsync(
            $"{AuditUrl}?start={Iso(DateTime.UtcNow.AddDays(-120))}&end={Iso(DateTime.UtcNow)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // The Item filter's menu, end to end. It exists because neither obvious source works: a page of the trail names
    // only some of the items in range, and the caller's vault holds credentials the trail never mentions.
    [Fact]
    public async Task AuditItems_NamesEverySubjectTheTrailCarries_OncePerSubject()
    {
        var repository = Factory.GetService<IAccessAuditEventRepository>();
        var cipherId = Guid.NewGuid();
        var collectionId = Guid.NewGuid();
        var ruleId = Guid.NewGuid();

        // The same cipher twice, so a duplicate would surface as two options for one item.
        foreach (var kind in new[] { AccessAuditEventKind.LeaseActivated, AccessAuditEventKind.LeaseRevoked })
        {
            await repository.CreateAsync(new AccessAuditEventData
            {
                Kind = kind,
                Phase = AccessAuditEventPhase.Outcome,
                OccurredAt = DateTime.UtcNow.AddMinutes(-2),
                OrganizationId = Organization.Id,
                CipherId = cipherId,
                CollectionId = collectionId,
            });
        }
        await repository.CreateAsync(new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.RuleCreated,
            Phase = AccessAuditEventPhase.Outcome,
            OccurredAt = DateTime.UtcNow.AddMinutes(-1),
            OrganizationId = Organization.Id,
            AccessRuleId = ruleId,
            RuleName = "Production database",
        });

        var items = (await GetJsonAsync($"{AuditUrl}/items"))["data"]!.AsArray();

        Assert.Equal(2, items.Count);
        var cipher = Assert.Single(items, item => item!["cipherId"]?.GetValue<Guid?>() == cipherId)!;
        Assert.Equal(collectionId, cipher["collectionId"]!.GetValue<Guid>());
        // No cipher name: it is Vault Data the caller resolves from its own vault, so the menu carries only the id.
        Assert.True(cipher["ruleName"] == null || cipher["ruleName"]!.GetValue<string?>() == null);
        var rule = Assert.Single(items, item => item!["ruleId"]?.GetValue<Guid?>() == ruleId)!;
        // The rule's name is plaintext organization configuration, so it travels with the id.
        Assert.Equal("Production database", rule["ruleName"]!.GetValue<string>());
    }

    // The menu follows the time period the auditor chose, so it cannot offer an option the page can never match.
    [Fact]
    public async Task AuditItems_FollowsTheRange()
    {
        var repository = Factory.GetService<IAccessAuditEventRepository>();
        var recentCipherId = Guid.NewGuid();
        var oldCipherId = Guid.NewGuid();

        foreach (var (cipherId, occurredAt) in new[]
                 {
                     (recentCipherId, DateTime.UtcNow.AddMinutes(-1)),
                     (oldCipherId, DateTime.UtcNow.AddDays(-10)),
                 })
        {
            await repository.CreateAsync(new AccessAuditEventData
            {
                Kind = AccessAuditEventKind.LeaseActivated,
                Phase = AccessAuditEventPhase.Outcome,
                OccurredAt = occurredAt,
                OrganizationId = Organization.Id,
                CipherId = cipherId,
            });
        }

        var all = (await GetJsonAsync($"{AuditUrl}/items"))["data"]!.AsArray();
        var narrowed = (await GetJsonAsync(
            $"{AuditUrl}/items?start={Iso(DateTime.UtcNow.AddDays(-2))}"))["data"]!.AsArray();

        Assert.Equal(2, all.Count);
        Assert.Equal(recentCipherId, Assert.Single(narrowed)!["cipherId"]!.GetValue<Guid>());
    }

    // One Item selection spanning both columns asks for either, not for the empty intersection every other pair of
    // dimensions would give.
    [Fact]
    public async Task Audit_WithAnItemFilter_UnionsCiphersWithRules()
    {
        var repository = Factory.GetService<IAccessAuditEventRepository>();
        var cipherId = Guid.NewGuid();
        var ruleId = Guid.NewGuid();

        await repository.CreateAsync(new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.LeaseActivated,
            Phase = AccessAuditEventPhase.Outcome,
            OccurredAt = DateTime.UtcNow.AddMinutes(-2),
            OrganizationId = Organization.Id,
            CipherId = cipherId,
        });
        await repository.CreateAsync(new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.RuleCreated,
            Phase = AccessAuditEventPhase.Outcome,
            OccurredAt = DateTime.UtcNow.AddMinutes(-1),
            OrganizationId = Organization.Id,
            AccessRuleId = ruleId,
            RuleName = "Production database",
        });
        await repository.CreateAsync(new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.RequestSubmitted,
            Phase = AccessAuditEventPhase.Outcome,
            OccurredAt = DateTime.UtcNow,
            OrganizationId = Organization.Id,
        });

        var byCipher = (await GetJsonAsync($"{AuditUrl}?cipherId={cipherId}"))["data"]!.AsArray();
        var byRule = (await GetJsonAsync($"{AuditUrl}?ruleId={ruleId}"))["data"]!.AsArray();
        var byEither = (await GetJsonAsync($"{AuditUrl}?cipherId={cipherId}&ruleId={ruleId}"))["data"]!.AsArray();

        Assert.Equal(cipherId, Assert.Single(byCipher)!["cipherId"]!.GetValue<Guid>());
        Assert.Equal(ruleId, Assert.Single(byRule)!["ruleId"]!.GetValue<Guid>());
        Assert.Equal(2, byEither.Count);
    }

    // Guarded exactly as the trail is, because it describes the same records: it must not become a way to learn what
    // the trail itself would not disclose.
    [Fact]
    public async Task AuditItems_AnOrganizationTheCallerIsNotIn_Returns404()
    {
        var response = await Client.GetAsync($"organizations/{Guid.NewGuid()}/audit/items");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AuditItems_WithSqlAuditLoggingDisabled_WithdrawsTheMenuToo()
    {
        FeatureService.IsEnabled(FeatureFlagKeys.PamDisableSqlAuditLogging).Returns(true);

        var response = await Client.GetAsync($"{AuditUrl}/items");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AuditItems_WithARangeWiderThanRetention_Returns400()
    {
        var response = await Client.GetAsync(
            $"{AuditUrl}/items?start={Iso(DateTime.UtcNow.AddDays(-120))}&end={Iso(DateTime.UtcNow)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static string Iso(DateTime value) => Uri.EscapeDataString(value.ToString("O"));

    private async Task<JsonObject> GetJsonAsync(string url)
    {
        var response = await Client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonObject>())!;
    }
}
