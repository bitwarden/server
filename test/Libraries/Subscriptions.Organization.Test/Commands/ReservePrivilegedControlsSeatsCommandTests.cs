using Bit.Core;
using Bit.Core.Billing.Organizations;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Organizations.Queries;
using Bit.Core.Exceptions;
using Bit.Subscriptions.Organization.Commands.PrivilegedControls;
using Bitwarden.Server.Sdk.Environment;
using Bitwarden.Server.Sdk.Features;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
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
    private readonly IFeatureService _featureService = Substitute.For<IFeatureService>();
    private readonly RecordingLogger<ReservePrivilegedControlsSeatsCommand> _logger = new();
    private readonly ReservePrivilegedControlsSeatsCommand _sut;

    public ReservePrivilegedControlsSeatsCommandTests()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM39883_Pam_Billing_Support).Returns(true);
        _sut = new ReservePrivilegedControlsSeatsCommand(
            _countNewPamSeatsRequiredQuery,
            _autoscalePrivilegedControlsSeatsCommand,
            _environment,
            _featureService,
            _logger);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(0)]
    public async Task RunAsync_Cloud_FlagOff_SucceedsWithoutCallingAutoscale(int pamSeats)
    {
        var organization = CreateOrganization(pamSeats: pamSeats);
        _featureService.IsEnabled(FeatureFlagKeys.PM39883_Pam_Billing_Support).Returns(false);

        var result = await _sut.RunAsync(organization, 2);

        Assert.True(result.Success);
        await _autoscalePrivilegedControlsSeatsCommand.DidNotReceiveWithAnyArgs().RunAsync(default!, default);
    }

    [Fact]
    public async Task RunAsync_SelfHosted_FlagOff_Shortfall_ReturnsSelfHostedShortfall()
    {
        var organization = CreateOrganization();
        _environment.SelfHosted.Returns(true);
        _featureService.IsEnabled(FeatureFlagKeys.PM39883_Pam_Billing_Support).Returns(false);
        _countNewPamSeatsRequiredQuery.CountNewPamSeatsRequiredAsync(organization, 2).Returns(1);

        var result = await _sut.RunAsync(organization, 2);

        Assert.IsType<CannotAutoscalePrivilegedControlsSeatsOnSelfHostError>(result.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task RunAsync_NoSeatsRequested_SucceedsWithoutCallingAutoscale(int seatsRequested)
    {
        var organization = CreateOrganization();

        var result = await _sut.RunAsync(organization, seatsRequested);

        Assert.True(result.Success);
        await _autoscalePrivilegedControlsSeatsCommand.DidNotReceiveWithAnyArgs().RunAsync(default!, default);
    }

    [Fact]
    public async Task RunAsync_PrivilegedControlsOff_SucceedsWithoutCallingAutoscale()
    {
        var organization = CreateOrganization(usePam: false);

        var result = await _sut.RunAsync(organization, 2);

        Assert.True(result.Success);
        await _autoscalePrivilegedControlsSeatsCommand.DidNotReceiveWithAnyArgs().RunAsync(default!, default);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsync_NullPrivilegedControlsSeats_SucceedsWithoutCheckingSeats(bool selfHosted)
    {
        var organization = CreateOrganization(pamSeats: null);
        _environment.SelfHosted.Returns(selfHosted);

        var result = await _sut.RunAsync(organization, 2);

        Assert.True(result.Success);
        await _autoscalePrivilegedControlsSeatsCommand.DidNotReceiveWithAnyArgs().RunAsync(default!, default);
        await _countNewPamSeatsRequiredQuery.DidNotReceiveWithAnyArgs().CountNewPamSeatsRequiredAsync(default!, default);
    }

    [Fact]
    public async Task RunAsync_Cloud_ZeroPrivilegedControlsSeats_ReturnsLimitReachedWithoutCallingAutoscale()
    {
        var organization = CreateOrganization(pamSeats: 0);

        var result = await _sut.RunAsync(organization, 2);

        Assert.IsType<PrivilegedControlsSeatLimitReachedError>(result.Error);
        await _autoscalePrivilegedControlsSeatsCommand.DidNotReceiveWithAnyArgs().RunAsync(default!, default);
    }

    [Fact]
    public async Task RunAsync_SelfHosted_ZeroPrivilegedControlsSeats_ReturnsSelfHostedShortfall()
    {
        var organization = CreateOrganization(pamSeats: 0);
        _environment.SelfHosted.Returns(true);
        _countNewPamSeatsRequiredQuery.CountNewPamSeatsRequiredAsync(organization, 2).Returns(2);

        var result = await _sut.RunAsync(organization, 2);

        Assert.IsType<CannotAutoscalePrivilegedControlsSeatsOnSelfHostError>(result.Error);
    }

    [Fact]
    public async Task RunAsync_SelfHosted_Shortfall_ReturnsSelfHostedShortfall()
    {
        var organization = CreateOrganization();
        _environment.SelfHosted.Returns(true);
        _countNewPamSeatsRequiredQuery.CountNewPamSeatsRequiredAsync(organization, 2).Returns(1);

        var result = await _sut.RunAsync(organization, 2);

        Assert.IsType<CannotAutoscalePrivilegedControlsSeatsOnSelfHostError>(result.Error);
        await _autoscalePrivilegedControlsSeatsCommand.DidNotReceiveWithAnyArgs().RunAsync(default!, default);
    }

    [Fact]
    public async Task RunAsync_SelfHosted_NoShortfall_Succeeds()
    {
        var organization = CreateOrganization();
        _environment.SelfHosted.Returns(true);
        _countNewPamSeatsRequiredQuery.CountNewPamSeatsRequiredAsync(organization, 2).Returns(0);

        var result = await _sut.RunAsync(organization, 2);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task RunAsync_Cloud_AutoscaleSucceeds_Succeeds()
    {
        var organization = CreateOrganization();
        _autoscalePrivilegedControlsSeatsCommand.RunAsync(organization, 3)
            .Returns(new AutoscalePrivilegedControlsSeatsResult(true, null));

        var result = await _sut.RunAsync(organization, 3);

        Assert.True(result.Success);
        await _autoscalePrivilegedControlsSeatsCommand.Received(1).RunAsync(organization, 3);
    }

    [Fact]
    public async Task RunAsync_Cloud_AutoscaleAtLimit_ReturnsLimitReached()
    {
        var organization = CreateOrganization();
        _autoscalePrivilegedControlsSeatsCommand.RunAsync(organization, 3)
            .Returns(new AutoscalePrivilegedControlsSeatsResult(false, 6));

        var result = await _sut.RunAsync(organization, 3);

        Assert.IsType<PrivilegedControlsSeatLimitReachedError>(result.Error);
    }

    [Fact]
    public async Task RunAsync_AutoscaleThrowsBadRequest_ReturnsExpansionFailedWithTheMessageAndLogsAWarning()
    {
        var organization = CreateOrganization();
        var exception = new BadRequestException("No subscription found.");
        _autoscalePrivilegedControlsSeatsCommand.RunAsync(organization, 3).ThrowsAsync(exception);

        var result = await _sut.RunAsync(organization, 3);

        var error = Assert.IsType<PrivilegedControlsSeatExpansionFailedError>(result.Error);
        Assert.Equal("No subscription found.", error.Message);
        Assert.Same(exception, result.Exception);
        Assert.Single(_logger.Warnings);
        Assert.Empty(_logger.Errors);
    }

    [Fact]
    public async Task RunAsync_AutoscaleThrowsUnexpectedly_ReturnsUnexpectedErrorAndLogsAnError()
    {
        var organization = CreateOrganization();
        var exception = new ConflictException("The subscription is invalid.");
        _autoscalePrivilegedControlsSeatsCommand.RunAsync(organization, 3).ThrowsAsync(exception);

        var result = await _sut.RunAsync(organization, 3);

        Assert.IsType<PrivilegedControlsSeatReservationUnexpectedError>(result.Error);
        Assert.Same(exception, result.Exception);
        Assert.Single(_logger.Errors);
        Assert.Empty(_logger.Warnings);
    }

    private static OrganizationEntity CreateOrganization(bool usePam = true, int? pamSeats = 5) => new()
    {
        Id = Guid.NewGuid(),
        UsePam = usePam,
        PamSeats = pamSeats
    };
}
