using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.Billing;
using Bit.Core.Billing.Commands.Implementations;
using Bit.Core.Billing.Entities;
using Bit.Core.Billing.Repositories;
using Bit.Core.Billing.Services;
using Bit.Core.Enums;
using Bit.Core.Models.StaticStore;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

using static Bit.Core.Test.Billing.Utilities;

namespace Bit.Core.Test.Billing.Commands;

[SutProviderCustomize]
public class AssignSeatsToClientOrganizationCommandTests
{
    [Theory, BitAutoData]
    public Task AssignSeatsToClientOrganization_NullProvider_ArgumentNullException(
        Organization organization,
        int seats,
        SutProvider<AssignSeatsToClientOrganizationCommand> sutProvider)
        => Assert.ThrowsAsync<ArgumentNullException>(() =>
            sutProvider.Sut.AssignSeatsToClientOrganization(null, organization, seats));

    [Theory, BitAutoData]
    public Task AssignSeatsToClientOrganization_NullOrganization_ArgumentNullException(
        Provider provider,
        int seats,
        SutProvider<AssignSeatsToClientOrganizationCommand> sutProvider)
        => Assert.ThrowsAsync<ArgumentNullException>(() =>
            sutProvider.Sut.AssignSeatsToClientOrganization(provider, null, seats));

    [Theory, BitAutoData]
    public Task AssignSeatsToClientOrganization_NegativeSeats_BillingException(
        Provider provider,
        Organization organization,
        SutProvider<AssignSeatsToClientOrganizationCommand> sutProvider)
        => Assert.ThrowsAsync<BillingException>(() =>
            sutProvider.Sut.AssignSeatsToClientOrganization(provider, organization, -5));

    [Theory, BitAutoData]
    public async Task AssignSeatsToClientOrganization_CurrentSeatsMatchesNewSeats_NoOp(
        Provider provider,
        Organization organization,
        int seats,
        SutProvider<AssignSeatsToClientOrganizationCommand> sutProvider)
    {
        organization.PlanType = PlanType.TeamsMonthly;

        organization.Seats = seats;

        await sutProvider.Sut.AssignSeatsToClientOrganization(provider, organization, seats);

        await sutProvider.GetDependency<IProviderPlanRepository>().DidNotReceive().GetByProviderId(provider.Id);
    }

    [Theory, BitAutoData]
    public async Task AssignSeatsToClientOrganization_OrganizationPlanTypeDoesNotSupportConsolidatedBilling_ContactSupport(
        Provider provider,
        Organization organization,
        int seats,
        SutProvider<AssignSeatsToClientOrganizationCommand> sutProvider)
    {
        organization.PlanType = PlanType.FamiliesAnnually;

        await ThrowsContactSupportAsync(() => sutProvider.Sut.AssignSeatsToClientOrganization(provider, organization, seats));
    }

    [Theory, BitAutoData]
    public async Task AssignSeatsToClientOrganization_ProviderPlanIsNotConfigured_ContactSupport(
        Provider provider,
        Organization organization,
        int seats,
        SutProvider<AssignSeatsToClientOrganizationCommand> sutProvider)
    {
        organization.PlanType = PlanType.TeamsMonthly;

        sutProvider.GetDependency<IProviderPlanRepository>().GetByProviderId(provider.Id).Returns(new List<ProviderPlan>
        {
            new ()
            {
                Id = Guid.NewGuid(),
                PlanType = PlanType.TeamsMonthly,
                ProviderId = provider.Id
            }
        });

        await ThrowsContactSupportAsync(() => sutProvider.Sut.AssignSeatsToClientOrganization(provider, organization, seats));
    }

    [Theory, BitAutoData]
    public async Task AssignSeatsToClientOrganization_BelowToBelow_Succeeds(
        Provider provider,
        Organization organization,
        SutProvider<AssignSeatsToClientOrganizationCommand> sutProvider)
    {
        organization.Seats = 10;

        organization.PlanType = PlanType.TeamsMonthly;

        // Scale up 10 seats
        const int seats = 20;

        var providerPlans = new List<ProviderPlan>
        {
            new()
            {
                Id = Guid.NewGuid(),
                PlanType = PlanType.TeamsMonthly,
                ProviderId = provider.Id,
                PurchasedSeats = 0,
                // 100 minimum
                SeatMinimum = 100,
                AllocatedSeats = 50
            },
            new()
            {
                Id = Guid.NewGuid(),
                PlanType = PlanType.EnterpriseMonthly,
                ProviderId = provider.Id,
                PurchasedSeats = 0,
                SeatMinimum = 500,
                AllocatedSeats = 0
            }
        };

        var providerPlan = providerPlans.First();

        sutProvider.GetDependency<IProviderPlanRepository>().GetByProviderId(provider.Id).Returns(providerPlans);

        // 50 seats currently assigned with a seat minimum of 100
        sutProvider.GetDependency<IProviderBillingService>().GetAssignedSeatTotalForPlanOrThrow(provider.Id, providerPlan.PlanType).Returns(50);

        await sutProvider.Sut.AssignSeatsToClientOrganization(provider, organization, seats);

        // 50 assigned seats + 10 seat scale up = 60 seats, well below the 100 minimum
        await sutProvider.GetDependency<IPaymentService>().DidNotReceiveWithAnyArgs().AdjustSeats(
            Arg.Any<Provider>(),
            Arg.Any<Plan>(),
            Arg.Any<int>(),
            Arg.Any<int>());

        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).ReplaceAsync(Arg.Is<Organization>(
            org => org.Id == organization.Id && org.Seats == seats));

        await sutProvider.GetDependency<IProviderPlanRepository>().Received(1).ReplaceAsync(Arg.Is<ProviderPlan>(
            pPlan => pPlan.AllocatedSeats == 60));
    }

    [Theory, BitAutoData]
    public async Task AssignSeatsToClientOrganization_BelowToAbove_Succeeds(
        Provider provider,
        Organization organization,
        SutProvider<AssignSeatsToClientOrganizationCommand> sutProvider)
    {
        organization.Seats = 10;

        organization.PlanType = PlanType.TeamsMonthly;

        // Scale up 10 seats
        const int seats = 20;

        var providerPlans = new List<ProviderPlan>
        {
            new()
            {
                Id = Guid.NewGuid(),
                PlanType = PlanType.TeamsMonthly,
                ProviderId = provider.Id,
                PurchasedSeats = 0,
                // 100 minimum
                SeatMinimum = 100,
                AllocatedSeats = 95
            },
            new()
            {
                Id = Guid.NewGuid(),
                PlanType = PlanType.EnterpriseMonthly,
                ProviderId = provider.Id,
                PurchasedSeats = 0,
                SeatMinimum = 500,
                AllocatedSeats = 0
            }
        };

        var providerPlan = providerPlans.First();

        sutProvider.GetDependency<IProviderPlanRepository>().GetByProviderId(provider.Id).Returns(providerPlans);

        // 95 seats currently assigned with a seat minimum of 100
        sutProvider.GetDependency<IProviderBillingService>().GetAssignedSeatTotalForPlanOrThrow(provider.Id, providerPlan.PlanType).Returns(95);

        await sutProvider.Sut.AssignSeatsToClientOrganization(provider, organization, seats);

        // 95 current + 10 seat scale = 105 seats, 5 above the minimum
        await sutProvider.GetDependency<IPaymentService>().Received(1).AdjustSeats(
            provider,
            StaticStore.GetPlan(providerPlan.PlanType),
            providerPlan.SeatMinimum!.Value,
            105);

        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).ReplaceAsync(Arg.Is<Organization>(
            org => org.Id == organization.Id && org.Seats == seats));

        // 105 total seats - 100 minimum = 5 purchased seats
        await sutProvider.GetDependency<IProviderPlanRepository>().Received(1).ReplaceAsync(Arg.Is<ProviderPlan>(
            pPlan => pPlan.Id == providerPlan.Id && pPlan.PurchasedSeats == 5 && pPlan.AllocatedSeats == 105));
    }

    [Theory, BitAutoData]
    public async Task AssignSeatsToClientOrganization_AboveToAbove_Succeeds(
        Provider provider,
        Organization organization,
        SutProvider<AssignSeatsToClientOrganizationCommand> sutProvider)
    {
        organization.Seats = 10;

        organization.PlanType = PlanType.TeamsMonthly;

        // Scale up 10 seats
        const int seats = 20;

        var providerPlans = new List<ProviderPlan>
        {
            new()
            {
                Id = Guid.NewGuid(),
                PlanType = PlanType.TeamsMonthly,
                ProviderId = provider.Id,
                // 10 additional purchased seats
                PurchasedSeats = 10,
                // 100 seat minimum
                SeatMinimum = 100,
                AllocatedSeats = 110
            },
            new()
            {
                Id = Guid.NewGuid(),
                PlanType = PlanType.EnterpriseMonthly,
                ProviderId = provider.Id,
                PurchasedSeats = 0,
                SeatMinimum = 500,
                AllocatedSeats = 0
            }
        };

        var providerPlan = providerPlans.First();

        sutProvider.GetDependency<IProviderPlanRepository>().GetByProviderId(provider.Id).Returns(providerPlans);

        // 110 seats currently assigned with a seat minimum of 100
        sutProvider.GetDependency<IProviderBillingService>().GetAssignedSeatTotalForPlanOrThrow(provider.Id, providerPlan.PlanType).Returns(110);

        await sutProvider.Sut.AssignSeatsToClientOrganization(provider, organization, seats);

        // 110 current + 10 seat scale up = 120 seats
        await sutProvider.GetDependency<IPaymentService>().Received(1).AdjustSeats(
            provider,
            StaticStore.GetPlan(providerPlan.PlanType),
            110,
            120);

        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).ReplaceAsync(Arg.Is<Organization>(
            org => org.Id == organization.Id && org.Seats == seats));

        // 120 total seats - 100 seat minimum = 20 purchased seats
        await sutProvider.GetDependency<IProviderPlanRepository>().Received(1).ReplaceAsync(Arg.Is<ProviderPlan>(
            pPlan => pPlan.Id == providerPlan.Id && pPlan.PurchasedSeats == 20 && pPlan.AllocatedSeats == 120));
    }

    [Theory, BitAutoData]
    public async Task AssignSeatsToClientOrganization_AboveToBelow_Succeeds(
        Provider provider,
        Organization organization,
        SutProvider<AssignSeatsToClientOrganizationCommand> sutProvider)
    {
        organization.Seats = 50;

        organization.PlanType = PlanType.TeamsMonthly;

        // Scale down 30 seats
        const int seats = 20;

        var providerPlans = new List<ProviderPlan>
        {
            new()
            {
                Id = Guid.NewGuid(),
                PlanType = PlanType.TeamsMonthly,
                ProviderId = provider.Id,
                // 10 additional purchased seats
                PurchasedSeats = 10,
                // 100 seat minimum
                SeatMinimum = 100,
                AllocatedSeats = 110
            },
            new()
            {
                Id = Guid.NewGuid(),
                PlanType = PlanType.EnterpriseMonthly,
                ProviderId = provider.Id,
                PurchasedSeats = 0,
                SeatMinimum = 500,
                AllocatedSeats = 0
            }
        };

        var providerPlan = providerPlans.First();

        sutProvider.GetDependency<IProviderPlanRepository>().GetByProviderId(provider.Id).Returns(providerPlans);

        // 110 seats currently assigned with a seat minimum of 100
        sutProvider.GetDependency<IProviderBillingService>().GetAssignedSeatTotalForPlanOrThrow(provider.Id, providerPlan.PlanType).Returns(110);

        await sutProvider.Sut.AssignSeatsToClientOrganization(provider, organization, seats);

        // 110 seats - 30 scale down seats = 80 seats, below the 100 seat minimum.
        await sutProvider.GetDependency<IPaymentService>().Received(1).AdjustSeats(
            provider,
            StaticStore.GetPlan(providerPlan.PlanType),
            110,
            providerPlan.SeatMinimum!.Value);

        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).ReplaceAsync(Arg.Is<Organization>(
            org => org.Id == organization.Id && org.Seats == seats));

        // Being below the seat minimum means no purchased seats.
        await sutProvider.GetDependency<IProviderPlanRepository>().Received(1).ReplaceAsync(Arg.Is<ProviderPlan>(
            pPlan => pPlan.Id == providerPlan.Id && pPlan.PurchasedSeats == 0 && pPlan.AllocatedSeats == 80));
    }
}
