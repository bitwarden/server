using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.PrivilegedControlsSeats;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Organizations.Queries;
using Bit.Subscriptions.Organization.Commands.PrivilegedControls;
using Bitwarden.Server.Sdk.Environment;
using NSubstitute;
using Xunit;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;

namespace Bit.Subscriptions.Organization.Test.Commands;

public class ReservePrivilegedControlsSeatsCommandTests
{
    private readonly ICountNewPamSeatsRequiredQuery _countNewPamSeatsRequiredQuery =
        Substitute.For<ICountNewPamSeatsRequiredQuery>();
    private readonly IAutoscalePrivilegedControlsSeatsCommand _autoscalePrivilegedControlsSeatsCommand =
        Substitute.For<IAutoscalePrivilegedControlsSeatsCommand>();
    private readonly IBitwardenEnvironment _environment = Substitute.For<IBitwardenEnvironment>();
    private readonly ReservePrivilegedControlsSeatsCommand _sut;

    public ReservePrivilegedControlsSeatsCommandTests() =>
        _sut = new ReservePrivilegedControlsSeatsCommand(
            _countNewPamSeatsRequiredQuery,
            _autoscalePrivilegedControlsSeatsCommand,
            _environment);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task RunAsync_NoMembers_ReturnsReservedWithoutCallingAutoscale(int membersTakingSeats)
    {
        var organization = CreateOrganization();

        var result = await _sut.RunAsync(organization, membersTakingSeats);

        Assert.Equal(PrivilegedControlsSeatReservation.Reserved, result);
        await _autoscalePrivilegedControlsSeatsCommand.DidNotReceiveWithAnyArgs().Run(default!, default);
    }

    [Fact]
    public async Task RunAsync_PamOff_ReturnsReservedWithoutCallingAutoscale()
    {
        var organization = CreateOrganization(usePam: false);

        var result = await _sut.RunAsync(organization, 2);

        Assert.Equal(PrivilegedControlsSeatReservation.Reserved, result);
        await _autoscalePrivilegedControlsSeatsCommand.DidNotReceiveWithAnyArgs().Run(default!, default);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public async Task RunAsync_NoPamSeats_ReturnsReservedWithoutCallingAutoscale(int? pamSeats)
    {
        var organization = CreateOrganization(pamSeats: pamSeats);

        var result = await _sut.RunAsync(organization, 2);

        Assert.Equal(PrivilegedControlsSeatReservation.Reserved, result);
        await _autoscalePrivilegedControlsSeatsCommand.DidNotReceiveWithAnyArgs().Run(default!, default);
    }

    [Fact]
    public async Task RunAsync_SelfHosted_Shortfall_ReturnsSelfHostedShortfall()
    {
        var organization = CreateOrganization();
        _environment.SelfHosted.Returns(true);
        _countNewPamSeatsRequiredQuery.CountNewPamSeatsRequiredAsync(organization, 2).Returns(1);

        var result = await _sut.RunAsync(organization, 2);

        Assert.Equal(PrivilegedControlsSeatReservation.SelfHostedShortfall, result);
        await _autoscalePrivilegedControlsSeatsCommand.DidNotReceiveWithAnyArgs().Run(default!, default);
    }

    [Fact]
    public async Task RunAsync_SelfHosted_NoShortfall_ReturnsReserved()
    {
        var organization = CreateOrganization();
        _environment.SelfHosted.Returns(true);
        _countNewPamSeatsRequiredQuery.CountNewPamSeatsRequiredAsync(organization, 2).Returns(0);

        var result = await _sut.RunAsync(organization, 2);

        Assert.Equal(PrivilegedControlsSeatReservation.Reserved, result);
    }

    [Fact]
    public async Task RunAsync_Cloud_AutoscaleSucceeds_ReturnsReserved()
    {
        var organization = CreateOrganization();
        _autoscalePrivilegedControlsSeatsCommand.Run(organization, 3)
            .Returns(new AutoscalePrivilegedControlsSeatsResult(true, null));

        var result = await _sut.RunAsync(organization, 3);

        Assert.Equal(PrivilegedControlsSeatReservation.Reserved, result);
        await _autoscalePrivilegedControlsSeatsCommand.Received(1).Run(organization, 3);
    }

    [Fact]
    public async Task RunAsync_Cloud_AutoscaleAtLimit_ReturnsLimitReached()
    {
        var organization = CreateOrganization();
        _autoscalePrivilegedControlsSeatsCommand.Run(organization, 3)
            .Returns(new AutoscalePrivilegedControlsSeatsResult(false, 6));

        var result = await _sut.RunAsync(organization, 3);

        Assert.Equal(PrivilegedControlsSeatReservation.LimitReached, result);
    }

    private static OrganizationEntity CreateOrganization(bool usePam = true, int? pamSeats = 5) => new()
    {
        Id = Guid.NewGuid(),
        UsePam = usePam,
        PamSeats = pamSeats
    };
}
