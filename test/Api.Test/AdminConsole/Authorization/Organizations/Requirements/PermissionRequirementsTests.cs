using Bit.Api.AdminConsole.Authorization;
using Bit.Api.AdminConsole.Authorization.Requirements;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Core.Test.AdminConsole.Helpers;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Authorization.Requirements;

public class PermissionRequirementsTests
{
    /// <summary>
    /// Correlates each IOrganizationRequirement with its custom permission. If you add a new requirement,
    /// add a new entry here to have it automatically included in the tests below.
    /// </summary>
    public static IEnumerable<object[]> RequirementData => new List<object[]>
    {
        new object[] { new AccessEventLogsRequirement(), nameof(Permissions.AccessEventLogs) },
        new object[] { new AccessImportExportRequirement(), nameof(Permissions.AccessImportExport) },
        new object[] { new AccessReportsRequirement(), nameof(Permissions.AccessReports) },
        new object[] { new ManageAccountRecoveryRequirement(), nameof(Permissions.ManageResetPassword) },
        new object[] { new ManageGroupsRequirement(), nameof(Permissions.ManageGroups) },
        new object[] { new ManagePoliciesRequirement(), nameof(Permissions.ManagePolicies) },
        new object[] { new ManageScimRequirement(), nameof(Permissions.ManageScim) },
        new object[] { new ManageSsoRequirement(), nameof(Permissions.ManageSso) },
        new object[] { new ManageUsersRequirement(), nameof(Permissions.ManageUsers) },
    };

    [Theory]
    [BitMemberAutoData(nameof(RequirementData))]
    [CurrentContextOrganizationCustomize(Type = OrganizationUserType.User)]
    public async Task Authorizes_Provider(IOrganizationRequirement requirement, string _, CurrentContextOrganization organization)
    {
        var result = await requirement.AuthorizeAsync(organization, () => Task.FromResult(true));
        Assert.True(result);
    }

    [Theory]
    [BitMemberAutoData(nameof(RequirementData))]
    [CurrentContextOrganizationCustomize(Type = OrganizationUserType.Owner)]
    public async Task Authorizes_Owner(IOrganizationRequirement requirement, string _, CurrentContextOrganization organization)
    {
        var result = await requirement.AuthorizeAsync(organization, () => Task.FromResult(false));
        Assert.True(result);
    }

    [Theory]
    [BitMemberAutoData(nameof(RequirementData))]
    [CurrentContextOrganizationCustomize(Type = OrganizationUserType.Admin)]
    public async Task Authorizes_Admin(IOrganizationRequirement requirement, string _, CurrentContextOrganization organization)
    {
        var result = await requirement.AuthorizeAsync(organization, () => Task.FromResult(false));
        Assert.True(result);
    }

    [Theory]
    [BitMemberAutoData(nameof(RequirementData))]
    [CurrentContextOrganizationCustomize(Type = OrganizationUserType.Custom)]
    public async Task Authorizes_Custom_With_Correct_Permission(IOrganizationRequirement requirement, string permissionName, CurrentContextOrganization organization)
    {
        organization.Permissions.SetPermission(permissionName, true);
        var result = await requirement.AuthorizeAsync(organization, () => Task.FromResult(false));
        Assert.True(result);
    }

    [Theory]
    [BitMemberAutoData(nameof(RequirementData))]
    [CurrentContextOrganizationCustomize(Type = OrganizationUserType.Custom)]
    public async Task DoesNotAuthorize_Custom_With_Other_Permissions(IOrganizationRequirement requirement, string permissionName, CurrentContextOrganization organization)
    {
        organization.Permissions.SetPermission(permissionName, true);
        organization.Permissions = organization.Permissions.Invert();
        var result = await requirement.AuthorizeAsync(organization, () => Task.FromResult(false));
        Assert.False(result);
    }

    [Theory]
    [BitMemberAutoData(nameof(RequirementData))]
    [CurrentContextOrganizationCustomize(Type = OrganizationUserType.User)]
    public async Task DoesNotAuthorize_User(IOrganizationRequirement requirement, string _, CurrentContextOrganization organization)
    {
        var result = await requirement.AuthorizeAsync(organization, () => Task.FromResult(false));
        Assert.False(result);
    }
}
