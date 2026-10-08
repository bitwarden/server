using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Xunit;

namespace Bit.Services.Pam.Test.Models;

public class PamRotationJobDetailsTests
{
    private static readonly DateTime _now = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _firstClaim = new(2026, 10, 5, 11, 40, 0, DateTimeKind.Utc);
    private static readonly DateTime _secondClaim = new(2026, 10, 5, 11, 50, 0, DateTimeKind.Utc);
    private static readonly Guid _accessConnectorId = Guid.NewGuid();

    [Fact]
    public void From_LiveClaim_SurfacesTheClaimAndItsAttemptExecutes()
    {
        var job = ClaimedJob(_firstClaim, expiresAt: _now.AddHours(1));

        var details = PamRotationJobDetails.From(job, [Attempt(job, _firstClaim)], _now);

        Assert.Equal(PamRotationJobStatus.Claimed, details.Status);
        Assert.Equal(_accessConnectorId, details.ClaimedByAccessConnectorId);
        Assert.Equal(_firstClaim, details.ClaimedAt);
        var attempt = Assert.Single(details.Attempts);
        Assert.Equal(PamRotationAttemptStatus.Executing, attempt.Status);
        Assert.Null(attempt.ResolvedDate);
    }

    [Fact]
    public void From_ClaimPastTheDeadline_TimesOutAndAbandonsTheAttemptAtTheDeadline()
    {
        // No sweep has written anything: the stored claim is intact, but the read reports TimedOut.
        var expiresAt = _now.AddMinutes(-5);
        var job = ClaimedJob(_firstClaim, expiresAt);

        var details = PamRotationJobDetails.From(job, [Attempt(job, _firstClaim)], _now);

        Assert.Equal(PamRotationJobStatus.TimedOut, details.Status);
        // The wire promises claim fields only while Claimed.
        Assert.Null(details.ClaimedByAccessConnectorId);
        Assert.Null(details.ClaimedAt);
        var attempt = Assert.Single(details.Attempts);
        Assert.Equal(PamRotationAttemptStatus.Abandoned, attempt.Status);
        Assert.Equal(expiresAt, attempt.ResolvedDate);
    }

    [Fact]
    public void From_ReleasedThenReclaimed_OnlyTheNewerAttemptExecutes()
    {
        var job = ClaimedJob(_secondClaim, expiresAt: _now.AddHours(1));
        var released = Attempt(job, _firstClaim);
        released.ResolvedDate = _secondClaim.AddMinutes(-1);

        var details = PamRotationJobDetails.From(job, [released, Attempt(job, _secondClaim)], _now);

        Assert.Equal(PamRotationAttemptStatus.Abandoned, details.Attempts[0].Status);
        // A release records when it ended; that stored date wins over any derived one.
        Assert.Equal(released.ResolvedDate, details.Attempts[0].ResolvedDate);
        Assert.Equal(PamRotationAttemptStatus.Executing, details.Attempts[1].Status);
    }

    [Fact]
    public void From_AccessConnectorScopedRead_AnotherAccessConnectorsClaimDoesNotMakeThisAttemptExecute()
    {
        // The access connector detail read only carries that connector's attempt, so it is the newest one in the list
        // even though another access connector has re-claimed the job since.
        var job = ClaimedJob(_secondClaim, expiresAt: _now.AddHours(1));
        job.ClaimedByAccessConnectorId = Guid.NewGuid();
        var mine = Attempt(job, _firstClaim);
        mine.ResolvedDate = _secondClaim.AddMinutes(-1);

        var details = PamRotationJobDetails.From(job, [mine], _now);

        Assert.Equal(PamRotationAttemptStatus.Abandoned, Assert.Single(details.Attempts).Status);
    }

    [Fact]
    public void From_RetriedJob_IsPendingAndTheErroredAttemptKeepsItsReport()
    {
        var job = new PamRotationJob
        {
            Id = Guid.NewGuid(),
            Action = PamRotationJobAction.None,
            NextClaimableAt = _now.AddMinutes(1),
            ExpiresAt = _now.AddHours(1),
        };
        var errored = Attempt(job, _firstClaim);
        errored.Action = PamRotationAttemptAction.Errored;
        errored.ResolvedDate = _firstClaim.AddMinutes(1);

        var details = PamRotationJobDetails.From(job, [errored], _now);

        Assert.Equal(PamRotationJobStatus.Pending, details.Status);
        var attempt = Assert.Single(details.Attempts);
        Assert.Equal(PamRotationAttemptStatus.Errored, attempt.Status);
        Assert.Equal(errored.ResolvedDate, attempt.ResolvedDate);
    }

    private static PamRotationJob ClaimedJob(DateTime claimedAt, DateTime expiresAt) => new()
    {
        Id = Guid.NewGuid(),
        Action = PamRotationJobAction.Claimed,
        ClaimedByAccessConnectorId = _accessConnectorId,
        ClaimedAt = claimedAt,
        CreationDate = _firstClaim.AddMinutes(-10),
        NextClaimableAt = _firstClaim.AddMinutes(-10),
        ExpiresAt = expiresAt,
    };

    private static PamRotationAttempt Attempt(PamRotationJob job, DateTime claimedAt) => new()
    {
        Id = Guid.NewGuid(),
        JobId = job.Id,
        ClaimedByAccessConnectorId = _accessConnectorId,
        Action = PamRotationAttemptAction.None,
        CreationDate = claimedAt,
    };
}
