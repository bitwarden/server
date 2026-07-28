using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
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

    [Fact]
    public void HasPermission_InvokesPickerAgainstPermissions()
    {
        var organization = new CurrentContextOrganization
        {
            Permissions = new Permissions { ManageUsers = true }
        };

        Assert.True(organization.HasPermission(p => p.ManageUsers));
        Assert.False(organization.HasPermission(p => p.ManageGroups));
    }
}
