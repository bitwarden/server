using Bit.Commercial.Pam.Api.Models.Response;
using Bit.Pam.Enums;
using Xunit;

namespace Bit.Commercial.Pam.Test.Api.Models;

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
