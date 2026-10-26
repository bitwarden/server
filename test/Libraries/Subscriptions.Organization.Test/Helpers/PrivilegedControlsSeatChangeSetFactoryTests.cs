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
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly IOrganizationUserRepository _organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
    private readonly PrivilegedControlsSeatChangeSetFactory _factory;

    public PrivilegedControlsSeatChangeSetFactoryTests()
    {
        _factory = new PrivilegedControlsSeatChangeSetFactory(_organizationUserRepository, _pricingClient);
    }

    [Fact]
    public async Task CreateAsync_FirstPurchaseAbovePasswordManagerSeats_ThrowsBadRequest()
    {
        var organization = CreateOrganization(seats: 5);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _factory.CreateAsync(organization, 6));

        Assert.Equal("You cannot have more Privileged Controls seats than Password Manager seats.", exception.Message);
    }

    [Fact]
    public async Task CreateAsync_FirstPurchaseEqualToPasswordManagerSeats_BuildsAddChange()
    {
        var organization = CreateOrganization(seats: 5);

        var result = await _factory.CreateAsync(organization, 5);

        Assert.Equal(5, Assert.Single(result.Changes).AsT0.Quantity);
    }

    [Fact]
    public async Task CreateAsync_FirstPurchaseOfOneSeat_BuildsAddChange()
    {
        var organization = CreateOrganization();

        var result = await _factory.CreateAsync(organization, 1);

        Assert.Equal(1, Assert.Single(result.Changes).AsT0.Quantity);
    }

    [Fact]
    public async Task CreateAsync_IncreaseAbovePasswordManagerSeats_ThrowsBadRequest()
    {
        var organization = CreateOrganization(pamSeats: 3, seats: 5);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _factory.CreateAsync(organization, 6));

        Assert.Equal("You cannot have more Privileged Controls seats than Password Manager seats.", exception.Message);
    }

    [Fact]
    public async Task CreateAsync_ZeroSavedSeats_BuildsAddChange()
    {
        var organization = CreateOrganization(pamSeats: 0);

        var result = await _factory.CreateAsync(organization, 3);

        var item = Assert.Single(result.Changes).AsT0;
        Assert.Equal(3, item.Quantity);
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

        var item = Assert.Single(result.Changes).AsT3;
        Assert.Equal(12, item.Quantity);
    }


    [Fact]
    public async Task CreateAsync_Increase_BuildsUpdateChange()
    {
        var organization = CreateOrganization(pamSeats: 12);

        var result = await _factory.CreateAsync(organization, 30);

        var item = Assert.Single(result.Changes).AsT3;
        Assert.Equal(30, item.Quantity);
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


    private OrganizationEntity CreateOrganization(
        PlanType planType = PlanType.EnterpriseAnnually,
        int? pamSeats = null,
        short? seats = 50)
    {
        var organization = new OrganizationEntity
        {
            Id = Guid.NewGuid(),
            PlanType = planType,
            PamSeats = pamSeats,
            Seats = seats
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
                    StripeSeatPlanId = "privileged-controls-enterprise-seat-annually"
                };
            }
        }
    }
}
