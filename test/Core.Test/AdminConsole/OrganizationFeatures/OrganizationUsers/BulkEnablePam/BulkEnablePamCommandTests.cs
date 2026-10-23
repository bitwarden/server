using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.BulkEnablePam;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RestoreUser.v1;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUser.v2;
using Bit.Core.Billing.Organizations.Queries;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.BulkEnablePam;

[SutProviderCustomize]
public class BulkEnablePamCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task BulkEnablePamAsync_GrantsAccessToMembersWithoutIt(Organization organization,
        List<Guid> organizationUserIds, List<OrganizationUser> organizationUsers,
        SutProvider<BulkEnablePamCommand> sutProvider)
    {
        organization.UsePam = true;
        foreach (var organizationUser in organizationUsers)
        {
            organizationUser.OrganizationId = organization.Id;
            organizationUser.AccessPam = false;
        }

        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyAsync(organizationUserIds)
            .Returns(organizationUsers);

        var result = await sutProvider.Sut.BulkEnablePamAsync(organization, organizationUserIds);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .ReplaceManyAsync(Arg.Is<IEnumerable<OrganizationUser>>(users => users.All(u => u.AccessPam)));
    }

    [Theory]
    [BitAutoData]
    public async Task BulkEnablePamAsync_SkipsMembersOfOtherOrganizationsAndThoseWithAccess(
        Organization organization, List<Guid> organizationUserIds, OrganizationUser targetUser,
        OrganizationUser alreadyEnabledUser, OrganizationUser otherOrgUser,
        SutProvider<BulkEnablePamCommand> sutProvider)
    {
        organization.UsePam = true;
        targetUser.OrganizationId = alreadyEnabledUser.OrganizationId = organization.Id;
        targetUser.AccessPam = false;
        alreadyEnabledUser.AccessPam = true;
        otherOrgUser.AccessPam = false;

        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyAsync(organizationUserIds)
            .Returns([targetUser, alreadyEnabledUser, otherOrgUser]);

        var result = await sutProvider.Sut.BulkEnablePamAsync(organization, organizationUserIds);

        Assert.True(result.IsSuccess);
        Assert.False(otherOrgUser.AccessPam);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .ReplaceManyAsync(Arg.Is<IEnumerable<OrganizationUser>>(users =>
                users.Count() == 1 && users.Single().Id == targetUser.Id && users.Single().AccessPam));
    }

    [Theory]
    [BitAutoData]
    public async Task BulkEnablePamAsync_WhenNoMembersNeedAccess_ReturnsUsersInvalid(Organization organization,
        List<Guid> organizationUserIds, List<OrganizationUser> organizationUsers,
        SutProvider<BulkEnablePamCommand> sutProvider)
    {
        organization.UsePam = true;
        foreach (var organizationUser in organizationUsers)
        {
            organizationUser.OrganizationId = organization.Id;
            organizationUser.AccessPam = true;
        }

        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyAsync(organizationUserIds)
            .Returns(organizationUsers);

        var result = await sutProvider.Sut.BulkEnablePamAsync(organization, organizationUserIds);

        Assert.True(result.IsError);
        Assert.IsType<UsersInvalid>(result.AsError);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceManyAsync(default!);
    }

    [Theory]
    [BitAutoData]
    public async Task BulkEnablePamAsync_WhenOrganizationDoesNotUsePam_ReturnsPamNotEnabled(
        Organization organization, List<Guid> organizationUserIds, List<OrganizationUser> organizationUsers,
        SutProvider<BulkEnablePamCommand> sutProvider)
    {
        organization.UsePam = false;
        foreach (var organizationUser in organizationUsers)
        {
            organizationUser.OrganizationId = organization.Id;
            organizationUser.AccessPam = false;
        }

        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyAsync(organizationUserIds)
            .Returns(organizationUsers);

        var result = await sutProvider.Sut.BulkEnablePamAsync(organization, organizationUserIds);

        Assert.True(result.IsError);
        Assert.IsType<PamNotEnabled>(result.AsError);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceManyAsync(default!);
    }

    [Theory]
    [BitAutoData]
    public async Task BulkEnablePamAsync_WhenSelfHostedAndSeatsRequired_ReturnsCannotAutoscale(
        Organization organization, List<Guid> organizationUserIds, List<OrganizationUser> organizationUsers,
        SutProvider<BulkEnablePamCommand> sutProvider)
    {
        organization.UsePam = true;
        foreach (var organizationUser in organizationUsers)
        {
            organizationUser.OrganizationId = organization.Id;
            organizationUser.AccessPam = false;
        }

        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyAsync(organizationUserIds)
            .Returns(organizationUsers);
        sutProvider.GetDependency<IGlobalSettings>().SelfHosted = true;
        sutProvider.GetDependency<ICountNewPamSeatsRequiredQuery>()
            .CountNewPamSeatsRequiredAsync(organization, organizationUsers.Count)
            .Returns(1);

        var result = await sutProvider.Sut.BulkEnablePamAsync(organization, organizationUserIds);

        Assert.True(result.IsError);
        Assert.IsType<CannotAutoscalePamSeatsOnSelfHost>(result.AsError);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceManyAsync(default!);
    }

    [Theory]
    [BitAutoData]
    public async Task BulkEnablePamAsync_WhenCloudAndSeatsRequired_Grants(Organization organization,
        List<Guid> organizationUserIds, List<OrganizationUser> organizationUsers,
        SutProvider<BulkEnablePamCommand> sutProvider)
    {
        organization.UsePam = true;
        foreach (var organizationUser in organizationUsers)
        {
            organizationUser.OrganizationId = organization.Id;
            organizationUser.AccessPam = false;
        }

        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyAsync(organizationUserIds)
            .Returns(organizationUsers);
        sutProvider.GetDependency<IGlobalSettings>().SelfHosted = false;
        sutProvider.GetDependency<ICountNewPamSeatsRequiredQuery>()
            .CountNewPamSeatsRequiredAsync(organization, organizationUsers.Count)
            .Returns(1);

        var result = await sutProvider.Sut.BulkEnablePamAsync(organization, organizationUserIds);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .ReplaceManyAsync(Arg.Is<IEnumerable<OrganizationUser>>(users => users.All(u => u.AccessPam)));
    }

    [Theory]
    [BitAutoData]
    public async Task BulkEnablePamAsync_WhenSelfHostedAndNoSeatsRequired_Grants(Organization organization,
        List<Guid> organizationUserIds, List<OrganizationUser> organizationUsers,
        SutProvider<BulkEnablePamCommand> sutProvider)
    {
        organization.UsePam = true;
        foreach (var organizationUser in organizationUsers)
        {
            organizationUser.OrganizationId = organization.Id;
            organizationUser.AccessPam = false;
        }

        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyAsync(organizationUserIds)
            .Returns(organizationUsers);
        sutProvider.GetDependency<IGlobalSettings>().SelfHosted = true;
        sutProvider.GetDependency<ICountNewPamSeatsRequiredQuery>()
            .CountNewPamSeatsRequiredAsync(organization, organizationUsers.Count)
            .Returns(0);

        var result = await sutProvider.Sut.BulkEnablePamAsync(organization, organizationUserIds);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .ReplaceManyAsync(Arg.Is<IEnumerable<OrganizationUser>>(users => users.All(u => u.AccessPam)));
    }
}
