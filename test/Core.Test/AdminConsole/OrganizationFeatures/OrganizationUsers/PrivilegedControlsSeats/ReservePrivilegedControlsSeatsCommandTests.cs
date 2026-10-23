using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.PrivilegedControlsSeats;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Organizations.Queries;
using Bit.Core.Enums;
using Bit.Core.Settings;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;
using SeatHelper = Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.PrivilegedControlsSeats.PrivilegedControlsSeats;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.PrivilegedControlsSeats;

[SutProviderCustomize]
public class ReservePrivilegedControlsSeatsCommandTests
{
    [Theory]
    [BitAutoData(0)]
    [BitAutoData(-1)]
    public async Task RunAsync_NoMembers_ReturnsReservedWithoutCallingAutoscale(int membersTakingSeats,
        Organization organization, SutProvider<ReservePrivilegedControlsSeatsCommand> sutProvider)
    {
        organization.UsePam = true;
        organization.PamSeats = 5;

        var result = await sutProvider.Sut.RunAsync(organization, membersTakingSeats);

        Assert.Equal(PrivilegedControlsSeatReservation.Reserved, result);
        await sutProvider.GetDependency<IAutoscalePrivilegedControlsSeatsCommand>()
            .DidNotReceiveWithAnyArgs().Run(default!, default);
    }

    [Theory]
    [BitAutoData]
    public async Task RunAsync_PamOff_ReturnsReservedWithoutCallingAutoscale(
        Organization organization, SutProvider<ReservePrivilegedControlsSeatsCommand> sutProvider)
    {
        organization.UsePam = false;
        organization.PamSeats = 5;

        var result = await sutProvider.Sut.RunAsync(organization, 2);

        Assert.Equal(PrivilegedControlsSeatReservation.Reserved, result);
        await sutProvider.GetDependency<IAutoscalePrivilegedControlsSeatsCommand>()
            .DidNotReceiveWithAnyArgs().Run(default!, default);
    }

    [Theory]
    [BitAutoData]
    public async Task RunAsync_PamSeatsNull_ReturnsReservedWithoutCallingAutoscale(
        Organization organization, SutProvider<ReservePrivilegedControlsSeatsCommand> sutProvider)
    {
        organization.UsePam = true;
        organization.PamSeats = null;

        var result = await sutProvider.Sut.RunAsync(organization, 2);

        Assert.Equal(PrivilegedControlsSeatReservation.Reserved, result);
        await sutProvider.GetDependency<IAutoscalePrivilegedControlsSeatsCommand>()
            .DidNotReceiveWithAnyArgs().Run(default!, default);
    }

    [Theory]
    [BitAutoData]
    public async Task RunAsync_SelfHosted_Shortfall_ReturnsSelfHostedShortfall(
        Organization organization, SutProvider<ReservePrivilegedControlsSeatsCommand> sutProvider)
    {
        organization.UsePam = true;
        organization.PamSeats = 5;
        sutProvider.GetDependency<IGlobalSettings>().SelfHosted.Returns(true);
        sutProvider.GetDependency<ICountNewPamSeatsRequiredQuery>()
            .CountNewPamSeatsRequiredAsync(organization, 2).Returns(1);

        var result = await sutProvider.Sut.RunAsync(organization, 2);

        Assert.Equal(PrivilegedControlsSeatReservation.SelfHostedShortfall, result);
        await sutProvider.GetDependency<IAutoscalePrivilegedControlsSeatsCommand>()
            .DidNotReceiveWithAnyArgs().Run(default!, default);
    }

    [Theory]
    [BitAutoData]
    public async Task RunAsync_SelfHosted_NoShortfall_ReturnsReserved(
        Organization organization, SutProvider<ReservePrivilegedControlsSeatsCommand> sutProvider)
    {
        organization.UsePam = true;
        organization.PamSeats = 5;
        sutProvider.GetDependency<IGlobalSettings>().SelfHosted.Returns(true);
        sutProvider.GetDependency<ICountNewPamSeatsRequiredQuery>()
            .CountNewPamSeatsRequiredAsync(organization, 2).Returns(0);

        var result = await sutProvider.Sut.RunAsync(organization, 2);

        Assert.Equal(PrivilegedControlsSeatReservation.Reserved, result);
    }

    [Theory]
    [BitAutoData]
    public async Task RunAsync_Cloud_AutoscaleSucceeds_ReturnsReserved(
        Organization organization, SutProvider<ReservePrivilegedControlsSeatsCommand> sutProvider)
    {
        organization.UsePam = true;
        organization.PamSeats = 5;
        sutProvider.GetDependency<IGlobalSettings>().SelfHosted.Returns(false);
        sutProvider.GetDependency<IAutoscalePrivilegedControlsSeatsCommand>()
            .Run(organization, 3).Returns(new AutoscalePrivilegedControlsSeatsResult(true, null));

        var result = await sutProvider.Sut.RunAsync(organization, 3);

        Assert.Equal(PrivilegedControlsSeatReservation.Reserved, result);
        await sutProvider.GetDependency<IAutoscalePrivilegedControlsSeatsCommand>()
            .Received(1).Run(organization, 3);
    }

    [Theory]
    [BitAutoData]
    public async Task RunAsync_Cloud_AutoscaleAtLimit_ReturnsLimitReached(
        Organization organization, SutProvider<ReservePrivilegedControlsSeatsCommand> sutProvider)
    {
        organization.UsePam = true;
        organization.PamSeats = 5;
        sutProvider.GetDependency<IGlobalSettings>().SelfHosted.Returns(false);
        sutProvider.GetDependency<IAutoscalePrivilegedControlsSeatsCommand>()
            .Run(organization, 3).Returns(new AutoscalePrivilegedControlsSeatsResult(false, 6));

        var result = await sutProvider.Sut.RunAsync(organization, 3);

        Assert.Equal(PrivilegedControlsSeatReservation.LimitReached, result);
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Invited, true)]
    [BitAutoData(OrganizationUserStatusType.Accepted, true)]
    [BitAutoData(OrganizationUserStatusType.Confirmed, true)]
    [BitAutoData(OrganizationUserStatusType.Revoked, false)]
    [BitAutoData(OrganizationUserStatusType.Staged, false)]
    public void OccupiesSeat_TrueOnlyForInvitedAcceptedConfirmed(OrganizationUserStatusType status, bool expected)
    {
        Assert.Equal(expected, SeatHelper.OccupiesSeat(status));
    }
}
