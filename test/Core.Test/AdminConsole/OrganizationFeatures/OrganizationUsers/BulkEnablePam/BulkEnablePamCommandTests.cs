using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.BulkEnablePam;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.PrivilegedControlsSeats;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RestoreUser.v1;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUser.v2;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
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
        sutProvider.GetDependency<IReservePrivilegedControlsSeatsCommand>()
            .RunAsync(organization, Arg.Any<int>())
            .Returns(PrivilegedControlsSeatReservation.SelfHostedShortfall);

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
        sutProvider.GetDependency<IReservePrivilegedControlsSeatsCommand>()
            .RunAsync(organization, Arg.Any<int>())
            .Returns(PrivilegedControlsSeatReservation.Reserved);

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
        sutProvider.GetDependency<IReservePrivilegedControlsSeatsCommand>()
            .RunAsync(organization, Arg.Any<int>())
            .Returns(PrivilegedControlsSeatReservation.Reserved);

        var result = await sutProvider.Sut.BulkEnablePamAsync(organization, organizationUserIds);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .ReplaceManyAsync(Arg.Is<IEnumerable<OrganizationUser>>(users => users.All(u => u.AccessPam)));
    }

    [Theory]
    [BitAutoData]
    public async Task BulkEnablePamAsync_MixedStatuses_ReservesOnlySeatUsingMembers(Organization organization,
        List<Guid> organizationUserIds, SutProvider<BulkEnablePamCommand> sutProvider)
    {
        organization.UsePam = true;
        var statuses = new[]
        {
            OrganizationUserStatusType.Confirmed, OrganizationUserStatusType.Confirmed,
            OrganizationUserStatusType.Invited, OrganizationUserStatusType.Revoked, OrganizationUserStatusType.Staged
        };
        var organizationUsers = statuses.Select(status => new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            AccessPam = false,
            Status = status
        }).ToList();
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyAsync(organizationUserIds)
            .Returns(organizationUsers);
        sutProvider.GetDependency<IReservePrivilegedControlsSeatsCommand>()
            .RunAsync(organization, 3)
            .Returns(PrivilegedControlsSeatReservation.Reserved);

        var result = await sutProvider.Sut.BulkEnablePamAsync(organization, organizationUserIds);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IReservePrivilegedControlsSeatsCommand>()
            .Received(1)
            .RunAsync(organization, 3);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .ReplaceManyAsync(Arg.Is<IEnumerable<OrganizationUser>>(users =>
                users.Count() == 5 && users.All(u => u.AccessPam)));
    }

    [Theory]
    [BitAutoData]
    public async Task BulkEnablePamAsync_AtLimit_ReturnsSeatLimitReachedAndSavesNothing(Organization organization,
        List<Guid> organizationUserIds, List<OrganizationUser> organizationUsers,
        SutProvider<BulkEnablePamCommand> sutProvider)
    {
        organization.UsePam = true;
        foreach (var organizationUser in organizationUsers)
        {
            organizationUser.OrganizationId = organization.Id;
            organizationUser.AccessPam = false;
            organizationUser.Status = OrganizationUserStatusType.Confirmed;
        }

        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyAsync(organizationUserIds)
            .Returns(organizationUsers);
        sutProvider.GetDependency<IReservePrivilegedControlsSeatsCommand>()
            .RunAsync(organization, organizationUsers.Count)
            .Returns(PrivilegedControlsSeatReservation.LimitReached);

        var result = await sutProvider.Sut.BulkEnablePamAsync(organization, organizationUserIds);

        Assert.True(result.IsError);
        Assert.IsType<PrivilegedControlsSeatLimitReached>(result.AsError);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceManyAsync(default!);
    }

    [Theory]
    [BitAutoData]
    public async Task BulkEnablePamAsync_OnlyRevokedAndStaged_ReservesZero(Organization organization,
        List<Guid> organizationUserIds, SutProvider<BulkEnablePamCommand> sutProvider)
    {
        organization.UsePam = true;
        var organizationUsers = new[] { OrganizationUserStatusType.Revoked, OrganizationUserStatusType.Staged }
            .Select(status => new OrganizationUser
            {
                Id = Guid.NewGuid(),
                OrganizationId = organization.Id,
                AccessPam = false,
                Status = status
            }).ToList();
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyAsync(organizationUserIds)
            .Returns(organizationUsers);
        sutProvider.GetDependency<IReservePrivilegedControlsSeatsCommand>()
            .RunAsync(organization, 0)
            .Returns(PrivilegedControlsSeatReservation.Reserved);

        var result = await sutProvider.Sut.BulkEnablePamAsync(organization, organizationUserIds);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<IReservePrivilegedControlsSeatsCommand>()
            .Received(1)
            .RunAsync(organization, 0);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .ReplaceManyAsync(Arg.Is<IEnumerable<OrganizationUser>>(users =>
                users.Count() == 2 && users.All(u => u.AccessPam)));
    }
}
