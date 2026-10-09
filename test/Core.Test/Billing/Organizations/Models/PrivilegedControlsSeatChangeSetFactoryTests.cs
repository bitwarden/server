using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Pricing;
using Bit.Core.Repositories;
using Bit.Core.Test.Billing.Mocks;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.Models;

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
    public async Task CreateAsync_FirstPurchaseBelowMinimum_ReturnsBadRequest()
    {
        var organization = CreateOrganization();

        var result = await _factory.CreateAsync(organization, _planDefaultMinimum - 1);

        Assert.Equal("Privileged Controls requires at least 10 seats.", result.AsT1.Response);
    }

    [Fact]
    public async Task CreateAsync_FirstPurchaseAtMinimum_BuildsAddChange()
    {
        var organization = CreateOrganization();

        var result = await _factory.CreateAsync(organization, _planDefaultMinimum);

        var item = Assert.Single(result.AsT0.Changes).AsT0;
        Assert.Equal(_planDefaultMinimum, item.Quantity);
    }

    [Fact]
    public async Task CreateAsync_ZeroSavedSeatsBelowMinimum_ReturnsBadRequest()
    {
        var organization = CreateOrganization(pamSeats: 0);

        var result = await _factory.CreateAsync(organization, _planDefaultMinimum - 1);

        Assert.Equal("Privileged Controls requires at least 10 seats.", result.AsT1.Response);
    }

    [Fact]
    public async Task CreateAsync_ZeroSavedSeatsAtMinimum_BuildsAddChange()
    {
        var organization = CreateOrganization(pamSeats: 0);

        var result = await _factory.CreateAsync(organization, _planDefaultMinimum);

        var item = Assert.Single(result.AsT0.Changes).AsT0;
        Assert.Equal(_planDefaultMinimum, item.Quantity);
    }

    [Fact]
    public async Task CreateAsync_ReductionBelowMinimum_ReturnsBadRequest()
    {
        var organization = CreateOrganization(pamSeats: 15);

        var result = await _factory.CreateAsync(organization, _planDefaultMinimum - 1);

        Assert.Equal("Privileged Controls requires at least 10 seats.", result.AsT1.Response);
    }

    [Fact]
    public async Task CreateAsync_ReductionBelowSeatsInUse_ReturnsBadRequest()
    {
        var organization = CreateOrganization(pamSeats: 20);
        _organizationUserRepository.GetOccupiedPamSeatCountByOrganizationIdAsync(organization.Id).Returns(14);

        var result = await _factory.CreateAsync(organization, 12);

        Assert.Equal(
            "14 users are currently occupying Privileged Controls seats. " +
            "You cannot decrease your subscription below your current occupied seat count.",
            result.AsT1.Response);
    }

    [Fact]
    public async Task CreateAsync_ReductionAtSeatsInUse_BuildsUpdateChange()
    {
        var organization = CreateOrganization(pamSeats: 20);
        _organizationUserRepository.GetOccupiedPamSeatCountByOrganizationIdAsync(organization.Id).Returns(12);

        var result = await _factory.CreateAsync(organization, 12);

        var item = Assert.Single(result.AsT0.Changes).AsT3;
        Assert.Equal(12, item.Quantity);
    }

    [Fact]
    public async Task CreateAsync_Increase_BuildsUpdateChange()
    {
        var organization = CreateOrganization(pamSeats: 12);

        var result = await _factory.CreateAsync(organization, 30);

        var item = Assert.Single(result.AsT0.Changes).AsT3;
        Assert.Equal(30, item.Quantity);
    }

    [Fact]
    public async Task CreateAsync_IncreaseFromBelowMinimum_DoesNotCheckMinimum()
    {
        var organization = CreateOrganization(pamSeats: 3, pamSeatMinimum: 10);

        var result = await _factory.CreateAsync(organization, 5);

        Assert.True(result.Success);
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
    public async Task CreateAsync_SameSeatCount_ReturnsBadRequest()
    {
        var organization = CreateOrganization(pamSeats: 12);

        var result = await _factory.CreateAsync(organization, 12);

        Assert.Equal("Your organization already has 12 Privileged Controls seats.", result.AsT1.Response);
    }

    [Fact]
    public async Task CreateAsync_SavedMinimum_AppliesInsteadOfPlanDefault()
    {
        var organization = CreateOrganization(pamSeatMinimum: 6);

        var result = await _factory.CreateAsync(organization, 6);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task CreateAsync_SavedMinimum_NamesSavedMinimumInMessage()
    {
        var organization = CreateOrganization(pamSeatMinimum: 6);

        var result = await _factory.CreateAsync(organization, 5);

        Assert.Equal("Privileged Controls requires at least 6 seats.", result.AsT1.Response);
    }

    [Fact]
    public async Task CreateAsync_NoSavedMinimum_AppliesPlanDefault()
    {
        var organization = CreateOrganization(pamSeatMinimum: null);

        var result = await _factory.CreateAsync(organization, _planDefaultMinimum - 1);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task CreateAsync_PlanWithoutPrivilegedControls_ReturnsBadRequest()
    {
        var organization = CreateOrganization(PlanType.TeamsAnnually);

        var result = await _factory.CreateAsync(organization, 10);

        Assert.Equal("Organization's plan does not support Privileged Controls.", result.AsT1.Response);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateAsync_FewerThanOneSeat_ReturnsBadRequest(int seats)
    {
        var organization = CreateOrganization(pamSeats: 12);

        var result = await _factory.CreateAsync(organization, seats);

        Assert.Equal("At least one Privileged Controls seat is required.", result.AsT1.Response);
    }

    [Fact]
    public async Task CreateAsync_SeatsAbovePasswordManagerSeats_BuildsChange()
    {
        var organization = CreateOrganization(pamSeats: 12);
        organization.Seats = 5;

        var result = await _factory.CreateAsync(organization, 30);

        Assert.True(result.Success);
    }

    private Organization CreateOrganization(
        PlanType planType = PlanType.EnterpriseAnnually,
        int? pamSeats = null,
        int? pamSeatMinimum = null)
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            PlanType = planType,
            PamSeats = pamSeats,
            PamSeatMinimum = pamSeatMinimum
        };
        _pricingClient.GetPlanOrThrow(planType).Returns(MockPlans.Get(planType));
        return organization;
    }
}
