using Bit.Pam.Enums;
using Xunit;

namespace Bit.Services.Pam.Test.Enums;

public class AccessStatusDerivationTests
{
    private static readonly DateTime _now = new(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _open = new(2026, 8, 27, 13, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _lapsed = new(2026, 8, 27, 11, 0, 0, DateTimeKind.Utc);

    // The full derivation table for requests: the recorded action interpreted against the clock, with the two sticky
    // Approved carve-outs (activated, applied extension) and the two origins of Expired.

    [Fact]
    public void ComputeStatus_NoneOpenWindow_IsPending() =>
        Assert.Equal(AccessRequestStatus.Pending,
            AccessStatusDerivation.ComputeStatus(AccessRequestAction.None, false, false, _open, _now));

    [Fact]
    public void ComputeStatus_NoneLapsedWindow_IsExpired() =>
        Assert.Equal(AccessRequestStatus.Expired,
            AccessStatusDerivation.ComputeStatus(AccessRequestAction.None, false, false, _lapsed, _now));

    [Fact]
    public void ComputeStatus_ApprovedOpenWindow_IsApproved() =>
        Assert.Equal(AccessRequestStatus.Approved,
            AccessStatusDerivation.ComputeStatus(AccessRequestAction.Approved, false, false, _open, _now));

    [Fact]
    public void ComputeStatus_ApprovedUnactivatedLapsedWindow_IsExpired() =>
        Assert.Equal(AccessRequestStatus.Expired,
            AccessStatusDerivation.ComputeStatus(AccessRequestAction.Approved, false, false, _lapsed, _now));

    [Fact]
    public void ComputeStatus_ApprovedWithLease_CannotLapseOutOfApproved() =>
        // An activated request's story continues on its lease; the lease's own end is the lease read's business.
        Assert.Equal(AccessRequestStatus.Approved,
            AccessStatusDerivation.ComputeStatus(AccessRequestAction.Approved, true, false, _lapsed, _now));

    [Fact]
    public void ComputeStatus_ApprovedExtension_CannotLapseOutOfApproved() =>
        // An applied extension finished its work at creation (the parent lease's end moved in place). The client's
        // extensionsByLeaseId folding filters on approved, so this carve-out is load-bearing.
        Assert.Equal(AccessRequestStatus.Approved,
            AccessStatusDerivation.ComputeStatus(AccessRequestAction.Approved, false, true, _lapsed, _now));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ComputeStatus_Denied_RecordedFactBeatsTheClock(bool windowOpen) =>
        Assert.Equal(AccessRequestStatus.Denied,
            AccessStatusDerivation.ComputeStatus(AccessRequestAction.Denied, false, false,
                windowOpen ? _open : _lapsed, _now));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ComputeStatus_Cancelled_RecordedFactBeatsTheClock(bool windowOpen) =>
        Assert.Equal(AccessRequestStatus.Cancelled,
            AccessStatusDerivation.ComputeStatus(AccessRequestAction.Cancelled, false, false,
                windowOpen ? _open : _lapsed, _now));

    [Fact]
    public void ComputeStatus_WindowEndIsExclusive_BoundaryInstantIsExpired() =>
        // NotAfter is exclusive everywhere (active reads use NotAfter > now), so the boundary instant is outside.
        Assert.Equal(AccessRequestStatus.Expired,
            AccessStatusDerivation.ComputeStatus(AccessRequestAction.None, false, false, _now, _now));

    [Fact]
    public void ComputeStatus_UnknownAction_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AccessStatusDerivation.ComputeStatus((AccessRequestAction)99, false, false, _open, _now));

    // The lease table: an early end beats the clock; only an untouched lease is the clock's to judge.

    [Fact]
    public void ComputeLeaseStatus_NoneOpenWindow_IsActive() =>
        Assert.Equal(AccessLeaseStatus.Active,
            AccessStatusDerivation.ComputeLeaseStatus(AccessLeaseAction.None, _open, _now));

    [Fact]
    public void ComputeLeaseStatus_NoneLapsedWindow_IsExpired() =>
        Assert.Equal(AccessLeaseStatus.Expired,
            AccessStatusDerivation.ComputeLeaseStatus(AccessLeaseAction.None, _lapsed, _now));

    [Fact]
    public void ComputeLeaseStatus_WindowEndIsExclusive_BoundaryInstantIsExpired() =>
        Assert.Equal(AccessLeaseStatus.Expired,
            AccessStatusDerivation.ComputeLeaseStatus(AccessLeaseAction.None, _now, _now));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ComputeLeaseStatus_Revoked_EndedEarlyEndedEarly(bool windowOpen) =>
        Assert.Equal(AccessLeaseStatus.Revoked,
            AccessStatusDerivation.ComputeLeaseStatus(AccessLeaseAction.Revoked, windowOpen ? _open : _lapsed, _now));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ComputeLeaseStatus_Cancelled_EndedEarlyEndedEarly(bool windowOpen) =>
        Assert.Equal(AccessLeaseStatus.Cancelled,
            AccessStatusDerivation.ComputeLeaseStatus(AccessLeaseAction.Cancelled, windowOpen ? _open : _lapsed, _now));

    [Fact]
    public void ComputeLeaseStatus_UnknownAction_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AccessStatusDerivation.ComputeLeaseStatus((AccessLeaseAction)99, _open, _now));
}
