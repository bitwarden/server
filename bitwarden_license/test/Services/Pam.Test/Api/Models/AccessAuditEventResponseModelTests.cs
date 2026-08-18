using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Services.Pam.Api.Models.Response;
using Xunit;

namespace Bit.Services.Pam.Test.Api.Models;

public class AccessAuditEventResponseModelTests
{
    [Fact]
    public void Constructor_CarriesTheStoredFactsAndTheFrozenDisplayNames()
    {
        var auditEvent = new AccessAuditEvent
        {
            Kind = AccessAuditEventKind.LeaseRevoked,
            Phase = AccessAuditEventPhase.Outcome,
            OccurredAt = new DateTime(2026, 8, 18, 9, 30, 0),
            OrganizationId = Guid.NewGuid(),
            ActorId = Guid.NewGuid(),
            RequesterId = Guid.NewGuid(),
            CollectionId = Guid.NewGuid(),
            CipherId = Guid.NewGuid(),
            AccessRequestId = Guid.NewGuid(),
            AccessLeaseId = Guid.NewGuid(),
            AccessRuleId = Guid.NewGuid(),
            Detail = "revoked by the approver",
            LeaseNotBefore = new DateTime(2026, 8, 18, 9, 0, 0),
            LeaseNotAfter = new DateTime(2026, 8, 18, 10, 0, 0),
            ActorName = "Ada",
            ActorEmail = "ada@example.com",
            RequesterName = "Grace",
            RequesterEmail = "grace@example.com",
            CipherName = "2.encrypted-cipher-name",
            CollectionName = "2.encrypted-collection-name",
            RuleName = "Production database",
        };

        var model = new AccessAuditEventResponseModel(auditEvent);

        Assert.Equal("accessAuditEvent", model.Object);
        Assert.Equal(AccessAuditEventKindNames.LeaseRevoked, model.Kind);
        Assert.Equal(auditEvent.OrganizationId, model.OrganizationId);
        Assert.Equal(auditEvent.ActorId, model.ActorId);
        Assert.Equal(auditEvent.RequesterId, model.RequesterId);
        Assert.Equal(auditEvent.CollectionId, model.CollectionId);
        Assert.Equal(auditEvent.CipherId, model.CipherId);
        // The wire names drop the Access* prefix the domain carries.
        Assert.Equal(auditEvent.AccessRequestId, model.RequestId);
        Assert.Equal(auditEvent.AccessLeaseId, model.LeaseId);
        Assert.Equal(auditEvent.AccessRuleId, model.RuleId);
        Assert.Equal(auditEvent.Detail, model.Detail);
        Assert.Equal(auditEvent.ActorName, model.ActorName);
        Assert.Equal(auditEvent.ActorEmail, model.ActorEmail);
        Assert.Equal(auditEvent.RequesterName, model.RequesterName);
        Assert.Equal(auditEvent.RequesterEmail, model.RequesterEmail);
        Assert.Equal(auditEvent.CipherName, model.CipherName);
        Assert.Equal(auditEvent.CollectionName, model.CollectionName);
        Assert.Equal(auditEvent.RuleName, model.RuleName);
    }

    // Dapper materializes the stored timestamps with an unspecified kind; the response has to mark them UTC or a
    // client east/west of UTC parses them as local time and the instant shifts.
    [Fact]
    public void Constructor_MarksTimestampsAsUtc()
    {
        var model = new AccessAuditEventResponseModel(new AccessAuditEvent
        {
            Kind = AccessAuditEventKind.LeaseActivated,
            OccurredAt = new DateTime(2026, 8, 18, 9, 30, 0, DateTimeKind.Unspecified),
            LeaseNotBefore = new DateTime(2026, 8, 18, 9, 0, 0, DateTimeKind.Unspecified),
            LeaseNotAfter = new DateTime(2026, 8, 18, 10, 0, 0, DateTimeKind.Unspecified),
        });

        Assert.Equal(DateTimeKind.Utc, model.OccurredAt.Kind);
        Assert.Equal(DateTimeKind.Utc, model.LeaseNotBefore!.Value.Kind);
        Assert.Equal(DateTimeKind.Utc, model.LeaseNotAfter!.Value.Kind);
        // Relabelled, not shifted.
        Assert.Equal(9, model.OccurredAt.Hour);
    }

    // No actor means nobody performed it — a system or automatic event. Drives the client's automated filter.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Constructor_FlagsAutomatedFromTheAbsenceOfAnActor(bool hasActor)
    {
        var model = new AccessAuditEventResponseModel(new AccessAuditEvent
        {
            Kind = AccessAuditEventKind.RequestDenied,
            OccurredAt = new DateTime(2026, 8, 18, 9, 30, 0, DateTimeKind.Utc),
            ActorId = hasActor ? Guid.NewGuid() : null,
        });

        Assert.Equal(!hasActor, model.Automated);
    }

    // A row that is still an Attempt is an action whose outcome never landed: in doubt, not merely pending.
    [Theory]
    [InlineData(AccessAuditEventPhase.Attempt, true)]
    [InlineData(AccessAuditEventPhase.Outcome, false)]
    public void Constructor_FlagsIncompleteForALoneAttempt(AccessAuditEventPhase phase, bool expected)
    {
        var model = new AccessAuditEventResponseModel(new AccessAuditEvent
        {
            Kind = AccessAuditEventKind.LeaseExtended,
            Phase = phase,
            OccurredAt = new DateTime(2026, 8, 18, 9, 30, 0, DateTimeKind.Utc),
        });

        Assert.Equal(expected, model.Incomplete);
    }

    [Fact]
    public void Constructor_ANullEvent_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new AccessAuditEventResponseModel(null!));
    }

    // Every kind in the domain enum has a wire name — the projection must not throw on a kind it can already store.
    [Fact]
    public void KindNames_CoverEveryDomainKind()
    {
        foreach (var kind in Enum.GetValues<AccessAuditEventKind>())
        {
            Assert.False(string.IsNullOrWhiteSpace(AccessAuditEventKindNames.From(kind)));
        }
    }
}
