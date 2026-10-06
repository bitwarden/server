using Bit.Pam;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Xunit;

namespace Bit.Services.Pam.Test;

public class PamRotationRulesTests
{
    private static readonly DateTime _now = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _claimedAt = new(2026, 10, 5, 11, 55, 0, DateTimeKind.Utc);

    private static PamRotationJob Job(
        PamRotationJobAction action = PamRotationJobAction.None, DateTime? nextClaimableAt = null,
        DateTime? expiresAt = null) => new()
        {
            Action = action,
            NextClaimableAt = nextClaimableAt ?? _now.AddMinutes(-1),
            ExpiresAt = expiresAt ?? _now.AddHours(1),
        };

    [Fact]
    public void IsClaimable_UnclaimedPastBackoffOnLiveConfig_IsClaimable() =>
        Assert.True(PamRotationRules.IsClaimable(Job(), new PamRotationConfig(), PamTargetSystemStatus.Active, _now));

    [Fact]
    public void IsClaimable_InsideBackoff_IsNotClaimable() =>
        Assert.False(PamRotationRules.IsClaimable(Job(nextClaimableAt: _now.AddMinutes(1)), new PamRotationConfig(),
            PamTargetSystemStatus.Active, _now));

    [Fact]
    public void IsClaimable_BackoffEndIsInclusive_BoundaryInstantIsClaimable() =>
        // Matches the claim's NextClaimableAt <= @Now.
        Assert.True(PamRotationRules.IsClaimable(Job(nextClaimableAt: _now), new PamRotationConfig(),
            PamTargetSystemStatus.Active, _now));

    [Fact]
    public void IsClaimable_PastTheDeadline_IsNotClaimable() =>
        // TimedOut before any sweep has recorded it: no longer Pending, so no longer claimable.
        Assert.False(PamRotationRules.IsClaimable(Job(expiresAt: _now), new PamRotationConfig(),
            PamTargetSystemStatus.Active, _now));

    [Fact]
    public void IsClaimable_PausedConfig_HoldsThePendingJob() =>
        Assert.False(PamRotationRules.IsClaimable(Job(), new PamRotationConfig { Enabled = false },
            PamTargetSystemStatus.Active, _now));

    [Fact]
    public void IsClaimable_DisabledTarget_HoldsThePendingJob() =>
        Assert.False(PamRotationRules.IsClaimable(Job(), new PamRotationConfig(), PamTargetSystemStatus.Disabled,
            _now));

    [Theory]
    [InlineData(PamRotationJobAction.Claimed)]
    [InlineData(PamRotationJobAction.Succeeded)]
    [InlineData(PamRotationJobAction.Failed)]
    public void IsClaimable_NotUnclaimed_IsNotClaimable(PamRotationJobAction action) =>
        Assert.False(PamRotationRules.IsClaimable(Job(action), new PamRotationConfig(), PamTargetSystemStatus.Active,
            _now));

    [Fact]
    public void IsCurrentAttempt_AttemptTheRecordedClaimCreated_IsCurrent()
    {
        var (job, attempt) = Claim();

        Assert.True(PamRotationRules.IsCurrentAttempt(job, attempt));
    }

    [Fact]
    public void IsCurrentAttempt_ClaimReleased_IsNotCurrent()
    {
        var (job, attempt) = Claim();
        job.ClaimedByAccessConnectorId = null;
        job.ClaimedAt = null;

        Assert.False(PamRotationRules.IsCurrentAttempt(job, attempt));
    }

    [Fact]
    public void IsCurrentAttempt_ReclaimedBySameAccessConnector_EarlierAttemptIsNotCurrent()
    {
        var (job, attempt) = Claim();
        job.ClaimedAt = _claimedAt.AddMinutes(1);

        Assert.False(PamRotationRules.IsCurrentAttempt(job, attempt));
    }

    [Fact]
    public void IsCurrentAttempt_SameInstantDifferentAccessConnector_IsNotCurrent()
    {
        var (job, attempt) = Claim();
        job.ClaimedByAccessConnectorId = Guid.NewGuid();

        Assert.False(PamRotationRules.IsCurrentAttempt(job, attempt));
    }

    private static (PamRotationJob Job, PamRotationAttempt Attempt) Claim()
    {
        var accessConnectorId = Guid.NewGuid();
        var job = Job(PamRotationJobAction.Claimed);
        job.Id = Guid.NewGuid();
        job.ClaimedByAccessConnectorId = accessConnectorId;
        job.ClaimedAt = _claimedAt;
        var attempt = new PamRotationAttempt
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            ClaimedByAccessConnectorId = accessConnectorId,
            CreationDate = _claimedAt,
        };
        return (job, attempt);
    }
}
