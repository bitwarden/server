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
            .GetManyByOrganizationIdAsync(Organization.Id, DateTime.UtcNow.AddDays(-1));
        Assert.Empty(stored);

        var trail = await Client.GetAsync(AuditUrl);
        Assert.Equal(HttpStatusCode.NotFound, trail.StatusCode);
    }

    private async Task<JsonObject> GetJsonAsync(string url)
    {
        var response = await Client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonObject>())!;
    }
}
