using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Models.Request.Organizations;

public class OrganizationUserUpdateRequestModelTests
{
    [Theory]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public void ToOrganizationUser_NonCustomRole_ClearsPermissions(OrganizationUserType type)
    {
        var existingUser = new OrganizationUser { Type = OrganizationUserType.Custom };
        existingUser.SetPermissions(new Permissions { ManageUsers = true });

        var model = new OrganizationUserUpdateRequestModel
        {
            Type = type,
            Permissions = new Permissions { ManageUsers = true }
        };

        var result = model.ToOrganizationUser(existingUser);

        Assert.Null(result.Permissions);
    }

    [Fact]
    public void ToOrganizationUser_CustomRole_SetsPermissions()
    {
        var existingUser = new OrganizationUser { Type = OrganizationUserType.User };

        var model = new OrganizationUserUpdateRequestModel
        {
            Type = OrganizationUserType.Custom,
            Permissions = new Permissions { ManageUsers = true }
        };

        var result = model.ToOrganizationUser(existingUser);

        Assert.True(result.GetPermissions()!.ManageUsers);
    }
}
