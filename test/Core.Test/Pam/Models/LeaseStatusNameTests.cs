using Bit.Core.Pam.Enums;
using Bit.Core.Pam.Models;
using Xunit;

namespace Bit.Core.Test.Pam.Models;

public class LeaseStatusNameTests
{
    [Theory]
    [InlineData(LeaseStatus.Active, LeaseStatusName.Active)]
    [InlineData(LeaseStatus.Expired, LeaseStatusName.Expired)]
    [InlineData(LeaseStatus.Revoked, LeaseStatusName.Revoked)]
    public void From_MapsToFrontendVocabulary(LeaseStatus status, string expected)
    {
        Assert.Equal(expected, LeaseStatusName.From(status));
    }
}
