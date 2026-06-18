using Bit.Api.Pam.Models.Response;
using Bit.Pam.Enums;
using Xunit;

namespace Bit.Commercial.Pam.Test.Api.Models;

public class AccessRequestStatusNamesTests
{
    [Theory]
    [InlineData(AccessRequestStatus.Pending, false, AccessRequestStatusNames.Pending)]
    [InlineData(AccessRequestStatus.Approved, false, AccessRequestStatusNames.Approved)]
    [InlineData(AccessRequestStatus.Approved, true, AccessRequestStatusNames.Activated)]
    [InlineData(AccessRequestStatus.Denied, false, AccessRequestStatusNames.Denied)]
    [InlineData(AccessRequestStatus.Cancelled, false, AccessRequestStatusNames.Cancelled)]
    [InlineData(AccessRequestStatus.ExpiredUnanswered, false, AccessRequestStatusNames.Expired)]
    public void From_MapsToFrontendVocabulary(AccessRequestStatus status, bool hasLease, string expected)
    {
        Assert.Equal(expected, AccessRequestStatusNames.From(status, hasLease));
    }
}
