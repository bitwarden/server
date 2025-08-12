using Bit.Api.AdminConsole.Authorization.Requirements;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Core.Test.AdminConsole.Helpers;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Authorization.Requirements;

public class BasePermissionRequirementTests
{
    [Theory, BitAutoData]
    [CurrentContextOrganizationCustomize(Type = OrganizationUserType.Owner)]
    public async Task Authorizes_Owners(CurrentContextOrganization organizationClaims)
    {
        var result = await new PermissionRequirement().AuthorizeAsync(organizationClaims, () => Task.FromResult(false));
        Assert.True(result);
    }

    [Theory, BitAutoData]
    [CurrentContextOrganizationCustomize(Type = OrganizationUserType.Admin)]
    public async Task Authorizes_Admins(CurrentContextOrganization organizationClaims)
    {
        var result = await new PermissionRequirement().AuthorizeAsync(organizationClaims, () => Task.FromResult(false));
        Assert.True(result);
    }

    [Theory, BitAutoData]
    [CurrentContextOrganizationCustomize(Type = OrganizationUserType.User)]
    public async Task Authorizes_Providers(CurrentContextOrganization organizationClaims)
    {
        var result = await new PermissionRequirement().AuthorizeAsync(organizationClaims, () => Task.FromResult(true));
        Assert.True(result);
    }

    [Theory, BitAutoData]
    [CurrentContextOrganizationCustomize(Type = OrganizationUserType.Custom)]
    public async Task Authorizes_CustomPermission(CurrentContextOrganization organizationClaims)
    {
        organizationClaims.Permissions.ManageGroups = true;
        var result = await new TestCustomPermissionRequirement().AuthorizeAsync(organizationClaims, () => Task.FromResult(false));
        Assert.True(result);
    }

    [Theory, BitAutoData]
    [CurrentContextOrganizationCustomize(Type = OrganizationUserType.User)]
    public async Task DoesNotAuthorize_Users(CurrentContextOrganization organizationClaims)
    {
        var result = await new PermissionRequirement().AuthorizeAsync(organizationClaims, () => Task.FromResult(false));
        Assert.False(result);
    }

    [Theory, BitAutoData]
    [CurrentContextOrganizationCustomize(Type = OrganizationUserType.Custom)]
    public async Task DoesNotAuthorize_OtherCustomPermissions(CurrentContextOrganization organizationClaims)
    {
        organizationClaims.Permissions.ManageGroups = true;
        organizationClaims.Permissions = organizationClaims.Permissions.Invert();
        var result = await new TestCustomPermissionRequirement().AuthorizeAsync(organizationClaims, () => Task.FromResult(false));
        Assert.False(result);
    }

    private class PermissionRequirement() : BasePermissionRequirement(_ => false);
    private class TestCustomPermissionRequirement() : BasePermissionRequirement(p => p.ManageGroups);
}
