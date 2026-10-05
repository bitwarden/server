using Bit.Admin.Enums;
using Bit.Admin.Utilities;

namespace Admin.Test.Utilities;

public class RolePermissionMappingTests
{
    [Theory]
    [InlineData("owner")]
    [InlineData("admin")]
    [InlineData("billing")]
    [InlineData("sales")]
    public void OrgExtendTrial_GrantedToRole(string role) =>
        Assert.Contains(Permission.Org_ExtendTrial, RolePermissionMapping.RolePermissions[role]);

    [Fact]
    public void OrgExtendTrial_NotGrantedToCustomerSuccess() =>
        Assert.DoesNotContain(Permission.Org_ExtendTrial, RolePermissionMapping.RolePermissions["cs"]);
}
