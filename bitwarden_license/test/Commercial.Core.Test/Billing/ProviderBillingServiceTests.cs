using System.Globalization;
using System.Net;
using Bit.Commercial.Core.Billing;
using Bit.Commercial.Core.Billing.Models;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Repositories;
using Bit.Core.Billing.Services;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using CsvHelper;
using NSubstitute;
using Stripe;
using Xunit;
using static Bit.Core.Test.Billing.Utilities;

namespace Bit.Commercial.Core.Test.Billing;

[SutProviderCustomize]
public class ProviderBillingServiceTests
{
    #region AssignSeatsToClientOrganization & ScaleSeats

    [Theory, BitAutoData]
    public Task AssignSeatsToClientOrganization_NullProvider_ArgumentNullException(
        Organization organization,
        int seats,
        SutProvider<ProviderBillingService> sutProvider)
        => Assert.ThrowsAsync<ArgumentNullException>(() =>
            sutProvider.Sut.AssignSeatsToClientOrganization(null, organization, seats));

    [Theory, BitAutoData]
    public Task AssignSeatsToClientOrganization_NullOrganization_ArgumentNullException(
        Provider provider,
        int seats,
        SutProvider<ProviderBillingService> sutProvider)
        => Assert.ThrowsAsync<ArgumentNullException>(() =>
            sutProvider.Sut.AssignSeatsToClientOrganization(provider, null, seats));

    [Theory, BitAutoData]
    public Task AssignSeatsToClientOrganization_NegativeSeats_BillingException(
        Provider provider,
        Organization organization,
        SutProvider<ProviderBillingService> sutProvider)
        => Assert.ThrowsAsync<BillingException>(() =>
            sutProvider.Sut.AssignSeatsToClientOrganization(provider, organization, -5));

    [Theory, BitAutoData]
    public async Task AssignSeatsToClientOrganization_CurrentSeatsMatchesNewSeats_NoOp(
        Provider provider,
        Organization organization,
        int seats,
        SutProvider<ProviderBillingService> sutProvider)
    {
        organization.PlanType = PlanType.TeamsMonthly;

        organization.Seats = seats;

        await sutProvider.Sut.AssignSeatsToClientOrganization(provider, organization, seats);

        await sutProvider.GetDependency<IProviderPlanRepository>().DidNotReceive().GetByProviderId(provider.Id);
    }

    [Theory, BitAutoData]
    public async Task
        AssignSeatsToClientOrganization_OrganizationPlanTypeDoesNotSupportConsolidatedBilling_ContactSupport(
            Provider provider,
            Organization organization,
            int seats,
            SutProvider<ProviderBillingService> sutProvider)
    {
        organization.PlanType = PlanType.FamiliesAnnually;

        await ThrowsBillingExceptionAsync(() =>
            sutProvider.Sut.AssignSeatsToClientOrganization(provider, organization, seats));
    }

    [Theory, BitAutoData]
    public async Task AssignSeatsToClientOrganization_ProviderPlanIsNotConfigured_ContactSupport(
        Provider provider,
        Organization organization,
        int seats,
        SutProvider<ProviderBillingService> sutProvider)
    {
        organization.PlanType = PlanType.TeamsMonthly;

        sutProvider.GetDependency<IProviderPlanRepository>().GetByProviderId(provider.Id).Returns(new List<ProviderPlan>
        {
            new() { Id = Guid.NewGuid(), PlanType = PlanType.TeamsMonthly, ProviderId = provider.Id }
        });

        await ThrowsBillingExceptionAsync(() =>
            sutProvider.Sut.AssignSeatsToClientOrganization(provider, organization, seats));
    }

    [Theory, BitAutoData]
    public async Task AssignSeatsToClientOrganization_BelowToBelow_Succeeds(
        Provider provider,
        Organization organization,
        SutProvider<ProviderBillingService> sutProvider)
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
        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(provider.Id).Returns(provider);

        var teamsMonthlyPlan = StaticStore.GetPlan(PlanType.TeamsMonthly);

        sutProvider.GetDependency<IProviderOrganizationRepository>().GetManyDetailsByProviderAsync(provider.Id).Returns(
        [
            new ProviderOrganizationOrganizationDetails
            {
                Plan = teamsMonthlyPlan.Name,
                Status = OrganizationStatusType.Managed,
                Seats = 25
            },
            new ProviderOrganizationOrganizationDetails
            {
                Plan = teamsMonthlyPlan.Name,
                Status = OrganizationStatusType.Managed,
                Seats = 25
            }
        ]);

        await sutProvider.Sut.AssignSeatsToClientOrganization(provider, organization, seats);

        // 50 assigned seats + 10 seat scale up = 60 seats, well below the 100 minimum
        await sutProvider.GetDependency<IPaymentService>().DidNotReceiveWithAnyArgs().AdjustSeats(
            Arg.Any<Provider>(),
            Arg.Any<Bit.Core.Models.StaticStore.Plan>(),
            Arg.Any<int>(),
            Arg.Any<int>());

        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).ReplaceAsync(Arg.Is<Organization>(
            org => org.Id == organization.Id && org.Seats == seats));

        await sutProvider.GetDependency<IProviderPlanRepository>().Received(1).ReplaceAsync(Arg.Is<ProviderPlan>(
            pPlan => pPlan.AllocatedSeats == 60));
    }

    [Theory, BitAutoData]
    public async Task AssignSeatsToClientOrganization_BelowToAbove_NotProviderAdmin_ContactSupport(
        Provider provider,
        Organization organization,
        SutProvider<ProviderBillingService> sutProvider)
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

        sutProvider.GetDependency<IProviderPlanRepository>().GetByProviderId(provider.Id).Returns(providerPlans);

        // 95 seats currently assigned with a seat minimum of 100
        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(provider.Id).Returns(provider);

        var teamsMonthlyPlan = StaticStore.GetPlan(PlanType.TeamsMonthly);

        sutProvider.GetDependency<IProviderOrganizationRepository>().GetManyDetailsByProviderAsync(provider.Id).Returns(
        [
            new ProviderOrganizationOrganizationDetails
            {
                Plan = teamsMonthlyPlan.Name,
                Status = OrganizationStatusType.Managed,
                Seats = 60
            },
            new ProviderOrganizationOrganizationDetails
            {
                Plan = teamsMonthlyPlan.Name,
                Status = OrganizationStatusType.Managed,
                Seats = 35
            }
        ]);

        sutProvider.GetDependency<ICurrentContext>().ProviderProviderAdmin(provider.Id).Returns(false);

        await ThrowsBillingExceptionAsync(() =>
            sutProvider.Sut.AssignSeatsToClientOrganization(provider, organization, seats));
    }

    [Theory, BitAutoData]
    public async Task AssignSeatsToClientOrganization_BelowToAbove_Succeeds(
        Provider provider,
        Organization organization,
        SutProvider<ProviderBillingService> sutProvider)
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
        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(provider.Id).Returns(provider);

        var teamsMonthlyPlan = StaticStore.GetPlan(PlanType.TeamsMonthly);

        sutProvider.GetDependency<IProviderOrganizationRepository>().GetManyDetailsByProviderAsync(provider.Id).Returns(
        [
            new ProviderOrganizationOrganizationDetails
            {
                Plan = teamsMonthlyPlan.Name,
                Status = OrganizationStatusType.Managed,
                Seats = 60
            },
            new ProviderOrganizationOrganizationDetails
            {
                Plan = teamsMonthlyPlan.Name,
                Status = OrganizationStatusType.Managed,
                Seats = 35
            }
        ]);

        sutProvider.GetDependency<ICurrentContext>().ProviderProviderAdmin(provider.Id).Returns(true);

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
        SutProvider<ProviderBillingService> sutProvider)
    {
        provider.Type = ProviderType.Msp;

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
        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(provider.Id).Returns(provider);

        var teamsMonthlyPlan = StaticStore.GetPlan(PlanType.TeamsMonthly);

        sutProvider.GetDependency<IProviderOrganizationRepository>().GetManyDetailsByProviderAsync(provider.Id).Returns(
        [
            new ProviderOrganizationOrganizationDetails
            {
                Plan = teamsMonthlyPlan.Name,
                Status = OrganizationStatusType.Managed,
                Seats = 60
            },
            new ProviderOrganizationOrganizationDetails
            {
                Plan = teamsMonthlyPlan.Name,
                Status = OrganizationStatusType.Managed,
                Seats = 50
            }
        ]);

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
        SutProvider<ProviderBillingService> sutProvider)
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
        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(provider.Id).Returns(provider);

        var teamsMonthlyPlan = StaticStore.GetPlan(PlanType.TeamsMonthly);

        sutProvider.GetDependency<IProviderOrganizationRepository>().GetManyDetailsByProviderAsync(provider.Id).Returns(
        [
            new ProviderOrganizationOrganizationDetails
            {
                Plan = teamsMonthlyPlan.Name,
                Status = OrganizationStatusType.Managed,
                Seats = 60
            },
            new ProviderOrganizationOrganizationDetails
            {
                Plan = teamsMonthlyPlan.Name,
                Status = OrganizationStatusType.Managed,
                Seats = 50
            }
        ]);

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

    #endregion

    #region CreateCustomerForClientOrganization

    [Theory, BitAutoData]
    public async Task CreateCustomerForClientOrganization_ProviderNull_ThrowsArgumentNullException(
        Organization organization,
        SutProvider<ProviderBillingService> sutProvider) =>
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            sutProvider.Sut.CreateCustomerForClientOrganization(null, organization));

    [Theory, BitAutoData]
    public async Task CreateCustomerForClientOrganization_OrganizationNull_ThrowsArgumentNullException(
        Provider provider,
        SutProvider<ProviderBillingService> sutProvider) =>
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            sutProvider.Sut.CreateCustomerForClientOrganization(provider, null));

    [Theory, BitAutoData]
    public async Task CreateCustomerForClientOrganization_HasGatewayCustomerId_NoOp(
        Provider provider,
        Organization organization,
        SutProvider<ProviderBillingService> sutProvider)
    {
        organization.GatewayCustomerId = "customer_id";

        await sutProvider.Sut.CreateCustomerForClientOrganization(provider, organization);

        await sutProvider.GetDependency<ISubscriberService>().DidNotReceiveWithAnyArgs()
            .GetCustomerOrThrow(Arg.Any<ISubscriber>(), Arg.Any<CustomerGetOptions>());
    }

    [Theory, BitAutoData]
    public async Task CreateCustomer_ForClientOrg_Succeeds(
        Provider provider,
        Organization organization,
        SutProvider<ProviderBillingService> sutProvider)
    {
        organization.GatewayCustomerId = null;
        organization.Name = "Name";
        organization.BusinessName = "BusinessName";

        var providerCustomer = new Customer
        {
            Address = new Address
            {
                Country = "USA",
                PostalCode = "12345",
                Line1 = "123 Main St.",
                Line2 = "Unit 4",
                City = "Fake Town",
                State = "Fake State"
            },
            TaxIds = new StripeList<TaxId>
            {
                Data =
                [
                    new TaxId { Type = "TYPE", Value = "VALUE" }
                ]
            }
        };

        sutProvider.GetDependency<ISubscriberService>().GetCustomerOrThrow(provider, Arg.Is<CustomerGetOptions>(
                options => options.Expand.FirstOrDefault() == "tax_ids"))
            .Returns(providerCustomer);

        sutProvider.GetDependency<IGlobalSettings>().BaseServiceUri
            .Returns(new Bit.Core.Settings.GlobalSettings.BaseServiceUriSettings(new Bit.Core.Settings.GlobalSettings())
            {
                CloudRegion = "US"
            });

        sutProvider.GetDependency<IStripeAdapter>().CustomerCreateAsync(Arg.Is<CustomerCreateOptions>(
                options =>
                    options.Address.Country == providerCustomer.Address.Country &&
                    options.Address.PostalCode == providerCustomer.Address.PostalCode &&
                    options.Address.Line1 == providerCustomer.Address.Line1 &&
                    options.Address.Line2 == providerCustomer.Address.Line2 &&
                    options.Address.City == providerCustomer.Address.City &&
                    options.Address.State == providerCustomer.Address.State &&
                    options.Name == organization.DisplayName() &&
                    options.Description == $"{provider.Name} Client Organization" &&
                    options.Email == provider.BillingEmail &&
                    options.InvoiceSettings.CustomFields.FirstOrDefault().Name == "Organization" &&
                    options.InvoiceSettings.CustomFields.FirstOrDefault().Value == "Name" &&
                    options.Metadata["region"] == "US" &&
                    options.TaxIdData.FirstOrDefault().Type == providerCustomer.TaxIds.FirstOrDefault().Type &&
                    options.TaxIdData.FirstOrDefault().Value == providerCustomer.TaxIds.FirstOrDefault().Value))
            .Returns(new Customer { Id = "customer_id" });

        await sutProvider.Sut.CreateCustomerForClientOrganization(provider, organization);

        await sutProvider.GetDependency<IStripeAdapter>().Received(1).CustomerCreateAsync(Arg.Is<CustomerCreateOptions>(
            options =>
                options.Address.Country == providerCustomer.Address.Country &&
                options.Address.PostalCode == providerCustomer.Address.PostalCode &&
                options.Address.Line1 == providerCustomer.Address.Line1 &&
                options.Address.Line2 == providerCustomer.Address.Line2 &&
                options.Address.City == providerCustomer.Address.City &&
                options.Address.State == providerCustomer.Address.State &&
                options.Name == organization.DisplayName() &&
                options.Description == $"{provider.Name} Client Organization" &&
                options.Email == provider.BillingEmail &&
                options.InvoiceSettings.CustomFields.FirstOrDefault().Name == "Organization" &&
                options.InvoiceSettings.CustomFields.FirstOrDefault().Value == "Name" &&
                options.Metadata["region"] == "US" &&
                options.TaxIdData.FirstOrDefault().Type == providerCustomer.TaxIds.FirstOrDefault().Type &&
                options.TaxIdData.FirstOrDefault().Value == providerCustomer.TaxIds.FirstOrDefault().Value));

        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).ReplaceAsync(Arg.Is<Organization>(
            org => org.GatewayCustomerId == "customer_id"));
    }

    #endregion

    #region GenerateClientInvoiceReport

    [Theory, BitAutoData]
    public async Task GenerateClientInvoiceReport_NullInvoiceId_ThrowsArgumentNullException(
        SutProvider<ProviderBillingService> sutProvider) =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => sutProvider.Sut.GenerateClientInvoiceReport(null));

    [Theory, BitAutoData]
    public async Task GenerateClientInvoiceReport_NoInvoiceItems_ReturnsNull(
        string invoiceId,
        SutProvider<ProviderBillingService> sutProvider)
    {
        sutProvider.GetDependency<IProviderInvoiceItemRepository>().GetByInvoiceId(invoiceId).Returns([]);

        var reportContent = await sutProvider.Sut.GenerateClientInvoiceReport(invoiceId);

        Assert.Null(reportContent);
    }

    [Theory, BitAutoData]
    public async Task GenerateClientInvoiceReport_Succeeds(
        string invoiceId,
        SutProvider<ProviderBillingService> sutProvider)
    {
        var clientId = Guid.NewGuid();

        var invoiceItems = new List<ProviderInvoiceItem>
        {
            new ()
            {
                ClientId = clientId,
                ClientName = "Client 1",
                AssignedSeats = 50,
                UsedSeats = 30,
                PlanName = "Teams (Monthly)",
                Total = 500
            }
        };

        sutProvider.GetDependency<IProviderInvoiceItemRepository>().GetByInvoiceId(invoiceId).Returns(invoiceItems);

        var reportContent = await sutProvider.Sut.GenerateClientInvoiceReport(invoiceId);

        using var memoryStream = new MemoryStream(reportContent);

        using var streamReader = new StreamReader(memoryStream);

        using var csvReader = new CsvReader(streamReader, CultureInfo.InvariantCulture);

        var records = csvReader.GetRecords<ProviderClientInvoiceReportRow>().ToList();

        Assert.Single(records);

        var record = records.First();

        Assert.Equal(clientId.ToString(), record.Id);
        Assert.Equal("Client 1", record.Client);
        Assert.Equal(50, record.Assigned);
        Assert.Equal(30, record.Used);
        Assert.Equal(20, record.Remaining);
        Assert.Equal("Teams (Monthly)", record.Plan);
        Assert.Equal("$500.00", record.Total);
    }

    #endregion

    #region GetAssignedSeatTotalForPlanOrThrow

    [Theory, BitAutoData]
    public async Task GetAssignedSeatTotalForPlanOrThrow_NullProvider_ContactSupport(
        Guid providerId,
        SutProvider<ProviderBillingService> sutProvider)
        => await ThrowsBillingExceptionAsync(() =>
            sutProvider.Sut.GetAssignedSeatTotalForPlanOrThrow(providerId, PlanType.TeamsMonthly));

    [Theory, BitAutoData]
    public async Task GetAssignedSeatTotalForPlanOrThrow_ResellerProvider_ContactSupport(
        Guid providerId,
        Provider provider,
        SutProvider<ProviderBillingService> sutProvider)
    {
        provider.Type = ProviderType.Reseller;

        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(providerId).Returns(provider);

        await ThrowsBillingExceptionAsync(
            () => sutProvider.Sut.GetAssignedSeatTotalForPlanOrThrow(providerId, PlanType.TeamsMonthly));
    }

    [Theory, BitAutoData]
    public async Task GetAssignedSeatTotalForPlanOrThrow_Succeeds(
        Guid providerId,
        Provider provider,
        SutProvider<ProviderBillingService> sutProvider)
    {
        provider.Type = ProviderType.Msp;

        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(providerId).Returns(provider);

        var teamsMonthlyPlan = StaticStore.GetPlan(PlanType.TeamsMonthly);
        var enterpriseMonthlyPlan = StaticStore.GetPlan(PlanType.EnterpriseMonthly);

        var providerOrganizationOrganizationDetailList = new List<ProviderOrganizationOrganizationDetails>
        {
            new() { Plan = teamsMonthlyPlan.Name, Status = OrganizationStatusType.Managed, Seats = 10 },
            new() { Plan = teamsMonthlyPlan.Name, Status = OrganizationStatusType.Managed, Seats = 10 },
            new()
            {
                // Ignored because of status.
                Plan = teamsMonthlyPlan.Name, Status = OrganizationStatusType.Created, Seats = 100
            },
            new()
            {
                // Ignored because of plan.
                Plan = enterpriseMonthlyPlan.Name, Status = OrganizationStatusType.Managed, Seats = 30
            }
        };

        sutProvider.GetDependency<IProviderOrganizationRepository>()
            .GetManyDetailsByProviderAsync(providerId)
            .Returns(providerOrganizationOrganizationDetailList);

        var assignedSeatTotal =
            await sutProvider.Sut.GetAssignedSeatTotalForPlanOrThrow(providerId, PlanType.TeamsMonthly);

        Assert.Equal(20, assignedSeatTotal);
    }

    #endregion

    #region SetupCustomer

    [Theory, BitAutoData]
    public async Task SetupCustomer_NullProvider_ThrowsArgumentNullException(
        SutProvider<ProviderBillingService> sutProvider,
        TaxInfo taxInfo) =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => sutProvider.Sut.SetupCustomer(null, taxInfo));

    [Theory, BitAutoData]
    public async Task SetupCustomer_NullTaxInfo_ThrowsArgumentNullException(
        SutProvider<ProviderBillingService> sutProvider,
        Provider provider) =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => sutProvider.Sut.SetupCustomer(provider, null));

    [Theory, BitAutoData]
    public async Task SetupCustomer_MissingCountry_ContactSupport(
        SutProvider<ProviderBillingService> sutProvider,
        Provider provider,
        TaxInfo taxInfo)
    {
        taxInfo.BillingAddressCountry = null;

        await ThrowsBillingExceptionAsync(() => sutProvider.Sut.SetupCustomer(provider, taxInfo));

        await sutProvider.GetDependency<IStripeAdapter>()
            .DidNotReceiveWithAnyArgs()
            .CustomerGetAsync(Arg.Any<string>(), Arg.Any<CustomerGetOptions>());
    }

    [Theory, BitAutoData]
    public async Task SetupCustomer_MissingPostalCode_ContactSupport(
        SutProvider<ProviderBillingService> sutProvider,
        Provider provider,
        TaxInfo taxInfo)
    {
        taxInfo.BillingAddressCountry = null;

        await ThrowsBillingExceptionAsync(() => sutProvider.Sut.SetupCustomer(provider, taxInfo));

        await sutProvider.GetDependency<IStripeAdapter>()
            .DidNotReceiveWithAnyArgs()
            .CustomerGetAsync(Arg.Any<string>(), Arg.Any<CustomerGetOptions>());
    }

    [Theory, BitAutoData]
    public async Task SetupCustomer_Success(
        SutProvider<ProviderBillingService> sutProvider,
        Provider provider,
        TaxInfo taxInfo)
    {
        provider.Name = "MSP";

        taxInfo.BillingAddressCountry = "AD";

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        var expected = new Customer
        {
            Id = "customer_id",
            Tax = new CustomerTax { AutomaticTax = StripeConstants.AutomaticTaxStatus.Supported }
        };

        stripeAdapter.CustomerCreateAsync(Arg.Is<CustomerCreateOptions>(o =>
                o.Address.Country == taxInfo.BillingAddressCountry &&
                o.Address.PostalCode == taxInfo.BillingAddressPostalCode &&
                o.Address.Line1 == taxInfo.BillingAddressLine1 &&
                o.Address.Line2 == taxInfo.BillingAddressLine2 &&
                o.Address.City == taxInfo.BillingAddressCity &&
                o.Address.State == taxInfo.BillingAddressState &&
                o.Description == WebUtility.HtmlDecode(provider.BusinessName) &&
                o.Email == provider.BillingEmail &&
                o.InvoiceSettings.CustomFields.FirstOrDefault().Name == "Provider" &&
                o.InvoiceSettings.CustomFields.FirstOrDefault().Value == "MSP" &&
                o.Metadata["region"] == "" &&
                o.TaxIdData.FirstOrDefault().Type == taxInfo.TaxIdType &&
                o.TaxIdData.FirstOrDefault().Value == taxInfo.TaxIdNumber))
            .Returns(expected);

        var actual = await sutProvider.Sut.SetupCustomer(provider, taxInfo);

        Assert.Equivalent(expected, actual);
    }

    #endregion

    #region SetupSubscription

    [Theory, BitAutoData]
    public async Task SetupSubscription_NullProvider_ThrowsArgumentNullException(
        SutProvider<ProviderBillingService> sutProvider) =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => sutProvider.Sut.SetupSubscription(null));

    [Theory, BitAutoData]
    public async Task SetupSubscription_NoProviderPlans_ContactSupport(
        SutProvider<ProviderBillingService> sutProvider,
        Provider provider)
    {
        provider.GatewaySubscriptionId = null;

        sutProvider.GetDependency<ISubscriberService>().GetCustomerOrThrow(provider).Returns(new Customer
        {
            Id = "customer_id",
            Tax = new CustomerTax { AutomaticTax = StripeConstants.AutomaticTaxStatus.Supported }
        });

        sutProvider.GetDependency<IProviderPlanRepository>().GetByProviderId(provider.Id)
            .Returns(new List<ProviderPlan>());

        await ThrowsBillingExceptionAsync(() => sutProvider.Sut.SetupSubscription(provider));

        await sutProvider.GetDependency<IStripeAdapter>()
            .DidNotReceiveWithAnyArgs()
            .SubscriptionCreateAsync(Arg.Any<SubscriptionCreateOptions>());
    }

    [Theory, BitAutoData]
    public async Task SetupSubscription_NoProviderTeamsPlan_ContactSupport(
        SutProvider<ProviderBillingService> sutProvider,
        Provider provider)
    {
        provider.GatewaySubscriptionId = null;

        sutProvider.GetDependency<ISubscriberService>().GetCustomerOrThrow(provider).Returns(new Customer
        {
            Id = "customer_id",
            Tax = new CustomerTax { AutomaticTax = StripeConstants.AutomaticTaxStatus.Supported }
        });

        var providerPlans = new List<ProviderPlan> { new() { PlanType = PlanType.EnterpriseMonthly } };

        sutProvider.GetDependency<IProviderPlanRepository>().GetByProviderId(provider.Id)
            .Returns(providerPlans);

        await ThrowsBillingExceptionAsync(() => sutProvider.Sut.SetupSubscription(provider));

        await sutProvider.GetDependency<IStripeAdapter>()
            .DidNotReceiveWithAnyArgs()
            .SubscriptionCreateAsync(Arg.Any<SubscriptionCreateOptions>());
    }

    [Theory, BitAutoData]
    public async Task SetupSubscription_NoProviderEnterprisePlan_ContactSupport(
        SutProvider<ProviderBillingService> sutProvider,
        Provider provider)
    {
        provider.GatewaySubscriptionId = null;

        sutProvider.GetDependency<ISubscriberService>().GetCustomerOrThrow(provider).Returns(new Customer
        {
            Id = "customer_id",
            Tax = new CustomerTax { AutomaticTax = StripeConstants.AutomaticTaxStatus.Supported }
        });

        var providerPlans = new List<ProviderPlan> { new() { PlanType = PlanType.TeamsMonthly } };

        sutProvider.GetDependency<IProviderPlanRepository>().GetByProviderId(provider.Id)
            .Returns(providerPlans);

        await ThrowsBillingExceptionAsync(() => sutProvider.Sut.SetupSubscription(provider));

        await sutProvider.GetDependency<IStripeAdapter>()
            .DidNotReceiveWithAnyArgs()
            .SubscriptionCreateAsync(Arg.Any<SubscriptionCreateOptions>());
    }

    [Theory, BitAutoData]
    public async Task SetupSubscription_SubscriptionIncomplete_ThrowsBillingException(
        SutProvider<ProviderBillingService> sutProvider,
        Provider provider)
    {
        provider.GatewaySubscriptionId = null;

        sutProvider.GetDependency<ISubscriberService>().GetCustomerOrThrow(provider).Returns(new Customer
        {
            Id = "customer_id",
            Tax = new CustomerTax { AutomaticTax = StripeConstants.AutomaticTaxStatus.Supported }
        });

        var providerPlans = new List<ProviderPlan>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ProviderId = provider.Id,
                PlanType = PlanType.TeamsMonthly,
                SeatMinimum = 100,
                PurchasedSeats = 0,
                AllocatedSeats = 0
            },
            new()
            {
                Id = Guid.NewGuid(),
                ProviderId = provider.Id,
                PlanType = PlanType.EnterpriseMonthly,
                SeatMinimum = 100,
                PurchasedSeats = 0,
                AllocatedSeats = 0
            }
        };

        sutProvider.GetDependency<IProviderPlanRepository>().GetByProviderId(provider.Id)
            .Returns(providerPlans);

        sutProvider.GetDependency<IStripeAdapter>().SubscriptionCreateAsync(Arg.Any<SubscriptionCreateOptions>())
            .Returns(
                new Subscription { Id = "subscription_id", Status = StripeConstants.SubscriptionStatus.Incomplete });

        await ThrowsBillingExceptionAsync(() => sutProvider.Sut.SetupSubscription(provider));
    }

    [Theory, BitAutoData]
    public async Task SetupSubscription_Succeeds(
        SutProvider<ProviderBillingService> sutProvider,
        Provider provider)
    {
        provider.GatewaySubscriptionId = null;

        sutProvider.GetDependency<ISubscriberService>().GetCustomerOrThrow(provider).Returns(new Customer
        {
            Id = "customer_id",
            Tax = new CustomerTax { AutomaticTax = StripeConstants.AutomaticTaxStatus.Supported }
        });

        var providerPlans = new List<ProviderPlan>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ProviderId = provider.Id,
                PlanType = PlanType.TeamsMonthly,
                SeatMinimum = 100,
                PurchasedSeats = 0,
                AllocatedSeats = 0
            },
            new()
            {
                Id = Guid.NewGuid(),
                ProviderId = provider.Id,
                PlanType = PlanType.EnterpriseMonthly,
                SeatMinimum = 100,
                PurchasedSeats = 0,
                AllocatedSeats = 0
            }
        };

        sutProvider.GetDependency<IProviderPlanRepository>().GetByProviderId(provider.Id)
            .Returns(providerPlans);

        var teamsPlan = StaticStore.GetPlan(PlanType.TeamsMonthly);
        var enterprisePlan = StaticStore.GetPlan(PlanType.EnterpriseMonthly);

        var expected = new Subscription { Id = "subscription_id", Status = StripeConstants.SubscriptionStatus.Active };

        sutProvider.GetDependency<IStripeAdapter>().SubscriptionCreateAsync(Arg.Is<SubscriptionCreateOptions>(
            sub =>
                sub.AutomaticTax.Enabled == true &&
                sub.CollectionMethod == StripeConstants.CollectionMethod.SendInvoice &&
                sub.Customer == "customer_id" &&
                sub.DaysUntilDue == 30 &&
                sub.Items.Count == 2 &&
                sub.Items.ElementAt(0).Price == teamsPlan.PasswordManager.StripeProviderPortalSeatPlanId &&
                sub.Items.ElementAt(0).Quantity == 100 &&
                sub.Items.ElementAt(1).Price == enterprisePlan.PasswordManager.StripeProviderPortalSeatPlanId &&
                sub.Items.ElementAt(1).Quantity == 100 &&
                sub.Metadata["providerId"] == provider.Id.ToString() &&
                sub.OffSession == true &&
                sub.ProrationBehavior == StripeConstants.ProrationBehavior.CreateProrations)).Returns(expected);

        var actual = await sutProvider.Sut.SetupSubscription(provider);

        Assert.Equivalent(expected, actual);
    }

    #endregion
}
