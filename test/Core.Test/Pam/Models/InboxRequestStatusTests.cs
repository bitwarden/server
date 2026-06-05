using Bit.Core.Pam.Enums;
using Bit.Core.Pam.Models;
using Xunit;

namespace Bit.Core.Test.Pam.Models;

public class InboxRequestStatusTests
{
    [Theory]
    [InlineData(LeaseRequestStatus.Pending, false, InboxRequestStatus.Pending)]
    [InlineData(LeaseRequestStatus.Approved, false, InboxRequestStatus.Approved)]
    [InlineData(LeaseRequestStatus.Approved, true, InboxRequestStatus.Activated)]
    [InlineData(LeaseRequestStatus.Denied, false, InboxRequestStatus.Denied)]
    [InlineData(LeaseRequestStatus.Cancelled, false, InboxRequestStatus.Cancelled)]
    [InlineData(LeaseRequestStatus.ExpiredUnanswered, false, InboxRequestStatus.Expired)]
    public void From_MapsToFrontendVocabulary(LeaseRequestStatus status, bool hasLease, string expected)
    {
        Assert.Equal(expected, InboxRequestStatus.From(status, hasLease));
    }
}
