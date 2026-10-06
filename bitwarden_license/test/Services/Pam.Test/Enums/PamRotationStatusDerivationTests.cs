using Bit.Pam.Enums;
using Xunit;

namespace Bit.Services.Pam.Test.Enums;

public class PamRotationStatusDerivationTests
{
    private static readonly DateTime _now = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _live = new(2026, 10, 5, 13, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _lapsed = new(2026, 10, 5, 11, 0, 0, DateTimeKind.Utc);

    // The job table: a recorded outcome beats the clock; an unresolved job times out whether claimed or not.

    [Fact]
    public void ComputeJobStatus_NoneBeforeDeadline_IsPending() =>
        Assert.Equal(PamRotationJobStatus.Pending,
            PamRotationStatusDerivation.ComputeJobStatus(PamRotationJobAction.None, _live, _now));

    [Fact]
    public void ComputeJobStatus_ClaimedBeforeDeadline_IsClaimed() =>
        Assert.Equal(PamRotationJobStatus.Claimed,
            PamRotationStatusDerivation.ComputeJobStatus(PamRotationJobAction.Claimed, _live, _now));

    [Theory]
    [InlineData(PamRotationJobAction.None)]
    [InlineData(PamRotationJobAction.Claimed)]
    public void ComputeJobStatus_UnresolvedPastDeadline_IsTimedOut(PamRotationJobAction action) =>
        // No sweep has to land first: the read reports TimedOut as soon as ExpiresAt passes.
        Assert.Equal(PamRotationJobStatus.TimedOut,
            PamRotationStatusDerivation.ComputeJobStatus(action, _lapsed, _now));

    [Theory]
    [InlineData(PamRotationJobAction.None)]
    [InlineData(PamRotationJobAction.Claimed)]
    public void ComputeJobStatus_DeadlineIsInclusive_BoundaryInstantIsTimedOut(PamRotationJobAction action) =>
        // Matches the timeout sweep's ExpiresAt <= @Now.
        Assert.Equal(PamRotationJobStatus.TimedOut,
            PamRotationStatusDerivation.ComputeJobStatus(action, _now, _now));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ComputeJobStatus_Succeeded_RecordedFactBeatsTheClock(bool beforeDeadline) =>
        Assert.Equal(PamRotationJobStatus.Succeeded,
            PamRotationStatusDerivation.ComputeJobStatus(PamRotationJobAction.Succeeded,
                beforeDeadline ? _live : _lapsed, _now));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ComputeJobStatus_Failed_RecordedFactBeatsTheClock(bool beforeDeadline) =>
        Assert.Equal(PamRotationJobStatus.Failed,
            PamRotationStatusDerivation.ComputeJobStatus(PamRotationJobAction.Failed,
                beforeDeadline ? _live : _lapsed, _now));

    [Fact]
    public void ComputeJobStatus_UnknownAction_Throws() =>
        // 4 is TimedOut's old stored byte, which the data migration must rewrite to None.
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PamRotationStatusDerivation.ComputeJobStatus((PamRotationJobAction)4, _live, _now));

    // The attempt table: a report beats everything; an unreported attempt executes only under its job's live claim.

    [Theory]
    [InlineData(PamRotationAttemptAction.Rotated, true, PamRotationJobStatus.Succeeded, PamRotationAttemptStatus.Rotated)]
    [InlineData(PamRotationAttemptAction.Errored, true, PamRotationJobStatus.Pending, PamRotationAttemptStatus.Errored)]
    [InlineData(PamRotationAttemptAction.Errored, true, PamRotationJobStatus.Failed, PamRotationAttemptStatus.Errored)]
    [InlineData(PamRotationAttemptAction.Errored, false, PamRotationJobStatus.Claimed, PamRotationAttemptStatus.Errored)]
    public void ComputeAttemptStatus_Reported_KeepsItsReport(PamRotationAttemptAction action, bool isCurrentAttempt,
        PamRotationJobStatus jobStatus, PamRotationAttemptStatus expected) =>
        // A retry or re-claim moves the job on, but the errored attempt stays Errored for the retry budget to count.
        Assert.Equal(expected, PamRotationStatusDerivation.ComputeAttemptStatus(action, isCurrentAttempt, jobStatus));

    [Fact]
    public void ComputeAttemptStatus_NoneCurrentOnClaimedJob_IsExecuting() =>
        Assert.Equal(PamRotationAttemptStatus.Executing,
            PamRotationStatusDerivation.ComputeAttemptStatus(PamRotationAttemptAction.None, true,
                PamRotationJobStatus.Claimed));

    [Fact]
    public void ComputeAttemptStatus_NoneOnReleasedJob_IsAbandoned() =>
        Assert.Equal(PamRotationAttemptStatus.Abandoned,
            PamRotationStatusDerivation.ComputeAttemptStatus(PamRotationAttemptAction.None, true,
                PamRotationJobStatus.Pending));

    [Fact]
    public void ComputeAttemptStatus_NoneOnTimedOutJob_IsAbandoned() =>
        Assert.Equal(PamRotationAttemptStatus.Abandoned,
            PamRotationStatusDerivation.ComputeAttemptStatus(PamRotationAttemptAction.None, true,
                PamRotationJobStatus.TimedOut));

    [Fact]
    public void ComputeAttemptStatus_NoneSupersededByReclaim_IsAbandoned() =>
        // Released, then re-claimed: the job is Claimed again, but by a newer attempt.
        Assert.Equal(PamRotationAttemptStatus.Abandoned,
            PamRotationStatusDerivation.ComputeAttemptStatus(PamRotationAttemptAction.None, false,
                PamRotationJobStatus.Claimed));

    [Fact]
    public void ComputeAttemptStatus_UnknownAction_Throws() =>
        // 3 is Abandoned's old stored byte, which the data migration must rewrite to None.
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PamRotationStatusDerivation.ComputeAttemptStatus((PamRotationAttemptAction)3, true,
                PamRotationJobStatus.Claimed));
}
