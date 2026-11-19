using Bit.Pam;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Xunit;

namespace Bit.Services.Pam.Test;

public class PamRotationRulesTests
{
    private static readonly DateTime _now = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _claimedDate = new(2026, 10, 5, 11, 55, 0, DateTimeKind.Utc);

    private static PamRotationJob Job(
        PamRotationJobAction action = PamRotationJobAction.None, DateTime? nextClaimableDate = null,
        DateTime? expirationDate = null) => new()
        {
            Action = action,
            NextClaimableDate = nextClaimableDate ?? _now.AddMinutes(-1),
            ExpirationDate = expirationDate ?? _now.AddHours(1),
        };

    [Fact]
    public void IsClaimable_UnclaimedPastBackoffOnLiveConfig_IsClaimable() =>
        Assert.True(PamRotationRules.IsClaimable(Job(), new PamRotationConfig(), PamTargetSystemStatus.Active, _now));

    [Fact]
    public void IsClaimable_InsideBackoff_IsNotClaimable() =>
        Assert.False(PamRotationRules.IsClaimable(Job(nextClaimableDate: _now.AddMinutes(1)), new PamRotationConfig(),
            PamTargetSystemStatus.Active, _now));

    [Fact]
    public void IsClaimable_BackoffEndIsInclusive_BoundaryInstantIsClaimable() =>
        // Matches the claim's NextClaimableDate <= @Now.
        Assert.True(PamRotationRules.IsClaimable(Job(nextClaimableDate: _now), new PamRotationConfig(),
            PamTargetSystemStatus.Active, _now));

    [Fact]
    public void IsClaimable_PastTheDeadline_IsNotClaimable() =>
        // TimedOut before any sweep has recorded it: no longer Pending, so no longer claimable.
        Assert.False(PamRotationRules.IsClaimable(Job(expirationDate: _now), new PamRotationConfig(),
            PamTargetSystemStatus.Active, _now));

    [Fact]
    public void IsClaimable_PausedConfig_HoldsThePendingJob() =>
        Assert.False(PamRotationRules.IsClaimable(Job(), new PamRotationConfig { Enabled = false },
            PamTargetSystemStatus.Active, _now));

    [Fact]
    public void IsClaimable_DisabledTarget_HoldsThePendingJob() =>
        Assert.False(PamRotationRules.IsClaimable(Job(), new PamRotationConfig(), PamTargetSystemStatus.Disabled,
            _now));

    [Fact]
    public void IsClaimable_PendingSealTarget_HoldsThePendingJob() =>
        Assert.False(PamRotationRules.IsClaimable(Job(), new PamRotationConfig(), PamTargetSystemStatus.PendingSeal,
            _now));

    [Fact]
    public void CanOffer_PendingSealTarget_IsNotOfferable() =>
        Assert.False(PamRotationRules.CanOffer(
            new PamRotationConfig { Enabled = true }, PamTargetSystemMethod.Automatic, PamTargetSystemStatus.PendingSeal));

    [Fact]
    public void CanOffer_ActiveAutomaticEnabledTarget_IsOfferable() =>
        Assert.True(PamRotationRules.CanOffer(
            new PamRotationConfig { Enabled = true }, PamTargetSystemMethod.Automatic, PamTargetSystemStatus.Active));

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
        job.ClaimedDate = null;

        Assert.False(PamRotationRules.IsCurrentAttempt(job, attempt));
    }

    [Fact]
    public void IsCurrentAttempt_ReclaimedBySameAccessConnector_EarlierAttemptIsNotCurrent()
    {
        var (job, attempt) = Claim();
        job.ClaimedDate = _claimedDate.AddMinutes(1);

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
        job.ClaimedDate = _claimedDate;
        var attempt = new PamRotationAttempt
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            ClaimedByAccessConnectorId = accessConnectorId,
            CreationDate = _claimedDate,
        };
        return (job, attempt);
    }
}
