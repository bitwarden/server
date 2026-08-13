using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.OrganizationFeatures.Queries;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Queries;

[SutProviderCustomize]
public class ListAccessAuditTrailQueryTests
{
    private static readonly DateTime _now = new(2026, 7, 3, 12, 0, 0, DateTimeKind.Utc);

    // A before/after pair (shared CorrelationId) collapses to a single row -- the Outcome (what actually happened).
    [Theory, BitAutoData]
    public async Task GetTrailAsync_CollapsesPairToOutcome(Guid organizationId)
    {
        var correlationId = Guid.NewGuid();
        var sutProvider = Setup(organizationId,
            Event(correlationId, AccessAuditEventKind.RequestApproved, AccessAuditEventPhase.Attempt, _now),
            Event(correlationId, AccessAuditEventKind.RequestApproved, AccessAuditEventPhase.Outcome, _now));

        var result = await sutProvider.Sut.GetTrailAsync(organizationId);

        var row = Assert.Single(result);
        Assert.Equal(AccessAuditEventPhase.Outcome, row.Phase);
    }

    // An action whose Outcome never landed collapses to its lone Attempt (the response flags it in-doubt).
    [Theory, BitAutoData]
    public async Task GetTrailAsync_OrphanAttempt_ReturnsTheAttempt(Guid organizationId)
    {
        var sutProvider = Setup(organizationId,
            Event(Guid.NewGuid(), AccessAuditEventKind.LeaseActivated, AccessAuditEventPhase.Attempt, _now));

        var result = await sutProvider.Sut.GetTrailAsync(organizationId);

        var row = Assert.Single(result);
        Assert.Equal(AccessAuditEventPhase.Attempt, row.Phase);
    }

    // Distinct correlation ids stay distinct rows (e.g. an auto-approved submit: submitted + approved), newest first.
    [Theory, BitAutoData]
    public async Task GetTrailAsync_KeepsDistinctActions_NewestFirst(Guid organizationId)
    {
        var submitted = Guid.NewGuid();
        var approved = Guid.NewGuid();
        var sutProvider = Setup(organizationId,
            Event(submitted, AccessAuditEventKind.RequestSubmitted, AccessAuditEventPhase.Attempt, _now.AddMinutes(-1)),
            Event(submitted, AccessAuditEventKind.RequestSubmitted, AccessAuditEventPhase.Outcome, _now.AddMinutes(-1)),
            Event(approved, AccessAuditEventKind.RequestApproved, AccessAuditEventPhase.Outcome, _now));

        var result = (await sutProvider.Sut.GetTrailAsync(organizationId)).ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal(AccessAuditEventKind.RequestApproved, result[0].Kind); // newest first
        Assert.Equal(AccessAuditEventKind.RequestSubmitted, result[1].Kind);
    }

    private static SutProvider<ListAccessAuditTrailQuery> Setup(Guid organizationId, params AccessAuditEvent[] events)
    {
        var sutProvider = new SutProvider<ListAccessAuditTrailQuery>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        sutProvider.GetDependency<IAccessAuditEventRepository>()
            .GetManyByOrganizationIdAsync(organizationId, Arg.Any<DateTime>())
            .Returns(events);
        return sutProvider;
    }

    private static AccessAuditEvent Event(
        Guid correlationId, AccessAuditEventKind kind, AccessAuditEventPhase phase, DateTime occurredAt)
        => new()
        {
            CorrelationId = correlationId,
            Kind = kind,
            Phase = phase,
            OccurredAt = occurredAt,
        };
}
