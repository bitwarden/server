using Bit.Services.Pam.Api.Models;
using Xunit;
using Domain = Bit.Pam.Enums;

namespace Bit.Services.Pam.Test.Api.Models;

public class DomainEnumMappingTests
{
    [Theory]
    [InlineData(Domain.AccessRequestStatus.Pending, false, AccessRequestStatus.Pending)]
    [InlineData(Domain.AccessRequestStatus.Approved, false, AccessRequestStatus.Approved)]
    [InlineData(Domain.AccessRequestStatus.Approved, true, AccessRequestStatus.Activated)]
    [InlineData(Domain.AccessRequestStatus.Denied, false, AccessRequestStatus.Denied)]
    [InlineData(Domain.AccessRequestStatus.Cancelled, false, AccessRequestStatus.Canceled)]
    [InlineData(Domain.AccessRequestStatus.ExpiredUnanswered, false, AccessRequestStatus.Expired)]
    public void ToApiStatus_MapsRequestStatusToWireContract(
        Domain.AccessRequestStatus status, bool hasLease, AccessRequestStatus expected)
    {
        Assert.Equal(expected, status.ToApiStatus(hasLease));
    }

    [Theory]
    [InlineData(Domain.AccessLeaseStatus.Active, AccessLeaseStatus.Active)]
    [InlineData(Domain.AccessLeaseStatus.Expired, AccessLeaseStatus.Expired)]
    [InlineData(Domain.AccessLeaseStatus.Revoked, AccessLeaseStatus.Revoked)]
    [InlineData(Domain.AccessLeaseStatus.Cancelled, AccessLeaseStatus.Cancelled)]
    public void ToApiStatus_MapsLeaseStatusToWireContract(
        Domain.AccessLeaseStatus status, AccessLeaseStatus expected)
    {
        Assert.Equal(expected, status.ToApiStatus());
    }

    [Theory]
    [InlineData(Domain.AccessDeciderKind.Automatic, DeciderKind.Automatic)]
    [InlineData(Domain.AccessDeciderKind.Human, DeciderKind.Human)]
    public void ToApiKind_MapsDeciderKindToWireContract(
        Domain.AccessDeciderKind kind, DeciderKind expected)
    {
        Assert.Equal(expected, kind.ToApiKind());
    }

    [Theory]
    [InlineData(Domain.AccessDecisionVerdict.Deny, AccessDecisionVerdict.Deny)]
    [InlineData(Domain.AccessDecisionVerdict.Approve, AccessDecisionVerdict.Approve)]
    public void ToApiVerdict_MapsVerdictToWireContract(
        Domain.AccessDecisionVerdict verdict, AccessDecisionVerdict expected)
    {
        Assert.Equal(expected, verdict.ToApiVerdict());
    }

    [Theory]
    [InlineData(AccessDecisionVerdict.Deny, Domain.AccessDecisionVerdict.Deny)]
    [InlineData(AccessDecisionVerdict.Approve, Domain.AccessDecisionVerdict.Approve)]
    public void ToDomainVerdict_MapsVerdictToDomain(
        AccessDecisionVerdict verdict, Domain.AccessDecisionVerdict expected)
    {
        Assert.Equal(expected, verdict.ToDomainVerdict());
    }
}
