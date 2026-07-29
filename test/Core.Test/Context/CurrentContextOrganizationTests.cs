using Bit.Core.Context;
using Bit.Core.Enums;
using Xunit;

namespace Bit.Core.Test.Context;

public class CurrentContextOrganizationTests
{
    [Theory]
    [InlineData(OrganizationUserType.Owner, true)]
    [InlineData(OrganizationUserType.Admin, true)]
    [InlineData(OrganizationUserType.User, false)]
    [InlineData(OrganizationUserType.Custom, false)]
    public void IsAdminOrOwner_ReturnsExpected(OrganizationUserType type, bool expected)
    {
        var organization = new CurrentContextOrganization { Type = type };

        Assert.Equal(expected, organization.IsAdminOrOwner);
    }
}
