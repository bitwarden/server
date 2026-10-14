using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Subscriptions.Organization.Helpers;
using NSubstitute;
using Xunit;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;
using PlanFeatures = Bit.Core.Models.StaticStore.Plan;

namespace Bit.Subscriptions.Organization.Test.Helpers;

public class PrivilegedControlsSeatChangeSetFactoryTests
{
    private const int _planDefaultMinimum = 10;

    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly IOrganizationUserRepository _organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
    private readonly PrivilegedControlsSeatChangeSetFactory _factory;

    public PrivilegedControlsSeatChangeSetFactoryTests()
    {
        _factory = new PrivilegedControlsSeatChangeSetFactory(_organizationUserRepository, _pricingClient);
    }

    [Fact]
    public async Task CreateAsync_FirstPurchaseBelowMinimum_ThrowsBadRequest()
    {
        var organization = CreateOrganization();

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => _factory.CreateAsync(organization, _planDefaultMinimum - 1));

        Assert.Equal("Privileged Controls requires at least 10 seats.", exception.Message);
    }

    [Fact]
    public async Task CreateAsync_FirstPurchaseAtMinimum_BuildsAddChange()
    {
        var organization = CreateOrganization();

        var result = await _factory.CreateAsync(organization, _planDefaultMinimum);

        var item = Assert.Single(result.ChangeSet.Changes).AsT0;
        Assert.Equal(_planDefaultMinimum, item.Quantity);
    }

    [Fact]
    public async Task CreateAsync_ZeroSavedSeatsBelowMinimum_ThrowsBadRequest()
    {
        var organization = CreateOrganization(pamSeats: 0);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => _factory.CreateAsync(organization, _planDefaultMinimum - 1));

        Assert.Equal("Privileged Controls requires at least 10 seats.", exception.Message);
    }

    [Fact]
    public async Task CreateAsync_ZeroSavedSeatsAtMinimum_BuildsAddChange()
    {
        var organization = CreateOrganization(pamSeats: 0);

        var result = await _factory.CreateAsync(organization, _planDefaultMinimum);

        var item = Assert.Single(result.ChangeSet.Changes).AsT0;
        Assert.Equal(_planDefaultMinimum, item.Quantity);
    }

    [Fact]
    public async Task CreateAsync_ReductionBelowMinimum_ThrowsBadRequest()
    {
        var organization = CreateOrganization(pamSeats: 15);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => _factory.CreateAsync(organization, _planDefaultMinimum - 1));

        Assert.Equal("Privileged Controls requires at least 10 seats.", exception.Message);
    }

    [Fact]
    public async Task CreateAsync_ReductionBelowSeatsInUse_ThrowsBadRequest()
    {
        var organization = CreateOrganization(pamSeats: 20);
        _organizationUserRepository.GetOccupiedPamSeatCountByOrganizationIdAsync(organization.Id).Returns(14);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _factory.CreateAsync(organization, 12));

        Assert.Equal(
            "14 users are currently occupying Privileged Controls seats. " +
            "You cannot decrease your subscription below your current occupied seat count.",
            exception.Message);
    }

    [Fact]
    public async Task CreateAsync_ReductionAtSeatsInUse_BuildsUpdateChange()
    {
        var organization = CreateOrganization(pamSeats: 20);
        _organizationUserRepository.GetOccupiedPamSeatCountByOrganizationIdAsync(organization.Id).Returns(12);

        var result = await _factory.CreateAsync(organization, 12);

        var item = Assert.Single(result.ChangeSet.Changes).AsT3;
        Assert.Equal(12, item.Quantity);
    }

    [Fact]
    public async Task CreateAsync_Increase_BuildsUpdateChange()
    {
        var organization = CreateOrganization(pamSeats: 12);

        var result = await _factory.CreateAsync(organization, 30);

        var item = Assert.Single(result.ChangeSet.Changes).AsT3;
        Assert.Equal(30, item.Quantity);
    }

    [Fact]
    public async Task CreateAsync_IncreaseFromBelowMinimum_DoesNotCheckMinimum()
    {
        var organization = CreateOrganization(pamSeats: 3, pamSeatMinimum: 10);

        var result = await _factory.CreateAsync(organization, 5);

        Assert.Equal(5, Assert.Single(result.ChangeSet.Changes).AsT3.Quantity);
    }

    [Fact]
    public async Task CreateAsync_Increase_DoesNotReadSeatsInUse()
    {
        var organization = CreateOrganization(pamSeats: 12);

        await _factory.CreateAsync(organization, 30);

        await _organizationUserRepository.DidNotReceive()
            .GetOccupiedPamSeatCountByOrganizationIdAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task CreateAsync_SameSeatCount_ThrowsBadRequest()
    {
        var organization = CreateOrganization(pamSeats: 12);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _factory.CreateAsync(organization, 12));

        Assert.Equal("Your organization already has 12 Privileged Controls seats.", exception.Message);
    }

    [Fact]
    public async Task CreateAsync_SavedMinimum_AppliesInsteadOfPlanDefault()
    {
        var organization = CreateOrganization(pamSeatMinimum: 6);

        var result = await _factory.CreateAsync(organization, 6);

        Assert.Equal(6, Assert.Single(result.ChangeSet.Changes).AsT0.Quantity);
    }

    [Fact]
    public async Task CreateAsync_SavedMinimum_ReturnsSavedMinimum()
    {
        var organization = CreateOrganization(pamSeatMinimum: 6);

        var result = await _factory.CreateAsync(organization, 6);

        Assert.Equal(6, result.SeatMinimum);
    }

    [Fact]
    public async Task CreateAsync_NoSavedMinimum_ReturnsPlanDefaultMinimum()
    {
        var organization = CreateOrganization(pamSeatMinimum: null);

        var result = await _factory.CreateAsync(organization, _planDefaultMinimum);

        Assert.Equal(_planDefaultMinimum, result.SeatMinimum);
    }

    [Fact]
    public async Task CreateAsync_SeatChangeOnExistingSeats_ReturnsMinimum()
    {
        var organization = CreateOrganization(pamSeats: 12, pamSeatMinimum: 6);

        var result = await _factory.CreateAsync(organization, 30);

        Assert.Equal(6, result.SeatMinimum);
    }

    [Fact]
    public async Task CreateAsync_SavedMinimum_NamesSavedMinimumInMessage()
    {
        var organization = CreateOrganization(pamSeatMinimum: 6);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _factory.CreateAsync(organization, 5));

        Assert.Equal("Privileged Controls requires at least 6 seats.", exception.Message);
    }

    [Fact]
    public async Task CreateAsync_NoSavedMinimum_AppliesPlanDefault()
    {
        var organization = CreateOrganization(pamSeatMinimum: null);

        await Assert.ThrowsAsync<BadRequestException>(
            () => _factory.CreateAsync(organization, _planDefaultMinimum - 1));
    }

    [Fact]
    public async Task CreateAsync_PlanWithoutPrivilegedControls_ThrowsBadRequest()
    {
        var organization = CreateOrganization(PlanType.TeamsAnnually);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _factory.CreateAsync(organization, 10));

        Assert.Equal("Organization's plan does not support Privileged Controls.", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateAsync_FewerThanOneSeat_ThrowsBadRequest(int seats)
    {
        var organization = CreateOrganization(pamSeats: 12);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _factory.CreateAsync(organization, seats));

        Assert.Equal("At least one Privileged Controls seat is required.", exception.Message);
    }

    [Fact]
    public async Task CreateAsync_SeatsAbovePasswordManagerSeats_BuildsChange()
    {
        var organization = CreateOrganization(pamSeats: 12);
        organization.Seats = 5;

        var result = await _factory.CreateAsync(organization, 30);

        Assert.Equal(30, Assert.Single(result.ChangeSet.Changes).AsT3.Quantity);
    }

    private OrganizationEntity CreateOrganization(
        PlanType planType = PlanType.EnterpriseAnnually,
        int? pamSeats = null,
        int? pamSeatMinimum = null)
    {
        var organization = new OrganizationEntity
        {
            Id = Guid.NewGuid(),
            PlanType = planType,
            PamSeats = pamSeats,
            PamSeatMinimum = pamSeatMinimum
        };
        _pricingClient.GetPlanOrThrow(planType).Returns(new TestPlan(planType == PlanType.EnterpriseAnnually));
        return organization;
    }

    private sealed record TestPlan : PlanFeatures
    {
        public TestPlan(bool supportsPrivilegedControls)
        {
            if (supportsPrivilegedControls)
            {
                PrivilegedControls = new PrivilegedControlsPlanFeatures
                {
                    StripeSeatPlanId = "privileged-controls-enterprise-seat-annually",
                    DefaultSeatMinimum = _planDefaultMinimum
                };
            }
        }
    }
}
