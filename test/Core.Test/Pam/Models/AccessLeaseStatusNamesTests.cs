using Bit.Pam.Enums;
using Bit.Pam.Models;
using Xunit;

namespace Bit.Core.Test.Pam.Models;

public class AccessLeaseStatusNamesTests
{
    [Theory]
    [InlineData(AccessLeaseStatus.Active, AccessLeaseStatusNames.Active)]
    [InlineData(AccessLeaseStatus.Expired, AccessLeaseStatusNames.Expired)]
    [InlineData(AccessLeaseStatus.Revoked, AccessLeaseStatusNames.Revoked)]
    public void From_MapsToFrontendVocabulary(AccessLeaseStatus status, string expected)
    {
        Assert.Equal(expected, AccessLeaseStatusNames.From(status));
    }
}
