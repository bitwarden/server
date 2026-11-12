using Bit.Core;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Bit.Core.Billing.Organizations;
using Bit.Core.Billing.Organizations.Queries;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Subscriptions.Organization.Queries;
using Bitwarden.Server.Sdk.Environment;
using Bitwarden.Server.Sdk.Features;
using NSubstitute;
using Xunit;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;

namespace Bit.Subscriptions.Organization.Test.Queries;

public class ValidatePrivilegedControlsSeatReservationQueryTests
{
    private readonly ICountNewPamSeatsRequiredQuery _countNewPamSeatsRequiredQuery =
        Substitute.For<ICountNewPamSeatsRequiredQuery>();
    private readonly IOrganizationUserRepository _organizationUserRepository =
        Substitute.For<IOrganizationUserRepository>();
    private readonly FakeIsOnPrivilegedControlsSalesInitiatedTrialQuery _isOnTrialQuery = new();
    private readonly IBitwardenEnvironment _environment = Substitute.For<IBitwardenEnvironment>();
    private readonly IFeatureService _featureService = Substitute.For<IFeatureService>();
    private readonly ValidatePrivilegedControlsSeatReservationQuery _sut;

    public ValidatePrivilegedControlsSeatReservationQueryTests()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM39883_Pam_Billing_Support).Returns(true);
        _sut = new ValidatePrivilegedControlsSeatReservationQuery(
            _countNewPamSeatsRequiredQuery,
            _organizationUserRepository,
            _isOnTrialQuery,
            _environment,
            _featureService);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task RunAsync_NoSeatsRequested_ReturnsNull(int seatsRequested)
    {
        var error = await _sut.RunAsync(CreateOrganization(), seatsRequested, 0);

        Assert.Null(error);
        await _countNewPamSeatsRequiredQuery.DidNotReceiveWithAnyArgs().CountNewPamSeatsRequiredAsync(default!, default);
    }

    [Fact]
    public async Task RunAsync_PrivilegedControlsOff_ReturnsNull()
    {
        var error = await _sut.RunAsync(CreateOrganization(usePam: false), 2, 0);

        Assert.Null(error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsync_NullPrivilegedControlsSeats_ReturnsNull(bool selfHosted)
    {
        _environment.SelfHosted.Returns(selfHosted);

        var error = await _sut.RunAsync(CreateOrganization(pamSeats: null), 2, 0);

        Assert.Null(error);
        await _countNewPamSeatsRequiredQuery.DidNotReceiveWithAnyArgs().CountNewPamSeatsRequiredAsync(default!, default);
    }

    [Fact]
    public async Task RunAsync_SelfHosted_Shortfall_ReturnsCannotAutoscaleOnSelfHost()
    {
        var organization = CreateOrganization();
        _environment.SelfHosted.Returns(true);
        _countNewPamSeatsRequiredQuery.CountNewPamSeatsRequiredAsync(organization, 2).Returns(1);

        var error = await _sut.RunAsync(organization, 2, 0);

        Assert.IsType<CannotAutoscalePrivilegedControlsSeatsOnSelfHostError>(error);
    }

    [Fact]
    public async Task RunAsync_SelfHosted_FlagOff_Shortfall_ReturnsCannotAutoscaleOnSelfHost()
    {
        var organization = CreateOrganization();
        _environment.SelfHosted.Returns(true);
        _featureService.IsEnabled(FeatureFlagKeys.PM39883_Pam_Billing_Support).Returns(false);
        _countNewPamSeatsRequiredQuery.CountNewPamSeatsRequiredAsync(organization, 2).Returns(1);

        var error = await _sut.RunAsync(organization, 2, 0);

        Assert.IsType<CannotAutoscalePrivilegedControlsSeatsOnSelfHostError>(error);
    }

    [Fact]
    public async Task RunAsync_SelfHosted_NoShortfall_ReturnsNull()
    {
        var organization = CreateOrganization();
        _environment.SelfHosted.Returns(true);
        _countNewPamSeatsRequiredQuery.CountNewPamSeatsRequiredAsync(organization, 2).Returns(0);

        var error = await _sut.RunAsync(organization, 2, 0);

        Assert.Null(error);
    }

    [Fact]
    public async Task RunAsync_FlagOff_Shortfall_ReturnsNotEnoughSeats()
    {
        var organization = CreateOrganization();
        _featureService.IsEnabled(FeatureFlagKeys.PM39883_Pam_Billing_Support).Returns(false);
        _countNewPamSeatsRequiredQuery.CountNewPamSeatsRequiredAsync(organization, 2).Returns(1);

        var error = await _sut.RunAsync(organization, 2, 0);

        Assert.IsType<NotEnoughPrivilegedControlsSeatsError>(error);
    }

    [Fact]
    public async Task RunAsync_FlagOff_NoShortfall_ReturnsNull()
    {
        var organization = CreateOrganization();
        _featureService.IsEnabled(FeatureFlagKeys.PM39883_Pam_Billing_Support).Returns(false);
        _countNewPamSeatsRequiredQuery.CountNewPamSeatsRequiredAsync(organization, 2).Returns(0);

        var error = await _sut.RunAsync(organization, 2, 0);

        Assert.Null(error);
    }

    [Fact]
    public async Task RunAsync_FlagOn_ZeroPrivilegedControlsSeats_ReturnsLimitReached()
    {
        var error = await _sut.RunAsync(CreateOrganization(pamSeats: 0), 2, 0);

        Assert.IsType<PrivilegedControlsSeatLimitReachedError>(error);
    }

    [Fact]
    public async Task RunAsync_FlagOn_SeatsFitWithinBoughtSeats_ReturnsNullWithoutCheckingTrial()
    {
        var organization = CreateOrganization();
        SetOccupiedSeats(organization, 3);

        var error = await _sut.RunAsync(organization, 2, 0);

        Assert.Null(error);
        Assert.Equal(0, _isOnTrialQuery.Calls);
    }

    [Fact]
    public async Task RunAsync_FlagOn_PastAutoscaleLimit_ReturnsLimitReached()
    {
        var organization = CreateOrganization(maxAutoscalePamSeats: 6);
        SetOccupiedSeats(organization, 5);

        var error = await _sut.RunAsync(organization, 2, 0);

        Assert.IsType<PrivilegedControlsSeatLimitReachedError>(error);
    }

    [Fact]
    public async Task RunAsync_FlagOn_OnTrial_PastAutoscaleLimit_ReturnsLimitReached()
    {
        var organization = CreateOrganization(maxAutoscalePamSeats: 6);
        SetOccupiedSeats(organization, 5);
        _isOnTrialQuery.Result = true;

        var error = await _sut.RunAsync(organization, 2, 0);

        Assert.IsType<PrivilegedControlsSeatLimitReachedError>(error);
    }

    [Fact]
    public async Task RunAsync_FlagOn_NullAutoscaleLimit_ReturnsNull()
    {
        var organization = CreateOrganization(maxAutoscalePamSeats: null, seats: 10);
        SetOccupiedSeats(organization, 5);

        var error = await _sut.RunAsync(organization, 2, 0);

        Assert.Null(error);
    }

    [Fact]
    public async Task RunAsync_FlagOn_WithinPasswordManagerSeatsAfterPurchase_ReturnsNullWithoutCheckingTrial()
    {
        var organization = CreateOrganization(seats: 6);
        SetOccupiedSeats(organization, 5);

        var error = await _sut.RunAsync(organization, 2, 1);

        Assert.Null(error);
        Assert.Equal(0, _isOnTrialQuery.Calls);
    }

    [Fact]
    public async Task RunAsync_FlagOn_PastPasswordManagerSeatsAfterPurchase_NotOnTrial_ReturnsMustNotExceedPasswordManagerSeats()
    {
        var organization = CreateOrganization(seats: 5);
        SetOccupiedSeats(organization, 5);

        var error = await _sut.RunAsync(organization, 2, 1);

        Assert.IsType<PrivilegedControlsSeatsMustNotExceedPasswordManagerSeatsError>(error);
        Assert.Equal(1, _isOnTrialQuery.Calls);
    }

    [Fact]
    public async Task RunAsync_FlagOn_PastPasswordManagerSeatsAfterPurchase_OnTrial_ReturnsNull()
    {
        var organization = CreateOrganization(seats: 5);
        SetOccupiedSeats(organization, 5);
        _isOnTrialQuery.Result = true;

        var error = await _sut.RunAsync(organization, 2, 1);

        Assert.Null(error);
    }

    [Fact]
    public async Task RunAsync_FlagOn_NullPasswordManagerSeats_SkipsPasswordManagerRule()
    {
        var organization = CreateOrganization(seats: null);
        SetOccupiedSeats(organization, 5);

        var error = await _sut.RunAsync(organization, 2, 0);

        Assert.Null(error);
        Assert.Equal(0, _isOnTrialQuery.Calls);
    }

    [Fact]
    public async Task RunAsync_TrialCheckThrows_Throws()
    {
        var organization = CreateOrganization(seats: 5);
        SetOccupiedSeats(organization, 5);
        _isOnTrialQuery.Exception = new ConflictException("x");

        await Assert.ThrowsAsync<ConflictException>(() => _sut.RunAsync(organization, 2, 1));
    }

    private void SetOccupiedSeats(OrganizationEntity organization, int occupied) =>
        _organizationUserRepository.GetOccupiedPamSeatCountByOrganizationIdAsync(organization.Id).Returns(occupied);

    private static OrganizationEntity CreateOrganization(
        bool usePam = true,
        int? pamSeats = 5,
        int? seats = 10,
        int? maxAutoscalePamSeats = null) => new()
        {
            Id = Guid.NewGuid(),
            UsePam = usePam,
            PamSeats = pamSeats,
            Seats = seats,
            MaxAutoscalePamSeats = maxAutoscalePamSeats
        };
}
