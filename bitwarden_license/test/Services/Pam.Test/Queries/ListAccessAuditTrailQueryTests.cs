using Bit.Core.Exceptions;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.OrganizationFeatures.Queries;
using Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;
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

    private static DateTime RetentionFloor => _now.AddDays(-AccessHistoryWindow.RetentionDays);

    [Theory, BitAutoData]
    public async Task GetTrailAsync_WithNoBounds_ReadsTheWholeRetentionWindow(Guid organizationId)
    {
        var (sutProvider, filters) = Setup(organizationId);

        await sutProvider.Sut.GetTrailAsync(organizationId, new AccessAuditTrailQueryOptions());

        var filter = Assert.Single(filters);
        Assert.Equal(RetentionFloor, filter.Since);
        Assert.Equal(_now, filter.Until);
    }

    [Theory, BitAutoData]
    public async Task GetTrailAsync_ReadsOnePageAtTheFixedSize(Guid organizationId)
    {
        var (sutProvider, filters) = Setup(organizationId);

        await sutProvider.Sut.GetTrailAsync(organizationId, new AccessAuditTrailQueryOptions());

        Assert.Equal(ListAccessAuditTrailQuery.PageSize, Assert.Single(filters).PageSize);
    }

    [Theory, BitAutoData]
    public async Task GetTrailAsync_WithBoundsInsideTheWindow_PassesThemThrough(Guid organizationId)
    {
        var (sutProvider, filters) = Setup(organizationId);
        var start = _now.AddDays(-7);
        var end = _now.AddDays(-1);

        await sutProvider.Sut.GetTrailAsync(organizationId,
            new AccessAuditTrailQueryOptions { Start = start, End = end });

        var filter = Assert.Single(filters);
        Assert.Equal(start, filter.Since);
        Assert.Equal(end, filter.Until);
    }

    [Theory, BitAutoData]
    public async Task GetTrailAsync_StartBeyondRetention_IsClampedToTheWindow(Guid organizationId)
    {
        var (sutProvider, filters) = Setup(organizationId);

        await sutProvider.Sut.GetTrailAsync(organizationId, new AccessAuditTrailQueryOptions
        {
            Start = _now.AddDays(-AccessHistoryWindow.RetentionDays - 10),
            End = _now.AddDays(-AccessHistoryWindow.RetentionDays + 5),
        });

        Assert.Equal(RetentionFloor, Assert.Single(filters).Since);
    }

    [Theory, BitAutoData]
    public async Task GetTrailAsync_InvertedRange_IsSwapped(Guid organizationId)
    {
        var (sutProvider, filters) = Setup(organizationId);
        var earlier = _now.AddDays(-7);
        var later = _now.AddDays(-1);

        await sutProvider.Sut.GetTrailAsync(organizationId,
            new AccessAuditTrailQueryOptions { Start = later, End = earlier });

        var filter = Assert.Single(filters);
        Assert.Equal(earlier, filter.Since);
        Assert.Equal(later, filter.Until);
    }

    [Theory, BitAutoData]
    public async Task GetTrailAsync_RangeWiderThanRetention_ThrowsBadRequest(Guid organizationId)
    {
        var (sutProvider, _) = Setup(organizationId);

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.GetTrailAsync(organizationId,
            new AccessAuditTrailQueryOptions
            {
                Start = _now.AddDays(-AccessHistoryWindow.RetentionDays - 1),
                End = _now,
            }));
    }

    [Theory, BitAutoData]
    public async Task GetTrailAsync_PassesEveryDimensionToTheStore(Guid organizationId, Guid actorId,
        Guid requesterId, Guid cipherId, Guid ruleId)
    {
        var (sutProvider, filters) = Setup(organizationId);

        await sutProvider.Sut.GetTrailAsync(organizationId, new AccessAuditTrailQueryOptions
        {
            Kinds = [AccessAuditEventKind.LeaseRevoked],
            ActorIds = [actorId],
            IncludeAutomatedActor = true,
            RequesterIds = [requesterId],
            CipherIds = [cipherId],
            RuleIds = [ruleId],
        });

        var filter = Assert.Single(filters);
        Assert.Equal([AccessAuditEventKind.LeaseRevoked], filter.Kinds);
        Assert.Equal([actorId], filter.ActorIds);
        Assert.True(filter.IncludeAutomatedActor);
        Assert.Equal([requesterId], filter.RequesterIds);
        Assert.Equal([cipherId], filter.CipherIds);
        Assert.Equal([ruleId], filter.RuleIds);
    }

    [Theory, BitAutoData]
    public async Task GetTrailAsync_WithAResumePosition_PassesItToTheStore(Guid organizationId, Guid beforeId)
    {
        var (sutProvider, filters) = Setup(organizationId);
        var beforeOccurredAt = _now.AddHours(-3);

        await sutProvider.Sut.GetTrailAsync(organizationId,
            new AccessAuditTrailQueryOptions { Before = new AccessAuditEventCursor(beforeOccurredAt, beforeId) });

        var filter = Assert.Single(filters);
        Assert.Equal(new AccessAuditEventCursor(beforeOccurredAt, beforeId), filter.Before);
    }

    // The token names the last row, so same-instant events resume exactly.
    [Theory, BitAutoData]
    public async Task GetTrailAsync_FullPage_ReturnsATokenNamingTheLastRow(Guid organizationId)
    {
        var page = Enumerable.Range(0, ListAccessAuditTrailQuery.PageSize)
            .Select(i => Event(_now.AddMinutes(-i)))
            .ToArray();
        var (sutProvider, _) = Setup(organizationId, page);

        var result = await sutProvider.Sut.GetTrailAsync(organizationId, new AccessAuditTrailQueryOptions());

        Assert.NotNull(result.ContinuationToken);
        Assert.True(AccessAuditTrailContinuationToken.TryParse(
            result.ContinuationToken!, out var occurredAt, out var id));
        Assert.Equal(page[^1].OccurredDate, occurredAt);
        Assert.Equal(page[^1].Id, id);
    }

    [Theory, BitAutoData]
    public async Task GetTrailAsync_ShortPage_ReturnsNoContinuationToken(Guid organizationId)
    {
        var (sutProvider, _) = Setup(organizationId, Event(_now));

        var result = await sutProvider.Sut.GetTrailAsync(organizationId, new AccessAuditTrailQueryOptions());

        Assert.Single(result.Data);
        Assert.Null(result.ContinuationToken);
    }

    [Theory, BitAutoData]
    public async Task GetTrailAsync_EmptyPage_ReturnsNoContinuationToken(Guid organizationId)
    {
        var (sutProvider, _) = Setup(organizationId);

        var result = await sutProvider.Sut.GetTrailAsync(organizationId, new AccessAuditTrailQueryOptions());

        Assert.Empty(result.Data);
        Assert.Null(result.ContinuationToken);
    }

    private static (SutProvider<ListAccessAuditTrailQuery> SutProvider, List<AccessAuditTrailFilter> Filters) Setup(
        Guid organizationId, params AccessAuditEvent[] events)
    {
        var sutProvider = new SutProvider<ListAccessAuditTrailQuery>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);

        var filters = new List<AccessAuditTrailFilter>();
        sutProvider.GetDependency<IAccessAuditEventRepository>()
            .GetPageByOrganizationIdAsync(organizationId, Arg.Do<AccessAuditTrailFilter>(filters.Add))
            .Returns(events);

        return (sutProvider, filters);
    }

    private static AccessAuditEvent Event(DateTime occurredAt) => new()
    {
        Id = Guid.NewGuid(),
        CorrelationId = Guid.NewGuid(),
        Kind = AccessAuditEventKind.RequestApproved,
        Phase = AccessAuditEventPhase.Outcome,
        OccurredDate = occurredAt,
    };
}
