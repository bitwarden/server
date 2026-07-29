using System.Globalization;
using Bit.Commercial.Core.Billing.Providers.Models;
using Bit.Commercial.Core.Billing.Providers.Services;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Providers.Entities;
using Bit.Core.Billing.Providers.Models;
using Bit.Core.Billing.Providers.Repositories;
using Bit.Core.Billing.Providers.Services;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Test.Billing.Mocks;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Braintree;
using CsvHelper;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Stripe;
using Xunit;
using static Bit.Core.Test.Billing.Utilities;
using Address = Stripe.Address;
using Customer = Stripe.Customer;
using PaymentMethod = Stripe.PaymentMethod;
using Subscription = Stripe.Subscription;

namespace Bit.Commercial.Core.Test.Billing.Providers;

[SutProviderCustomize]
public class ProviderBillingServiceTests
{
    #region ChangePlan

    [Theory, BitAutoData]
    public async Task ChangePlan_NullProviderPlan_ThrowsBadRequestException(
        ChangeProviderPlanCommand command,
        SutProvider<ProviderBillingService> sutProvider)
    {
        // Arrange
        var providerPlanRepository = sutProvider.GetDependency<IProviderPlanRepository>();
        providerPlanRepository.GetByIdAsync(Arg.Any<Guid>()).Returns((ProviderPlan)null);

        // Act
        var actual = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.ChangePlan(command));

        // Assert
        Assert.Equal("Provider plan not found.", actual.Message);
    }

    [Theory, BitAutoData]
    public async Task ChangePlan_ProviderNotFound_DoesNothing(
        ChangeProviderPlanCommand command,
        SutProvider<ProviderBillingService> sutProvider)
    {
        // Arrange
        var providerPlanRepository = sutProvider.GetDependency<IProviderPlanRepository>();
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();
        var existingPlan = new ProviderPlan
        {
            Id = command.ProviderPlanId,
            PlanType = command.NewPlan,
            PurchasedSeats = 0,
            AllocatedSeats = 0,
            SeatMinimum = 0
        };
        providerPlanRepository
            .GetByIdAsync(Arg.Is<Guid>(p => p == command.ProviderPlanId))
            .Returns(existingPlan);

        // Act
        await sutProvider.Sut.ChangePlan(command);

        // Assert
        await providerPlanRepository.Received(0).ReplaceAsync(Arg.Any<ProviderPlan>());
        await stripeAdapter.Received(0).UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
    }

    [Theory, BitAutoData]
    public async Task ChangePlan_SameProviderPlan_DoesNothing(
        ChangeProviderPlanCommand command,
        SutProvider<ProviderBillingService> sutProvider)
    {
        // Arrange
        var providerPlanRepository = sutProvider.GetDependency<IProviderPlanRepository>();
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();
        var existingPlan = new ProviderPlan
        {
            Id = command.ProviderPlanId,
            PlanType = command.NewPlan,
            PurchasedSeats = 0,
            AllocatedSeats = 0,
            SeatMinimum = 0
        };
        providerPlanRepository
            .GetByIdAsync(Arg.Is<Guid>(p => p == command.ProviderPlanId))
            .Returns(existingPlan);

        // Act
        await sutProvider.Sut.ChangePlan(command);

        // Assert
        await providerPlanRepository.Received(0).ReplaceAsync(Arg.Any<ProviderPlan>());
        await stripeAdapter.Received(0).UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
    }

    [Theory, BitAutoData]
    public async Task ChangePlan_UpdatesSubscriptionCorrectly(
        Guid providerPlanId,
        Provider provider,
        SutProvider<ProviderBillingService> sutProvider)
    {
        // Arrange
        provider.Type = ProviderType.BusinessUnit;

        var providerPlanRepository = sutProvider.GetDependency<IProviderPlanRepository>();
        var existingPlan = new ProviderPlan
        {
            Id = providerPlanId,
            ProviderId = provider.Id,
            PlanType = PlanType.EnterpriseAnnually,
            PurchasedSeats = 2,
            AllocatedSeats = 10,
            SeatMinimum = 8
        };
        providerPlanRepository
            .GetByIdAsync(Arg.Is<Guid>(p => p == providerPlanId))
            .Returns(existingPlan);

        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(existingPlan.PlanType)
            .Returns(MockPlans.Get(existingPlan.PlanType));

        sutProvider.GetDependency<ISubscriberService>().GetSubscriptionOrThrow(provider)
            .Returns(new Subscription
            {
                Id = provider.GatewaySubscriptionId,
                Items = new StripeList<SubscriptionItem>
                {
                    Data =
                    [
                        new SubscriptionItem
                        {
                            Id = "si_ent_annual",
                            Price = new Price
                            {
                                Id = MockPlans.Get(PlanType.EnterpriseAnnually).PasswordManager
                                    .StripeProviderPortalSeatPlanId
                            },
                            Quantity = 10
                        }
                    ]
                }
            });

        var command =
            new ChangeProviderPlanCommand(provider, providerPlanId, PlanType.EnterpriseMonthly);

        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(command.NewPlan)
            .Returns(MockPlans.Get(command.NewPlan));

        // Act
        await sutProvider.Sut.ChangePlan(command);

        // Assert
        await providerPlanRepository.Received(1)
            .ReplaceAsync(Arg.Is<ProviderPlan>(p => p.PlanType == PlanType.EnterpriseMonthly));

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        await stripeAdapter.Received(1)
            .UpdateSubscriptionAsync(
                Arg.Is(provider.GatewaySubscriptionId),
                Arg.Is<SubscriptionUpdateOptions>(p =>
                    p.Items.Count(si => si.Id == "si_ent_annual" && si.Deleted == true) == 1));

        var newPlanCfg = MockPlans.Get(command.NewPlan);
        await stripeAdapter.Received(1)
            .UpdateSubscriptionAsync(
                Arg.Is(provider.GatewaySubscriptionId),
                Arg.Is<SubscriptionUpdateOptions>(p =>
                    p.Items.Count(si =>
                        si.Price == newPlanCfg.PasswordManager.StripeProviderPortalSeatPlanId &&
                        si.Deleted == default &&
                        si.Quantity == 10) == 1));
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
                options => options.Expand.Contains("tax") && options.Expand.Contains("tax_ids")))
            .Returns(providerCustomer);

        sutProvider.GetDependency<IGlobalSettings>().BaseServiceUri
            .Returns(new Bit.Core.Settings.GlobalSettings.BaseServiceUriSettings(new Bit.Core.Settings.GlobalSettings())
            {
                CloudRegion = "US"
            });

        sutProvider.GetDependency<IStripeAdapter>().CreateCustomerAsync(Arg.Is<CustomerCreateOptions>(
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

        await sutProvider.GetDependency<IStripeAdapter>().Received(1).CreateCustomerAsync(Arg.Is<CustomerCreateOptions>(
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

    [Theory, BitAutoData]
    public async Task CreateCustomerForClientOrganization_DoesNotSetTaxExempt(
        Provider provider,
        Organization organization,
        SutProvider<ProviderBillingService> sutProvider)
    {
        organization.GatewayCustomerId = null;
        organization.Name = "Name";

        var providerCustomer = new Customer
        {
            Address = new Address { Country = "DE", PostalCode = "10115" },
            TaxIds = new StripeList<TaxId> { Data = [] },
            TaxExempt = StripeConstants.TaxExempt.Reverse
        };

        sutProvider.GetDependency<ISubscriberService>().GetCustomerOrThrow(provider, Arg.Any<CustomerGetOptions>())
            .Returns(providerCustomer);

        sutProvider.GetDependency<IGlobalSettings>().BaseServiceUri
            .Returns(new Bit.Core.Settings.GlobalSettings.BaseServiceUriSettings(new Bit.Core.Settings.GlobalSettings())
            {
                CloudRegion = "US"
            });

        sutProvider.GetDependency<IStripeAdapter>()
            .CreateCustomerAsync(Arg.Any<CustomerCreateOptions>())
            .Returns(new Customer { Id = "customer_id" });

        await sutProvider.Sut.CreateCustomerForClientOrganization(provider, organization);

        await sutProvider.GetDependency<IStripeAdapter>().Received(1).CreateCustomerAsync(
            Arg.Is<CustomerCreateOptions>(options => options.TaxExempt == null));
    }

    #endregion

    #region GenerateClientInvoiceReport

    [Theory, BitAutoData]
    public async Task GenerateClientInvoiceReport_NullInvoiceId_ThrowsArgumentNullException(
        Guid providerId,
        SutProvider<ProviderBillingService> sutProvider) =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => sutProvider.Sut.GenerateClientInvoiceReport(providerId, null));

    [Theory, BitAutoData]
    public async Task GenerateClientInvoiceReport_NoInvoiceItems_ReturnsNull(
        Guid providerId,
        string invoiceId,
        SutProvider<ProviderBillingService> sutProvider)
    {
        sutProvider.GetDependency<IProviderInvoiceItemRepository>().GetByProviderIdAndInvoiceId(providerId, invoiceId).Returns([]);

        var reportContent = await sutProvider.Sut.GenerateClientInvoiceReport(providerId, invoiceId);

        Assert.Null(reportContent);
    }

    [Theory, BitAutoData]
    public async Task GenerateClientInvoiceReport_Succeeds(
        Guid providerId,
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

        sutProvider.GetDependency<IProviderInvoiceItemRepository>().GetByProviderIdAndInvoiceId(providerId, invoiceId).Returns(invoiceItems);

        var reportContent = await sutProvider.Sut.GenerateClientInvoiceReport(providerId, invoiceId);

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

    #region ScaleSeats

    [Theory, BitAutoData]
    public async Task ScaleSeats_BelowToBelow_Succeeds(
        Provider provider,
        SutProvider<ProviderBillingService> sutProvider)
    {
        var providerPlans = new List<ProviderPlan>
        {
            new()
            {
                Id = Guid.NewGuid(),
                PlanType = PlanType.TeamsMonthly,
                ProviderId = provider.Id,
                PurchasedSeats = 0,
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

        foreach (var plan in providerPlans)
        {
            sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(plan.PlanType)
                .Returns(MockPlans.Get(plan.PlanType));
        }

        sutProvider.GetDependency<IProviderPlanRepository>().GetByProviderId(provider.Id).Returns(providerPlans);

        var subscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { Price = new Price { Id = ProviderPriceAdapter.MSP.Active.Teams } },
                    new SubscriptionItem
                    {
                        Price = new Price { Id = ProviderPriceAdapter.MSP.Active.Enterprise }
                    }
                ]
            }
        };

        sutProvider.GetDependency<ISubscriberService>().GetSubscriptionOrThrow(provider).Returns(subscription);

        // 50 seats currently assigned with a seat minimum of 100
        var teamsMonthlyPlan = MockPlans.Get(PlanType.TeamsMonthly);

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

        await sutProvider.Sut.ScaleSeats(provider, PlanType.TeamsMonthly, 10);

        // 50 assigned seats + 10 seat scale up = 60 seats, well below the 100 minimum
        await sutProvider.GetDependency<IStripeAdapter>().DidNotReceiveWithAnyArgs().UpdateSubscriptionAsync(
            Arg.Any<string>(),
            Arg.Any<SubscriptionUpdateOptions>());

        await sutProvider.GetDependency<IProviderPlanRepository>().Received(1).ReplaceAsync(Arg.Is<ProviderPlan>(
            pPlan => pPlan.AllocatedSeats == 60));
    }

    [Theory, BitAutoData]
    public async Task ScaleSeats_BelowToAbove_Succeeds(
        Provider provider,
        SutProvider<ProviderBillingService> sutProvider)
    {
        var providerPlans = new List<ProviderPlan>
        {
            new()
            {
                Id = Guid.NewGuid(),
                PlanType = PlanType.TeamsMonthly,
                ProviderId = provider.Id,
                PurchasedSeats = 0,
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

        foreach (var plan in providerPlans)
        {
            sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(plan.PlanType)
                .Returns(MockPlans.Get(plan.PlanType));
        }

        var providerPlan = providerPlans.First();

        sutProvider.GetDependency<IProviderPlanRepository>().GetByProviderId(provider.Id).Returns(providerPlans);

        var subscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { Price = new Price { Id = ProviderPriceAdapter.MSP.Active.Teams } },
                    new SubscriptionItem
                    {
                        Price = new Price { Id = ProviderPriceAdapter.MSP.Active.Enterprise }
                    }
                ]
            }
        };

        sutProvider.GetDependency<ISubscriberService>().GetSubscriptionOrThrow(provider).Returns(subscription);

        // 95 seats currently assigned with a seat minimum of 100
        var teamsMonthlyPlan = MockPlans.Get(PlanType.TeamsMonthly);

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

        await sutProvider.Sut.ScaleSeats(provider, PlanType.TeamsMonthly, 10);

        // 95 current + 10 seat scale = 105 seats, 5 above the minimum
        await sutProvider.GetDependency<IStripeAdapter>().Received(1).UpdateSubscriptionAsync(
            provider.GatewaySubscriptionId,
            Arg.Is<SubscriptionUpdateOptions>(
                options =>
                    options.Items.First().Price == ProviderPriceAdapter.MSP.Active.Teams &&
                    options.Items.First().Quantity == 105));

        // 105 total seats - 100 minimum = 5 purchased seats
        await sutProvider.GetDependency<IProviderPlanRepository>().Received(1).ReplaceAsync(Arg.Is<ProviderPlan>(
            pPlan => pPlan.Id == providerPlan.Id && pPlan.PurchasedSeats == 5 && pPlan.AllocatedSeats == 105));
    }

    [Theory, BitAutoData]
    public async Task ScaleSeats_AboveToAbove_Succeeds(
        Provider provider,
        SutProvider<ProviderBillingService> sutProvider)
    {
        var providerPlans = new List<ProviderPlan>
        {
            new()
            {
                Id = Guid.NewGuid(),
                PlanType = PlanType.TeamsMonthly,
                ProviderId = provider.Id,
                PurchasedSeats = 10,
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

        foreach (var plan in providerPlans)
        {
            sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(plan.PlanType)
                .Returns(MockPlans.Get(plan.PlanType));
        }

        var providerPlan = providerPlans.First();

        sutProvider.GetDependency<IProviderPlanRepository>().GetByProviderId(provider.Id).Returns(providerPlans);

        var subscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { Price = new Price { Id = ProviderPriceAdapter.MSP.Active.Teams } },
                    new SubscriptionItem
                    {
                        Price = new Price { Id = ProviderPriceAdapter.MSP.Active.Enterprise }
                    }
                ]
            }
        };

        sutProvider.GetDependency<ISubscriberService>().GetSubscriptionOrThrow(provider).Returns(subscription);

        // 110 seats currently assigned with a seat minimum of 100
        var teamsMonthlyPlan = MockPlans.Get(PlanType.TeamsMonthly);

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

        await sutProvider.Sut.ScaleSeats(provider, PlanType.TeamsMonthly, 10);

        // 110 current + 10 seat scale up = 120 seats
        await sutProvider.GetDependency<IStripeAdapter>().Received(1).UpdateSubscriptionAsync(
            provider.GatewaySubscriptionId,
            Arg.Is<SubscriptionUpdateOptions>(
                options =>
                    options.Items.First().Price == ProviderPriceAdapter.MSP.Active.Teams &&
                    options.Items.First().Quantity == 120));

        // 120 total seats - 100 seat minimum = 20 purchased seats
        await sutProvider.GetDependency<IProviderPlanRepository>().Received(1).ReplaceAsync(Arg.Is<ProviderPlan>(
            pPlan => pPlan.Id == providerPlan.Id && pPlan.PurchasedSeats == 20 && pPlan.AllocatedSeats == 120));
    }

    [Theory, BitAutoData]
    public async Task ScaleSeats_AboveToBelow_Succeeds(
        Provider provider,
        SutProvider<ProviderBillingService> sutProvider)
    {
        var providerPlans = new List<ProviderPlan>
        {
            new()
            {
                Id = Guid.NewGuid(),
                PlanType = PlanType.TeamsMonthly,
                ProviderId = provider.Id,
                PurchasedSeats = 10,
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

        foreach (var plan in providerPlans)
        {
            sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(plan.PlanType)
                .Returns(MockPlans.Get(plan.PlanType));
        }

        var providerPlan = providerPlans.First();

        sutProvider.GetDependency<IProviderPlanRepository>().GetByProviderId(provider.Id).Returns(providerPlans);

        var subscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { Price = new Price { Id = ProviderPriceAdapter.MSP.Active.Teams } },
                    new SubscriptionItem
                    {
                        Price = new Price { Id = ProviderPriceAdapter.MSP.Active.Enterprise }
                    }
                ]
            }
        };

        sutProvider.GetDependency<ISubscriberService>().GetSubscriptionOrThrow(provider).Returns(subscription);

        // 110 seats currently assigned with a seat minimum of 100
        var teamsMonthlyPlan = MockPlans.Get(PlanType.TeamsMonthly);

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

        await sutProvider.Sut.ScaleSeats(provider, PlanType.TeamsMonthly, -30);

        // 110 seats - 30 scale down seats = 80 seats, below the 100 seat minimum.
        await sutProvider.GetDependency<IStripeAdapter>().Received(1).UpdateSubscriptionAsync(
            provider.GatewaySubscriptionId,
            Arg.Is<SubscriptionUpdateOptions>(
                options =>
                    options.Items.First().Price == ProviderPriceAdapter.MSP.Active.Teams &&
                    options.Items.First().Quantity == providerPlan.SeatMinimum!.Value));

        // Being below the seat minimum means no purchased seats.
        await sutProvider.GetDependency<IProviderPlanRepository>().Received(1).ReplaceAsync(Arg.Is<ProviderPlan>(
            pPlan => pPlan.Id == providerPlan.Id && pPlan.PurchasedSeats == 0 && pPlan.AllocatedSeats == 80));
    }

    #endregion

    #region SeatAdjustmentResultsInPurchase

    [Theory, BitAutoData]
    public async Task SeatAdjustmentResultsInPurchase_BelowToAbove_True(
        Provider provider,
        PlanType planType,
        SutProvider<ProviderBillingService> sutProvider)
    {
        sutProvider.GetDependency<IProviderPlanRepository>().GetByProviderId(provider.Id).Returns([
            new ProviderPlan
            {
                PlanType = planType,
                SeatMinimum = 10,
                AllocatedSeats = 0,
                PurchasedSeats = 0
            }
        ]);

        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(planType).Returns(MockPlans.Get(planType));

        sutProvider.GetDependency<IProviderOrganizationRepository>().GetManyDetailsByProviderAsync(provider.Id).Returns(
        [
            new ProviderOrganizationOrganizationDetails
            {
                Plan = MockPlans.Get(planType).Name,
                Status = OrganizationStatusType.Managed,
                Seats = 5
            }
        ]);

        const int seatAdjustment = 10;

        var result = await sutProvider.Sut.SeatAdjustmentResultsInPurchase(
            provider,
            planType,
            seatAdjustment);

        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async Task SeatAdjustmentResultsInPurchase_AboveToFurtherAbove_True(
        Provider provider,
        PlanType planType,
        SutProvider<ProviderBillingService> sutProvider)
    {
        sutProvider.GetDependency<IProviderPlanRepository>().GetByProviderId(provider.Id).Returns([
            new ProviderPlan
            {
                PlanType = planType,
                SeatMinimum = 10,
                AllocatedSeats = 0,
                PurchasedSeats = 5
            }
        ]);

        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(planType).Returns(MockPlans.Get(planType));

        sutProvider.GetDependency<IProviderOrganizationRepository>().GetManyDetailsByProviderAsync(provider.Id).Returns(
        [
            new ProviderOrganizationOrganizationDetails
            {
                Plan = MockPlans.Get(planType).Name,
                Status = OrganizationStatusType.Managed,
                Seats = 15
            }
        ]);

        const int seatAdjustment = 5;

        var result = await sutProvider.Sut.SeatAdjustmentResultsInPurchase(
            provider,
            planType,
            seatAdjustment);

        Assert.True(result);
    }

    #endregion

    #region SetupCustomer

    [Theory, BitAutoData]
    public async Task SetupCustomer_NullPaymentMethod_ThrowsNullReferenceException(
        SutProvider<ProviderBillingService> sutProvider,
        Provider provider,
        BillingAddress billingAddress)
    {
        await Assert.ThrowsAsync<NullReferenceException>(() =>
            sutProvider.Sut.SetupCustomer(provider, null, billingAddress));
    }

    [Theory, BitAutoData]
    public async Task SetupCustomer_WithBankAccount_Error_Reverts(
        SutProvider<ProviderBillingService> sutProvider,
        Provider provider,
        BillingAddress billingAddress)
    {
        provider.Name = "MSP";
        billingAddress.Country = "AD";
        billingAddress.TaxId = new TaxID("es_nif", "12345678Z");

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();
        var tokenizedPaymentMethod = new TokenizedPaymentMethod { Type = TokenizablePaymentMethodType.BankAccount, Token = "token" };

        stripeAdapter.ListSetupIntentsAsync(Arg.Is<SetupIntentListOptions>(options =>
            options.PaymentMethod == tokenizedPaymentMethod.Token)).Returns([
            new SetupIntent { Id = "setup_intent_id" }
        ]);

        stripeAdapter.CreateCustomerAsync(Arg.Is<CustomerCreateOptions>(o =>
                o.Address.Country == billingAddress.Country &&
                o.Address.PostalCode == billingAddress.PostalCode &&
                o.Address.Line1 == billingAddress.Line1 &&
                o.Address.Line2 == billingAddress.Line2 &&
                o.Address.City == billingAddress.City &&
                o.Address.State == billingAddress.State &&
                o.Description == provider.DisplayBusinessName() &&
                o.Email == provider.BillingEmail &&
                o.InvoiceSettings.CustomFields.FirstOrDefault().Name == provider.SubscriberType() &&
                o.InvoiceSettings.CustomFields.FirstOrDefault().Value == provider.DisplayName() &&
                o.Metadata["region"] == "" &&
                o.TaxIdData.FirstOrDefault().Type == billingAddress.TaxId.Code &&
                o.TaxIdData.FirstOrDefault().Value == billingAddress.TaxId.Value))
            .Throws<StripeException>();

        await Assert.ThrowsAsync<StripeException>(() =>
            sutProvider.Sut.SetupCustomer(provider, tokenizedPaymentMethod, billingAddress));

        await stripeAdapter.Received(1).CancelSetupIntentAsync("setup_intent_id", Arg.Is<SetupIntentCancelOptions>(options =>
            options.CancellationReason == "abandoned"));
    }

    [Theory, BitAutoData]
    public async Task SetupCustomer_WithPayPal_Error_Reverts(
        SutProvider<ProviderBillingService> sutProvider,
        Provider provider,
        BillingAddress billingAddress)
    {
        provider.Name = "MSP";
        billingAddress.Country = "AD";
        billingAddress.TaxId = new TaxID("es_nif", "12345678Z");

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();
        var tokenizedPaymentMethod = new TokenizedPaymentMethod { Type = TokenizablePaymentMethodType.PayPal, Token = "token" };

        sutProvider.GetDependency<ISubscriberService>().CreateBraintreeCustomer(provider, tokenizedPaymentMethod.Token)
            .Returns("braintree_customer_id");

        stripeAdapter.CreateCustomerAsync(Arg.Is<CustomerCreateOptions>(o =>
                o.Address.Country == billingAddress.Country &&
                o.Address.PostalCode == billingAddress.PostalCode &&
                o.Address.Line1 == billingAddress.Line1 &&
                o.Address.Line2 == billingAddress.Line2 &&
                o.Address.City == billingAddress.City &&
                o.Address.State == billingAddress.State &&
                o.Description == provider.DisplayBusinessName() &&
                o.Email == provider.BillingEmail &&
                o.InvoiceSettings.CustomFields.FirstOrDefault().Name == provider.SubscriberType() &&
                o.InvoiceSettings.CustomFields.FirstOrDefault().Value == provider.DisplayName() &&
                o.Metadata["region"] == "" &&
                o.Metadata["btCustomerId"] == "braintree_customer_id" &&
                o.TaxIdData.FirstOrDefault().Type == billingAddress.TaxId.Code &&
                o.TaxIdData.FirstOrDefault().Value == billingAddress.TaxId.Value))
            .Throws<StripeException>();

        await Assert.ThrowsAsync<StripeException>(() =>
            sutProvider.Sut.SetupCustomer(provider, tokenizedPaymentMethod, billingAddress));

        await sutProvider.GetDependency<IBraintreeGateway>().Customer.Received(1).DeleteAsync("braintree_customer_id");
    }

    [Theory, BitAutoData]
    public async Task SetupCustomer_WithBankAccount_Success(
        SutProvider<ProviderBillingService> sutProvider,
        Provider provider,
        BillingAddress billingAddress)
    {
        provider.Name = "MSP";
        billingAddress.Country = "AD";
        billingAddress.TaxId = new TaxID("es_nif", "12345678Z");

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        var expected = new Customer
        {
            Id = "customer_id",
            Tax = new CustomerTax { AutomaticTax = StripeConstants.AutomaticTaxStatus.Supported }
        };

        var tokenizedPaymentMethod = new TokenizedPaymentMethod { Type = TokenizablePaymentMethodType.BankAccount, Token = "token" };

        stripeAdapter.ListSetupIntentsAsync(Arg.Is<SetupIntentListOptions>(options =>
            options.PaymentMethod == tokenizedPaymentMethod.Token)).Returns([
            new SetupIntent { Id = "setup_intent_id" }
        ]);

        stripeAdapter.CreateCustomerAsync(Arg.Is<CustomerCreateOptions>(o =>
                o.Address.Country == billingAddress.Country &&
                o.Address.PostalCode == billingAddress.PostalCode &&
                o.Address.Line1 == billingAddress.Line1 &&
                o.Address.Line2 == billingAddress.Line2 &&
                o.Address.City == billingAddress.City &&
                o.Address.State == billingAddress.State &&
                o.Description == provider.DisplayBusinessName() &&
                o.Email == provider.BillingEmail &&
                o.InvoiceSettings.CustomFields.FirstOrDefault().Name == provider.SubscriberType() &&
                o.InvoiceSettings.CustomFields.FirstOrDefault().Value == provider.DisplayName() &&
                o.Metadata["region"] == "" &&
                o.TaxIdData.FirstOrDefault().Type == billingAddress.TaxId.Code &&
                o.TaxIdData.FirstOrDefault().Value == billingAddress.TaxId.Value))
            .Returns(expected);

        var actual = await sutProvider.Sut.SetupCustomer(provider, tokenizedPaymentMethod, billingAddress);

        Assert.Equivalent(expected, actual);

        await stripeAdapter.Received(1).UpdateSetupIntentAsync("setup_intent_id",
            Arg.Is<SetupIntentUpdateOptions>(options => options.Customer == expected.Id));
    }

    [Theory, BitAutoData]
    public async Task SetupCustomer_WithPayPal_Success(
        SutProvider<ProviderBillingService> sutProvider,
        Provider provider,
        BillingAddress billingAddress)
    {
        provider.Name = "MSP";
        billingAddress.Country = "AD";
        billingAddress.TaxId = new TaxID("es_nif", "12345678Z");

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        var expected = new Customer
        {
            Id = "customer_id",
            Tax = new CustomerTax { AutomaticTax = StripeConstants.AutomaticTaxStatus.Supported }
        };

        var tokenizedPaymentMethod = new TokenizedPaymentMethod { Type = TokenizablePaymentMethodType.PayPal, Token = "token" };

        sutProvider.GetDependency<ISubscriberService>().CreateBraintreeCustomer(provider, tokenizedPaymentMethod.Token)
            .Returns("braintree_customer_id");

        stripeAdapter.CreateCustomerAsync(Arg.Is<CustomerCreateOptions>(o =>
                o.Address.Country == billingAddress.Country &&
                o.Address.PostalCode == billingAddress.PostalCode &&
                o.Address.Line1 == billingAddress.Line1 &&
                o.Address.Line2 == billingAddress.Line2 &&
                o.Address.City == billingAddress.City &&
                o.Address.State == billingAddress.State &&
                o.Description == provider.DisplayBusinessName() &&
                o.Email == provider.BillingEmail &&
                o.InvoiceSettings.CustomFields.FirstOrDefault().Name == provider.SubscriberType() &&
                o.InvoiceSettings.CustomFields.FirstOrDefault().Value == provider.DisplayName() &&
                o.Metadata["region"] == "" &&
                o.Metadata["btCustomerId"] == "braintree_customer_id" &&
                o.TaxIdData.FirstOrDefault().Type == billingAddress.TaxId.Code &&
                o.TaxIdData.FirstOrDefault().Value == billingAddress.TaxId.Value))
            .Returns(expected);

        var actual = await sutProvider.Sut.SetupCustomer(provider, tokenizedPaymentMethod, billingAddress);

        Assert.Equivalent(expected, actual);
    }

    [Theory, BitAutoData]
    public async Task SetupCustomer_WithCard_Success(
        SutProvider<ProviderBillingService> sutProvider,
        Provider provider,
        BillingAddress billingAddress)
    {
        provider.Name = "MSP";
        billingAddress.Country = "AD";
        billingAddress.TaxId = new TaxID("es_nif", "12345678Z");

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        var expected = new Customer
        {
            Id = "customer_id",
            Tax = new CustomerTax { AutomaticTax = StripeConstants.AutomaticTaxStatus.Supported }
        };

        var tokenizedPaymentMethod = new TokenizedPaymentMethod { Type = TokenizablePaymentMethodType.Card, Token = "token" };

        stripeAdapter.CreateCustomerAsync(Arg.Is<CustomerCreateOptions>(o =>
                o.Address.Country == billingAddress.Country &&
                o.Address.PostalCode == billingAddress.PostalCode &&
                o.Address.Line1 == billingAddress.Line1 &&
                o.Address.Line2 == billingAddress.Line2 &&
                o.Address.City == billingAddress.City &&
                o.Address.State == billingAddress.State &&
                o.Description == provider.DisplayBusinessName() &&
                o.Email == provider.BillingEmail &&
                o.InvoiceSettings.DefaultPaymentMethod == tokenizedPaymentMethod.Token &&
                o.InvoiceSettings.CustomFields.FirstOrDefault().Name == provider.SubscriberType() &&
                o.InvoiceSettings.CustomFields.FirstOrDefault().Value == provider.DisplayName() &&
                o.Metadata["region"] == "" &&
                o.TaxIdData.FirstOrDefault().Type == billingAddress.TaxId.Code &&
                o.TaxIdData.FirstOrDefault().Value == billingAddress.TaxId.Value))
            .Returns(expected);

        var actual = await sutProvider.Sut.SetupCustomer(provider, tokenizedPaymentMethod, billingAddress);

        Assert.Equivalent(expected, actual);
    }

    [Theory, BitAutoData]
    public async Task SetupCustomer_WithInvalidTaxId_ThrowsBadRequestException(
        SutProvider<ProviderBillingService> sutProvider,
        Provider provider,
        BillingAddress billingAddress)
    {
        provider.Name = "MSP";
        billingAddress.Country = "AD";
        billingAddress.TaxId = new TaxID("es_nif", "invalid_tax_id");

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();
        var tokenizedPaymentMethod = new TokenizedPaymentMethod { Type = TokenizablePaymentMethodType.Card, Token = "token" };

        stripeAdapter.CreateCustomerAsync(Arg.Any<CustomerCreateOptions>())
            .Throws(new StripeException("Invalid tax ID") { StripeError = new StripeError { Code = "tax_id_invalid" } });

        var actual = await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.SetupCustomer(provider, tokenizedPaymentMethod, billingAddress));

        Assert.Equal("Your tax ID wasn't recognized for your selected country. Please ensure your country and tax ID are valid.", actual.Message);
    }

    [Theory, BitAutoData]
    public async Task SetupCustomer_DoesNotSetTaxExempt(
        SutProvider<ProviderBillingService> sutProvider,
        Provider provider,
        BillingAddress billingAddress)
    {
        provider.Name = "MSP";
        billingAddress.Country = "FR";
        billingAddress.TaxId = null;

        var tokenizedPaymentMethod = new TokenizedPaymentMethod { Type = TokenizablePaymentMethodType.Card, Token = "token" };
        var expected = new Customer { Id = "customer_id" };

        sutProvider.GetDependency<IStripeAdapter>()
            .CreateCustomerAsync(Arg.Any<CustomerCreateOptions>())
            .Returns(expected);

        var actual = await sutProvider.Sut.SetupCustomer(provider, tokenizedPaymentMethod, billingAddress);

        await sutProvider.GetDependency<IStripeAdapter>().Received(1).CreateCustomerAsync(
            Arg.Is<CustomerCreateOptions>(options =>
                options.Address.Country == billingAddress.Country &&
                options.TaxExempt == null));

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
            .CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>());
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

        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.EnterpriseMonthly)
            .Returns(MockPlans.Get(PlanType.EnterpriseMonthly));

        await ThrowsBillingExceptionAsync(() => sutProvider.Sut.SetupSubscription(provider));

        await sutProvider.GetDependency<IStripeAdapter>()
            .DidNotReceiveWithAnyArgs()
            .CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>());
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

        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(PlanType.TeamsMonthly)
            .Returns(MockPlans.Get(PlanType.TeamsMonthly));

        await ThrowsBillingExceptionAsync(() => sutProvider.Sut.SetupSubscription(provider));

        await sutProvider.GetDependency<IStripeAdapter>()
            .DidNotReceiveWithAnyArgs()
            .CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>());
    }

    [Theory, BitAutoData]
    public async Task SetupSubscription_SubscriptionIncomplete_ThrowsBillingException(
        SutProvider<ProviderBillingService> sutProvider,
        Provider provider)
    {
        provider.GatewaySubscriptionId = null;

        sutProvider.GetDependency<ISubscriberService>()
            .GetCustomerOrThrow(
                provider,
                Arg.Is<CustomerGetOptions>(p => p.Expand.Contains("tax") || p.Expand.Contains("tax_ids")))
            .Returns(new Customer
            {
                Id = "customer_id",
                Address = new Address { Country = "US" }
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

        foreach (var plan in providerPlans)
        {
            sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(plan.PlanType)
                .Returns(MockPlans.Get(plan.PlanType));
        }

        sutProvider.GetDependency<IProviderPlanRepository>().GetByProviderId(provider.Id)
            .Returns(providerPlans);

        sutProvider.GetDependency<IStripeAdapter>().CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>())
            .Returns(
                new Subscription { Id = "subscription_id", Status = StripeConstants.SubscriptionStatus.Incomplete });

        await ThrowsBillingExceptionAsync(() => sutProvider.Sut.SetupSubscription(provider));
    }

    [Theory, BitAutoData]
    public async Task SetupSubscription_SendInvoice_Succeeds(
        SutProvider<ProviderBillingService> sutProvider,
        Provider provider)
    {
        provider.Type = ProviderType.Msp;
        provider.GatewaySubscriptionId = null;

        var customer = new Customer
        {
            Id = "customer_id",
            Address = new Address { Country = "US" }
        };
        sutProvider.GetDependency<ISubscriberService>()
            .GetCustomerOrThrow(
                provider,
                Arg.Is<CustomerGetOptions>(p => p.Expand.Contains("tax") || p.Expand.Contains("tax_ids"))).Returns(customer);

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

        foreach (var plan in providerPlans)
        {
            sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(plan.PlanType)
                .Returns(MockPlans.Get(plan.PlanType));
        }

        sutProvider.GetDependency<IProviderPlanRepository>().GetByProviderId(provider.Id)
            .Returns(providerPlans);

        var expected = new Subscription { Id = "subscription_id", Status = StripeConstants.SubscriptionStatus.Active };

        sutProvider.GetDependency<IStripeAdapter>().CreateSubscriptionAsync(Arg.Is<SubscriptionCreateOptions>(
            sub =>
                sub.AutomaticTax.Enabled == true &&
                sub.CollectionMethod == StripeConstants.CollectionMethod.SendInvoice &&
                sub.Customer == "customer_id" &&
                sub.DaysUntilDue == 30 &&
                sub.Items.Count == 2 &&
                sub.Items.ElementAt(0).Price == ProviderPriceAdapter.MSP.Active.Teams &&
                sub.Items.ElementAt(0).Quantity == 100 &&
                sub.Items.ElementAt(1).Price == ProviderPriceAdapter.MSP.Active.Enterprise &&
                sub.Items.ElementAt(1).Quantity == 100 &&
                sub.Metadata["providerId"] == provider.Id.ToString() &&
                sub.OffSession == true &&
                sub.ProrationBehavior == StripeConstants.ProrationBehavior.CreateProrations)).Returns(expected);

        var actual = await sutProvider.Sut.SetupSubscription(provider);

        Assert.Equivalent(expected, actual);
    }

    [Theory, BitAutoData]
    public async Task SetupSubscription_ChargeAutomatically_HasCard_Succeeds(
        SutProvider<ProviderBillingService> sutProvider,
        Provider provider)
    {
        provider.Type = ProviderType.Msp;
        provider.GatewaySubscriptionId = null;

        var customer = new Customer
        {
            Id = "customer_id",
            Address = new Address { Country = "US" },
            InvoiceSettings = new CustomerInvoiceSettings
            {
                DefaultPaymentMethodId = "pm_123"
            }
        };

        sutProvider.GetDependency<ISubscriberService>()
            .GetCustomerOrThrow(
                provider,
                Arg.Is<CustomerGetOptions>(p => p.Expand.Contains("tax") || p.Expand.Contains("tax_ids"))).Returns(customer);

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

        foreach (var plan in providerPlans)
        {
            sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(plan.PlanType)
                .Returns(MockPlans.Get(plan.PlanType));
        }

        sutProvider.GetDependency<IProviderPlanRepository>().GetByProviderId(provider.Id)
            .Returns(providerPlans);

        var expected = new Subscription { Id = "subscription_id", Status = StripeConstants.SubscriptionStatus.Active };


        sutProvider.GetDependency<IStripeAdapter>().CreateSubscriptionAsync(Arg.Is<SubscriptionCreateOptions>(
            sub =>
                sub.AutomaticTax.Enabled == true &&
                sub.CollectionMethod == StripeConstants.CollectionMethod.ChargeAutomatically &&
                sub.Customer == "customer_id" &&
                sub.DaysUntilDue == null &&
                sub.Items.Count == 2 &&
                sub.Items.ElementAt(0).Price == ProviderPriceAdapter.MSP.Active.Teams &&
                sub.Items.ElementAt(0).Quantity == 100 &&
                sub.Items.ElementAt(1).Price == ProviderPriceAdapter.MSP.Active.Enterprise &&
                sub.Items.ElementAt(1).Quantity == 100 &&
                sub.Metadata["providerId"] == provider.Id.ToString() &&
                sub.OffSession == true &&
                sub.ProrationBehavior == StripeConstants.ProrationBehavior.CreateProrations &&
                sub.TrialPeriodDays == 14)).Returns(expected);

        var actual = await sutProvider.Sut.SetupSubscription(provider);

        Assert.Equivalent(expected, actual);
    }

    [Theory, BitAutoData]
    public async Task SetupSubscription_ChargeAutomatically_HasBankAccount_Succeeds(
        SutProvider<ProviderBillingService> sutProvider,
        Provider provider)
    {
        provider.Type = ProviderType.Msp;
        provider.GatewaySubscriptionId = null;

        var customer = new Customer
        {
            Id = "customer_id",
            Address = new Address { Country = "US" },
            InvoiceSettings = new CustomerInvoiceSettings(),
            Metadata = new Dictionary<string, string>()
        };

        sutProvider.GetDependency<ISubscriberService>()
            .GetCustomerOrThrow(
                provider,
                Arg.Is<CustomerGetOptions>(p => p.Expand.Contains("tax") || p.Expand.Contains("tax_ids"))).Returns(customer);

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

        foreach (var plan in providerPlans)
        {
            sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(plan.PlanType)
                .Returns(MockPlans.Get(plan.PlanType));
        }

        sutProvider.GetDependency<IProviderPlanRepository>().GetByProviderId(provider.Id)
            .Returns(providerPlans);

        var expected = new Subscription { Id = "subscription_id", Status = StripeConstants.SubscriptionStatus.Active };

        sutProvider.GetDependency<IStripeAdapter>().ListSetupIntentsAsync(Arg.Is<SetupIntentListOptions>(options =>
            options.Customer == customer.Id &&
            options.Expand.Contains("data.payment_method"))).Returns([
            new SetupIntent
            {
                Id = "seti_123",
                Status = "requires_action",
                NextAction = new SetupIntentNextAction
                {
                    VerifyWithMicrodeposits = new SetupIntentNextActionVerifyWithMicrodeposits()
                },
                PaymentMethod = new PaymentMethod
                {
                    UsBankAccount = new PaymentMethodUsBankAccount()
                }
            }
        ]);

        sutProvider.GetDependency<IStripeAdapter>().CreateSubscriptionAsync(Arg.Is<SubscriptionCreateOptions>(
            sub =>
                sub.AutomaticTax.Enabled == true &&
                sub.CollectionMethod == StripeConstants.CollectionMethod.ChargeAutomatically &&
                sub.Customer == "customer_id" &&
                sub.DaysUntilDue == null &&
                sub.Items.Count == 2 &&
                sub.Items.ElementAt(0).Price == ProviderPriceAdapter.MSP.Active.Teams &&
                sub.Items.ElementAt(0).Quantity == 100 &&
                sub.Items.ElementAt(1).Price == ProviderPriceAdapter.MSP.Active.Enterprise &&
                sub.Items.ElementAt(1).Quantity == 100 &&
                sub.Metadata["providerId"] == provider.Id.ToString() &&
                sub.OffSession == true &&
                sub.ProrationBehavior == StripeConstants.ProrationBehavior.CreateProrations &&
                sub.TrialPeriodDays == 14)).Returns(expected);

        var actual = await sutProvider.Sut.SetupSubscription(provider);

        Assert.Equivalent(expected, actual);
    }

    [Theory, BitAutoData]
    public async Task SetupSubscription_ChargeAutomatically_HasPayPal_Succeeds(
        SutProvider<ProviderBillingService> sutProvider,
        Provider provider)
    {
        provider.Type = ProviderType.Msp;
        provider.GatewaySubscriptionId = null;

        var customer = new Customer
        {
            Id = "customer_id",
            Address = new Address
            {
                Country = "US"
            },
            InvoiceSettings = new CustomerInvoiceSettings(),
            Metadata = new Dictionary<string, string>
            {
                ["btCustomerId"] = "braintree_customer_id"
            }
        };

        sutProvider.GetDependency<ISubscriberService>()
            .GetCustomerOrThrow(
                provider,
                Arg.Is<CustomerGetOptions>(p => p.Expand.Contains("tax") || p.Expand.Contains("tax_ids"))).Returns(customer);

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

        foreach (var plan in providerPlans)
        {
            sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(plan.PlanType)
                .Returns(MockPlans.Get(plan.PlanType));
        }

        sutProvider.GetDependency<IProviderPlanRepository>().GetByProviderId(provider.Id)
            .Returns(providerPlans);

        var expected = new Subscription { Id = "subscription_id", Status = StripeConstants.SubscriptionStatus.Active };


        sutProvider.GetDependency<IStripeAdapter>().CreateSubscriptionAsync(Arg.Is<SubscriptionCreateOptions>(
            sub =>
                sub.AutomaticTax.Enabled == true &&
                sub.CollectionMethod == StripeConstants.CollectionMethod.ChargeAutomatically &&
                sub.Customer == "customer_id" &&
                sub.DaysUntilDue == null &&
                sub.Items.Count == 2 &&
                sub.Items.ElementAt(0).Price == ProviderPriceAdapter.MSP.Active.Teams &&
                sub.Items.ElementAt(0).Quantity == 100 &&
                sub.Items.ElementAt(1).Price == ProviderPriceAdapter.MSP.Active.Enterprise &&
                sub.Items.ElementAt(1).Quantity == 100 &&
                sub.Metadata["providerId"] == provider.Id.ToString() &&
                sub.OffSession == true &&
                sub.ProrationBehavior == StripeConstants.ProrationBehavior.CreateProrations &&
                sub.TrialPeriodDays == 14)).Returns(expected);

        var actual = await sutProvider.Sut.SetupSubscription(provider);

        Assert.Equivalent(expected, actual);
    }

    [Theory, BitAutoData]
    public async Task SetupSubscription_ReverseCharge_Succeeds(
        SutProvider<ProviderBillingService> sutProvider,
        Provider provider)
    {
        provider.Type = ProviderType.Msp;
        provider.GatewaySubscriptionId = null;

        var customer = new Customer
        {
            Id = "customer_id",
            Address = new Address { Country = "CA" },
            InvoiceSettings = new CustomerInvoiceSettings
            {
                DefaultPaymentMethodId = "pm_123"
            }
        };

        sutProvider.GetDependency<ISubscriberService>()
            .GetCustomerOrThrow(
                provider,
                Arg.Is<CustomerGetOptions>(p => p.Expand.Contains("tax") || p.Expand.Contains("tax_ids"))).Returns(customer);

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

        foreach (var plan in providerPlans)
        {
            sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(plan.PlanType)
                .Returns(MockPlans.Get(plan.PlanType));
        }

        sutProvider.GetDependency<IProviderPlanRepository>().GetByProviderId(provider.Id)
            .Returns(providerPlans);

        var expected = new Subscription { Id = "subscription_id", Status = StripeConstants.SubscriptionStatus.Active };


        sutProvider.GetDependency<IStripeAdapter>().CreateSubscriptionAsync(Arg.Is<SubscriptionCreateOptions>(
            sub =>
                sub.AutomaticTax.Enabled == true &&
                sub.CollectionMethod == StripeConstants.CollectionMethod.ChargeAutomatically &&
                sub.Customer == "customer_id" &&
                sub.DaysUntilDue == null &&
                sub.Items.Count == 2 &&
                sub.Items.ElementAt(0).Price == ProviderPriceAdapter.MSP.Active.Teams &&
                sub.Items.ElementAt(0).Quantity == 100 &&
                sub.Items.ElementAt(1).Price == ProviderPriceAdapter.MSP.Active.Enterprise &&
                sub.Items.ElementAt(1).Quantity == 100 &&
                sub.Metadata["providerId"] == provider.Id.ToString() &&
                sub.OffSession == true &&
                sub.ProrationBehavior == StripeConstants.ProrationBehavior.CreateProrations &&
                sub.TrialPeriodDays == 14)).Returns(expected);

        var actual = await sutProvider.Sut.SetupSubscription(provider);

        Assert.Equivalent(expected, actual);
    }

    #endregion

    #region UpdateSeatMinimums

    [Theory, BitAutoData]
    public async Task UpdateSeatMinimums_NegativeSeatMinimum_ThrowsBadRequestException(
        Provider provider,
        SutProvider<ProviderBillingService> sutProvider)
    {
        // Arrange
        var command = new UpdateProviderSeatMinimumsCommand(
            provider,
            [
                (PlanType.TeamsMonthly, -10),
                (PlanType.EnterpriseMonthly, 50)
            ]);

        // Act
        var actual = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSeatMinimums(command));

        // Assert
        Assert.Equal("Provider seat minimums must be at least 0.", actual.Message);
    }

    [Theory, BitAutoData]
    public async Task UpdateSeatMinimums_NoPurchasedSeats_AllocatedHigherThanIncomingMinimum_UpdatesPurchasedSeats_SyncsStripeWithNewSeatMinimum(
        Provider provider,
        SutProvider<ProviderBillingService> sutProvider)
    {
        // Arrange
        provider.Type = ProviderType.Msp;

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();
        var providerPlanRepository = sutProvider.GetDependency<IProviderPlanRepository>();

        const string enterpriseLineItemId = "enterprise_line_item_id";
        const string teamsLineItemId = "teams_line_item_id";

        var enterprisePriceId = MockPlans.Get(PlanType.EnterpriseMonthly).PasswordManager.StripeProviderPortalSeatPlanId;
        var teamsPriceId = MockPlans.Get(PlanType.TeamsMonthly).PasswordManager.StripeProviderPortalSeatPlanId;

        var subscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Id = enterpriseLineItemId,
                        Price = new Price { Id = enterprisePriceId }
                    },
                    new SubscriptionItem
                    {
                        Id = teamsLineItemId,
                        Price = new Price { Id = teamsPriceId }
                    }
                ]
            }
        };

        sutProvider.GetDependency<ISubscriberService>().GetSubscriptionOrThrow(provider).Returns(subscription);

        var providerPlans = new List<ProviderPlan>
        {
            new() { PlanType = PlanType.EnterpriseMonthly, SeatMinimum = 50, PurchasedSeats = 0, AllocatedSeats = 20 },
            new() { PlanType = PlanType.TeamsMonthly, SeatMinimum = 30, PurchasedSeats = 0, AllocatedSeats = 25 }
        };

        foreach (var plan in providerPlans)
        {
            sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(plan.PlanType)
                .Returns(MockPlans.Get(plan.PlanType));
        }

        providerPlanRepository.GetByProviderId(provider.Id).Returns(providerPlans);

        var command = new UpdateProviderSeatMinimumsCommand(
            provider,
            [
                (PlanType.EnterpriseMonthly, 30),
                (PlanType.TeamsMonthly, 20)
            ]);

        // Act
        await sutProvider.Sut.UpdateSeatMinimums(command);

        // Assert
        await providerPlanRepository.Received(1).ReplaceAsync(Arg.Is<ProviderPlan>(
            providerPlan => providerPlan.PlanType == PlanType.EnterpriseMonthly && providerPlan.SeatMinimum == 30));

        await providerPlanRepository.Received(1).ReplaceAsync(Arg.Is<ProviderPlan>(
            providerPlan => providerPlan.PlanType == PlanType.TeamsMonthly && providerPlan.SeatMinimum == 20 && providerPlan.PurchasedSeats == 5));

        await stripeAdapter.Received(1).UpdateSubscriptionAsync(provider.GatewaySubscriptionId,
            Arg.Is<SubscriptionUpdateOptions>(
                options =>
                    options.Items.Count == 2 &&
                    options.Items.ElementAt(0).Id == enterpriseLineItemId &&
                    options.Items.ElementAt(0).Quantity == 30 &&
                    options.Items.ElementAt(1).Id == teamsLineItemId &&
                    options.Items.ElementAt(1).Quantity == 25));
    }

    [Theory, BitAutoData]
    public async Task UpdateSeatMinimums_NoPurchasedSeats_AllocatedLowerThanIncomingMinimum_SyncsStripeWithNewSeatMinimum(
        Provider provider,
        SutProvider<ProviderBillingService> sutProvider)
    {
        // Arrange
        provider.Type = ProviderType.Msp;

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();
        var providerPlanRepository = sutProvider.GetDependency<IProviderPlanRepository>();

        const string enterpriseLineItemId = "enterprise_line_item_id";
        const string teamsLineItemId = "teams_line_item_id";

        var enterprisePriceId = MockPlans.Get(PlanType.EnterpriseMonthly).PasswordManager.StripeProviderPortalSeatPlanId;
        var teamsPriceId = MockPlans.Get(PlanType.TeamsMonthly).PasswordManager.StripeProviderPortalSeatPlanId;

        var subscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Id = enterpriseLineItemId,
                        Price = new Price { Id = enterprisePriceId }
                    },
                    new SubscriptionItem
                    {
                        Id = teamsLineItemId,
                        Price = new Price { Id = teamsPriceId }
                    }
                ]
            }
        };

        sutProvider.GetDependency<ISubscriberService>().GetSubscriptionOrThrow(provider).Returns(subscription);

        var providerPlans = new List<ProviderPlan>
        {
            new() { PlanType = PlanType.EnterpriseMonthly, SeatMinimum = 50, PurchasedSeats = 0, AllocatedSeats = 40 },
            new() { PlanType = PlanType.TeamsMonthly, SeatMinimum = 30, PurchasedSeats = 0, AllocatedSeats = 15 }
        };

        foreach (var plan in providerPlans)
        {
            sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(plan.PlanType)
                .Returns(MockPlans.Get(plan.PlanType));
        }

        providerPlanRepository.GetByProviderId(provider.Id).Returns(providerPlans);

        var command = new UpdateProviderSeatMinimumsCommand(
            provider,
            [
                (PlanType.EnterpriseMonthly, 70),
                (PlanType.TeamsMonthly, 50)
            ]);

        // Act
        await sutProvider.Sut.UpdateSeatMinimums(command);

        // Assert
        await providerPlanRepository.Received(1).ReplaceAsync(Arg.Is<ProviderPlan>(
            providerPlan => providerPlan.PlanType == PlanType.EnterpriseMonthly && providerPlan.SeatMinimum == 70));

        await providerPlanRepository.Received(1).ReplaceAsync(Arg.Is<ProviderPlan>(
            providerPlan => providerPlan.PlanType == PlanType.TeamsMonthly && providerPlan.SeatMinimum == 50));

        await stripeAdapter.Received(1).UpdateSubscriptionAsync(provider.GatewaySubscriptionId,
            Arg.Is<SubscriptionUpdateOptions>(
                options =>
                    options.Items.Count == 2 &&
                    options.Items.ElementAt(0).Id == enterpriseLineItemId &&
                    options.Items.ElementAt(0).Quantity == 70 &&
                    options.Items.ElementAt(1).Id == teamsLineItemId &&
                    options.Items.ElementAt(1).Quantity == 50));
    }

    [Theory, BitAutoData]
    public async Task UpdateSeatMinimums_PurchasedSeats_NewMinimumLessThanTotal_UpdatesPurchasedSeats(
        Provider provider,
        SutProvider<ProviderBillingService> sutProvider)
    {
        // Arrange
        provider.Type = ProviderType.Msp;

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();
        var providerPlanRepository = sutProvider.GetDependency<IProviderPlanRepository>();

        const string enterpriseLineItemId = "enterprise_line_item_id";
        const string teamsLineItemId = "teams_line_item_id";

        var enterprisePriceId = MockPlans.Get(PlanType.EnterpriseMonthly).PasswordManager.StripeProviderPortalSeatPlanId;
        var teamsPriceId = MockPlans.Get(PlanType.TeamsMonthly).PasswordManager.StripeProviderPortalSeatPlanId;

        var subscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Id = enterpriseLineItemId,
                        Price = new Price { Id = enterprisePriceId }
                    },
                    new SubscriptionItem
                    {
                        Id = teamsLineItemId,
                        Price = new Price { Id = teamsPriceId }
                    }
                ]
            }
        };

        sutProvider.GetDependency<ISubscriberService>().GetSubscriptionOrThrow(provider).Returns(subscription);

        var providerPlans = new List<ProviderPlan>
        {
            new() { PlanType = PlanType.EnterpriseMonthly, SeatMinimum = 50, PurchasedSeats = 20 },
            new() { PlanType = PlanType.TeamsMonthly, SeatMinimum = 50, PurchasedSeats = 20 }
        };

        foreach (var plan in providerPlans)
        {
            sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(plan.PlanType)
                .Returns(MockPlans.Get(plan.PlanType));
        }

        providerPlanRepository.GetByProviderId(provider.Id).Returns(providerPlans);

        var command = new UpdateProviderSeatMinimumsCommand(
            provider,
            [
                (PlanType.EnterpriseMonthly, 60),
                (PlanType.TeamsMonthly, 60)
            ]);

        // Act
        await sutProvider.Sut.UpdateSeatMinimums(command);

        // Assert
        await providerPlanRepository.Received(1).ReplaceAsync(Arg.Is<ProviderPlan>(
            providerPlan => providerPlan.PlanType == PlanType.EnterpriseMonthly && providerPlan.SeatMinimum == 60 && providerPlan.PurchasedSeats == 10));

        await providerPlanRepository.Received(1).ReplaceAsync(Arg.Is<ProviderPlan>(
            providerPlan => providerPlan.PlanType == PlanType.TeamsMonthly && providerPlan.SeatMinimum == 60 && providerPlan.PurchasedSeats == 10));

        await stripeAdapter.DidNotReceiveWithAnyArgs()
            .UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
    }

    [Theory, BitAutoData]
    public async Task UpdateSeatMinimums_PurchasedSeats_NewMinimumGreaterThanTotal_ClearsPurchasedSeats_SyncsStripeWithNewSeatMinimum(
        Provider provider,
        SutProvider<ProviderBillingService> sutProvider)
    {
        // Arrange
        provider.Type = ProviderType.Msp;

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();
        var providerPlanRepository = sutProvider.GetDependency<IProviderPlanRepository>();

        const string enterpriseLineItemId = "enterprise_line_item_id";
        const string teamsLineItemId = "teams_line_item_id";

        var enterprisePriceId = MockPlans.Get(PlanType.EnterpriseMonthly).PasswordManager.StripeProviderPortalSeatPlanId;
        var teamsPriceId = MockPlans.Get(PlanType.TeamsMonthly).PasswordManager.StripeProviderPortalSeatPlanId;

        var subscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Id = enterpriseLineItemId,
                        Price = new Price { Id = enterprisePriceId }
                    },
                    new SubscriptionItem
                    {
                        Id = teamsLineItemId,
                        Price = new Price { Id = teamsPriceId }
                    }
                ]
            }
        };

        sutProvider.GetDependency<ISubscriberService>().GetSubscriptionOrThrow(provider).Returns(subscription);

        var providerPlans = new List<ProviderPlan>
        {
            new() { PlanType = PlanType.EnterpriseMonthly, SeatMinimum = 50, PurchasedSeats = 20 },
            new() { PlanType = PlanType.TeamsMonthly, SeatMinimum = 50, PurchasedSeats = 20 }
        };

        foreach (var plan in providerPlans)
        {
            sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(plan.PlanType)
                .Returns(MockPlans.Get(plan.PlanType));
        }

        providerPlanRepository.GetByProviderId(provider.Id).Returns(providerPlans);

        var command = new UpdateProviderSeatMinimumsCommand(
            provider,
            [
                (PlanType.EnterpriseMonthly, 80),
                (PlanType.TeamsMonthly, 80)
            ]);

        // Act
        await sutProvider.Sut.UpdateSeatMinimums(command);

        // Assert
        await providerPlanRepository.Received(1).ReplaceAsync(Arg.Is<ProviderPlan>(
            providerPlan => providerPlan.PlanType == PlanType.EnterpriseMonthly && providerPlan.SeatMinimum == 80 && providerPlan.PurchasedSeats == 0));

        await providerPlanRepository.Received(1).ReplaceAsync(Arg.Is<ProviderPlan>(
            providerPlan => providerPlan.PlanType == PlanType.TeamsMonthly && providerPlan.SeatMinimum == 80 && providerPlan.PurchasedSeats == 0));

        await stripeAdapter.Received(1).UpdateSubscriptionAsync(provider.GatewaySubscriptionId,
            Arg.Is<SubscriptionUpdateOptions>(
                options =>
                    options.Items.Count == 2 &&
                    options.Items.ElementAt(0).Id == enterpriseLineItemId &&
                    options.Items.ElementAt(0).Quantity == 80 &&
                    options.Items.ElementAt(1).Id == teamsLineItemId &&
                    options.Items.ElementAt(1).Quantity == 80));
    }

    [Theory, BitAutoData]
    public async Task UpdateSeatMinimums_SinglePlanTypeUpdate_Succeeds(
        Provider provider,
        SutProvider<ProviderBillingService> sutProvider)
    {
        // Arrange
        provider.Type = ProviderType.Msp;

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();
        var providerPlanRepository = sutProvider.GetDependency<IProviderPlanRepository>();

        const string enterpriseLineItemId = "enterprise_line_item_id";
        const string teamsLineItemId = "teams_line_item_id";

        var enterprisePriceId = MockPlans.Get(PlanType.EnterpriseMonthly).PasswordManager.StripeProviderPortalSeatPlanId;
        var teamsPriceId = MockPlans.Get(PlanType.TeamsMonthly).PasswordManager.StripeProviderPortalSeatPlanId;

        var subscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Id = enterpriseLineItemId,
                        Price = new Price { Id = enterprisePriceId }
                    },
                    new SubscriptionItem
                    {
                        Id = teamsLineItemId,
                        Price = new Price { Id = teamsPriceId }
                    }
                ]
            }
        };

        sutProvider.GetDependency<ISubscriberService>().GetSubscriptionOrThrow(provider).Returns(subscription);

        var providerPlans = new List<ProviderPlan>
        {
            new() { PlanType = PlanType.EnterpriseMonthly, SeatMinimum = 50, PurchasedSeats = 0 },
            new() { PlanType = PlanType.TeamsMonthly, SeatMinimum = 30, PurchasedSeats = 0 }
        };

        foreach (var plan in providerPlans)
        {
            sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(plan.PlanType)
                .Returns(MockPlans.Get(plan.PlanType));
        }

        providerPlanRepository.GetByProviderId(provider.Id).Returns(providerPlans);

        var command = new UpdateProviderSeatMinimumsCommand(
            provider,
            [
                (PlanType.EnterpriseMonthly, 70),
                (PlanType.TeamsMonthly, 30)
            ]);

        // Act
        await sutProvider.Sut.UpdateSeatMinimums(command);

        // Assert
        await providerPlanRepository.Received(1).ReplaceAsync(Arg.Is<ProviderPlan>(
            providerPlan => providerPlan.PlanType == PlanType.EnterpriseMonthly && providerPlan.SeatMinimum == 70));

        await providerPlanRepository.DidNotReceive().ReplaceAsync(Arg.Is<ProviderPlan>(
            providerPlan => providerPlan.PlanType == PlanType.TeamsMonthly));

        await stripeAdapter.Received(1).UpdateSubscriptionAsync(provider.GatewaySubscriptionId,
            Arg.Is<SubscriptionUpdateOptions>(
                options =>
                    options.Items.Count == 1 &&
                    options.Items.ElementAt(0).Id == enterpriseLineItemId &&
                    options.Items.ElementAt(0).Quantity == 70));
    }

    #endregion

    #region UpdateProviderNameAndEmail

    [Theory, BitAutoData]
    public async Task UpdateProviderNameAndEmail_NullGatewayCustomerId_LogsWarningAndReturns(
        Provider provider,
        SutProvider<ProviderBillingService> sutProvider)
    {
        // Arrange
        provider.GatewayCustomerId = null;
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        // Act
        await sutProvider.Sut.UpdateProviderNameAndEmail(provider);

        // Assert
        await stripeAdapter.DidNotReceive().UpdateCustomerAsync(
            Arg.Any<string>(),
            Arg.Any<CustomerUpdateOptions>());
    }

    [Theory, BitAutoData]
    public async Task UpdateProviderNameAndEmail_EmptyGatewayCustomerId_LogsWarningAndReturns(
        Provider provider,
        SutProvider<ProviderBillingService> sutProvider)
    {
        // Arrange
        provider.GatewayCustomerId = "";
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        // Act
        await sutProvider.Sut.UpdateProviderNameAndEmail(provider);

        // Assert
        await stripeAdapter.DidNotReceive().UpdateCustomerAsync(
            Arg.Any<string>(),
            Arg.Any<CustomerUpdateOptions>());
    }

    [Theory, BitAutoData]
    public async Task UpdateProviderNameAndEmail_NullProviderName_LogsWarningAndReturns(
        Provider provider,
        SutProvider<ProviderBillingService> sutProvider)
    {
        // Arrange
        provider.Name = null;
        provider.GatewayCustomerId = "cus_test123";
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        // Act
        await sutProvider.Sut.UpdateProviderNameAndEmail(provider);

        // Assert
        await stripeAdapter.DidNotReceive().UpdateCustomerAsync(
            Arg.Any<string>(),
            Arg.Any<CustomerUpdateOptions>());
    }

    [Theory, BitAutoData]
    public async Task UpdateProviderNameAndEmail_EmptyProviderName_LogsWarningAndReturns(
        Provider provider,
        SutProvider<ProviderBillingService> sutProvider)
    {
        // Arrange
        provider.Name = "";
        provider.GatewayCustomerId = "cus_test123";
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        // Act
        await sutProvider.Sut.UpdateProviderNameAndEmail(provider);

        // Assert
        await stripeAdapter.DidNotReceive().UpdateCustomerAsync(
            Arg.Any<string>(),
            Arg.Any<CustomerUpdateOptions>());
    }

    [Theory, BitAutoData]
    public async Task UpdateProviderNameAndEmail_ValidProvider_CallsStripeWithCorrectParameters(
        Provider provider,
        SutProvider<ProviderBillingService> sutProvider)
    {
        // Arrange
        provider.Name = "Test Provider";
        provider.BillingEmail = "billing@test.com";
        provider.GatewayCustomerId = "cus_test123";
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        // Act
        await sutProvider.Sut.UpdateProviderNameAndEmail(provider);

        // Assert
        await stripeAdapter.Received(1).UpdateCustomerAsync(
            provider.GatewayCustomerId,
            Arg.Is<CustomerUpdateOptions>(options =>
                options.Email == provider.BillingEmail &&
                options.Description == provider.Name &&
                options.InvoiceSettings.CustomFields.Count == 1 &&
                options.InvoiceSettings.CustomFields[0].Name == "Provider" &&
                options.InvoiceSettings.CustomFields[0].Value == provider.Name));
    }

    [Theory, BitAutoData]
    public async Task UpdateProviderNameAndEmail_LongProviderName_UsesFullName(
        Provider provider,
        SutProvider<ProviderBillingService> sutProvider)
    {
        // Arrange
        var longName = new string('A', 50); // 50 characters
        provider.Name = longName;
        provider.BillingEmail = "billing@test.com";
        provider.GatewayCustomerId = "cus_test123";
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        // Act
        await sutProvider.Sut.UpdateProviderNameAndEmail(provider);

        // Assert
        await stripeAdapter.Received(1).UpdateCustomerAsync(
            provider.GatewayCustomerId,
            Arg.Is<CustomerUpdateOptions>(options =>
                options.InvoiceSettings.CustomFields[0].Value == longName));
    }

    [Theory, BitAutoData]
    public async Task UpdateProviderNameAndEmail_NullBillingEmail_UpdatesWithNull(
        Provider provider,
        SutProvider<ProviderBillingService> sutProvider)
    {
        // Arrange
        provider.Name = "Test Provider";
        provider.BillingEmail = null;
        provider.GatewayCustomerId = "cus_test123";
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        // Act
        await sutProvider.Sut.UpdateProviderNameAndEmail(provider);

        // Assert
        await stripeAdapter.Received(1).UpdateCustomerAsync(
            provider.GatewayCustomerId,
            Arg.Is<CustomerUpdateOptions>(options =>
                options.Email == null &&
                options.Description == provider.Name));
    }

    #endregion

    #region AddExistingOrganization

    // Releasing any active migration subscription schedule before the Stripe cancel keeps
    // 2020-plan orgs with an attached SubscriptionSchedule from tripping a Stripe-side cancel
    // failure. Dropping the org's migration cohort assignment is delegated to Release by
    // passing organization.Id; the assignment-cleanup behavior itself is owned and covered by
    // PriceIncreaseScheduler.Release (see PriceIncreaseSchedulerTests).

    private static void ArrangeAddExistingOrganizationHappyPath(
        SutProvider<ProviderBillingService> sutProvider,
        Provider provider,
        Organization organization,
        PlanType planType = PlanType.EnterpriseAnnually2020)
    {
        organization.PlanType = planType;
        organization.Seats = 10;

        sutProvider.GetDependency<IStripeAdapter>()
            .CancelSubscriptionAsync(organization.GatewaySubscriptionId, Arg.Any<SubscriptionCancelOptions>())
            .Returns(new Subscription
            {
                Id = organization.GatewaySubscriptionId,
                LatestInvoice = new Invoice { Status = StripeConstants.InvoiceStatus.Open }
            });

        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(Arg.Any<PlanType>())
            .Returns(MockPlans.Get(PlanType.EnterpriseMonthly));

        sutProvider.GetDependency<IProviderPlanRepository>()
            .GetByProviderId(provider.Id)
            .Returns([
                new ProviderPlan
                {
                    PlanType = PlanType.EnterpriseMonthly,
                    SeatMinimum = 100,
                    AllocatedSeats = 0,
                    PurchasedSeats = 0
                }
            ]);

        sutProvider.GetDependency<IProviderOrganizationRepository>()
            .GetManyDetailsByProviderAsync(provider.Id)
            .Returns([]);

        sutProvider.GetDependency<ISubscriberService>()
            .GetCustomer(organization)
            .Returns(new Customer { Balance = 0 });
    }

    [Theory, BitAutoData]
    public async Task AddExistingOrganization_ReleasesMigrationScheduleWithOrgGatewayIds_BeforeStripeOperations(
        Provider provider,
        Organization organization,
        string key,
        SutProvider<ProviderBillingService> sutProvider)
    {
        // Arrange — provider and org have distinct gateway IDs so a regression that passed
        // the provider's IDs to Release instead of the org's would fail the Received check.
        provider.Type = ProviderType.Msp;
        provider.GatewayCustomerId = "cus_provider_msp";
        provider.GatewaySubscriptionId = "sub_provider_msp";
        organization.GatewayCustomerId = "cus_org_msp";
        organization.GatewaySubscriptionId = "sub_org_msp";
        ArrangeAddExistingOrganizationHappyPath(sutProvider, provider, organization);

        var priceIncreaseScheduler = sutProvider.GetDependency<IPriceIncreaseScheduler>();
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();
        var orgCustomerId = organization.GatewayCustomerId;
        var orgSubscriptionId = organization.GatewaySubscriptionId;
        var orgId = organization.Id;

        // Act
        await sutProvider.Sut.AddExistingOrganization(provider, organization, key);

        // Assert — Release must run first, with the Organization's own gateway IDs and its
        // own Id (not the Provider's), and must precede both Stripe mutations. Passing
        // organization.Id is what delegates the migration cohort cleanup to Release.
        Received.InOrder(() =>
        {
            priceIncreaseScheduler.Release(orgCustomerId, orgSubscriptionId, orgId);
            stripeAdapter.UpdateSubscriptionAsync(orgSubscriptionId, Arg.Any<SubscriptionUpdateOptions>());
            stripeAdapter.CancelSubscriptionAsync(orgSubscriptionId, Arg.Any<SubscriptionCancelOptions>());
        });
        await priceIncreaseScheduler.Received(1).Release(orgCustomerId, orgSubscriptionId, orgId);
        await priceIncreaseScheduler.Received(0)
            .Release(provider.GatewayCustomerId, Arg.Any<string>(), Arg.Any<Guid?>());
    }

    [Theory, BitAutoData]
    public async Task AddExistingOrganization_DelegatesCohortCleanupToRelease_AfterStripeCancel(
        Provider provider,
        Organization organization,
        string key,
        SutProvider<ProviderBillingService> sutProvider)
    {
        // Arrange — 2020-plan org. The service no longer touches the cohort assignment
        // repository directly; it hands organization.Id to Release, which owns the cleanup.
        provider.Type = ProviderType.Msp;
        organization.GatewayCustomerId = "cus_with_assignment";
        organization.GatewaySubscriptionId = "sub_with_assignment";
        ArrangeAddExistingOrganizationHappyPath(sutProvider, provider, organization);

        var priceIncreaseScheduler = sutProvider.GetDependency<IPriceIncreaseScheduler>();
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var providerOrganizationRepository = sutProvider.GetDependency<IProviderOrganizationRepository>();
        var customerId = organization.GatewayCustomerId;
        var subscriptionId = organization.GatewaySubscriptionId;
        var orgId = organization.Id;

        // Act
        await sutProvider.Sut.AddExistingOrganization(provider, organization, key);

        // Assert — Release is invoked once with organization.Id (delegating cohort cleanup)
        // before the Stripe cancel and the org-transition writes.
        Received.InOrder(() =>
        {
            priceIncreaseScheduler.Release(customerId, subscriptionId, orgId);
            stripeAdapter.CancelSubscriptionAsync(subscriptionId, Arg.Any<SubscriptionCancelOptions>());
            organizationRepository.ReplaceAsync(organization);
            providerOrganizationRepository.CreateAsync(Arg.Any<ProviderOrganization>());
        });
        await priceIncreaseScheduler.Received(1).Release(customerId, subscriptionId, orgId);
    }

    [Theory, BitAutoData]
    public async Task AddExistingOrganization_ContinuesEntireFlow_AfterReleaseSucceeds(
        Provider provider,
        Organization organization,
        string key,
        SutProvider<ProviderBillingService> sutProvider)
    {
        // Arrange
        provider.Type = ProviderType.Msp;
        organization.GatewayCustomerId = "cus_full_flow";
        organization.GatewaySubscriptionId = "sub_full_flow";
        ArrangeAddExistingOrganizationHappyPath(sutProvider, provider, organization);

        var priceIncreaseScheduler = sutProvider.GetDependency<IPriceIncreaseScheduler>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var providerOrganizationRepository = sutProvider.GetDependency<IProviderOrganizationRepository>();
        var eventService = sutProvider.GetDependency<IEventService>();
        // Capture before Act — AddExistingOrganization clears GatewaySubscriptionId
        // on the entity as part of the transition to provider-managed.
        var customerId = organization.GatewayCustomerId;
        var subscriptionId = organization.GatewaySubscriptionId;
        var orgId = organization.Id;

        // Act
        await sutProvider.Sut.AddExistingOrganization(provider, organization, key);

        // Assert
        await priceIncreaseScheduler.Received(1).Release(customerId, subscriptionId, orgId);
        await organizationRepository.Received(1).ReplaceAsync(organization);
        Assert.Equal(MockPlans.Get(PlanType.EnterpriseMonthly).HasRiskInsights, organization.UseRiskInsights);
        await providerOrganizationRepository.Received(1).CreateAsync(Arg.Is<ProviderOrganization>(po =>
            po.ProviderId == provider.Id && po.OrganizationId == organization.Id && po.Key == key));
        await eventService.Received(1).LogProviderOrganizationEventAsync(
            Arg.Is<ProviderOrganization>(po => po.OrganizationId == organization.Id),
            EventType.ProviderOrganization_Added);
    }

    [Theory, BitAutoData]
    public async Task AddExistingOrganization_CompletesAndDelegatesCohortCleanup_ForMoeProvider(
        Provider provider,
        Organization organization,
        string key,
        SutProvider<ProviderBillingService> sutProvider)
    {
        // Arrange — same flow under a BusinessUnit (MOE) provider. Exercises the
        // BusinessUnit branch of GetManagedPlanTypeAsync and confirms Release receives
        // organization.Id (delegating cohort cleanup) regardless of provider type.
        //
        // The discriminating setup: provider plan is TeamsMonthly while the org is on
        // EnterpriseAnnually2020. The MSP switch would map EnterpriseAnnually2020 →
        // EnterpriseMonthly, so a final PlanType of TeamsMonthly can only have come
        // from the BusinessUnit branch reading the provider's first plan. Without this
        // mismatch, deleting the entire BusinessUnit branch would still pass the test.
        provider.Type = ProviderType.BusinessUnit;
        organization.GatewayCustomerId = "cus_moe_baseline";
        organization.GatewaySubscriptionId = "sub_moe_baseline";
        ArrangeAddExistingOrganizationHappyPath(sutProvider, provider, organization);

        sutProvider.GetDependency<IProviderPlanRepository>()
            .GetByProviderId(provider.Id)
            .Returns([
                new ProviderPlan
                {
                    PlanType = PlanType.TeamsMonthly,
                    SeatMinimum = 100,
                    AllocatedSeats = 0,
                    PurchasedSeats = 0
                }
            ]);
        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(PlanType.TeamsMonthly)
            .Returns(MockPlans.Get(PlanType.TeamsMonthly));

        var priceIncreaseScheduler = sutProvider.GetDependency<IPriceIncreaseScheduler>();
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var providerOrganizationRepository = sutProvider.GetDependency<IProviderOrganizationRepository>();
        var customerId = organization.GatewayCustomerId;
        var subscriptionId = organization.GatewaySubscriptionId;
        var orgId = organization.Id;

        // Act
        await sutProvider.Sut.AddExistingOrganization(provider, organization, key);

        // Assert — Release (with organization.Id) precedes the Stripe operations and the
        // org transition commits, and the org lands on the provider's first plan
        // (TeamsMonthly) — proving the BusinessUnit branch ran.
        Received.InOrder(() =>
        {
            priceIncreaseScheduler.Release(customerId, subscriptionId, orgId);
            stripeAdapter.UpdateSubscriptionAsync(subscriptionId, Arg.Any<SubscriptionUpdateOptions>());
            stripeAdapter.CancelSubscriptionAsync(subscriptionId, Arg.Any<SubscriptionCancelOptions>());
            organizationRepository.ReplaceAsync(organization);
            providerOrganizationRepository.CreateAsync(Arg.Any<ProviderOrganization>());
        });
        await priceIncreaseScheduler.Received(1).Release(customerId, subscriptionId, orgId);
        await organizationRepository.Received(1).ReplaceAsync(Arg.Is<Organization>(o =>
            o.Id == organization.Id && o.PlanType == PlanType.TeamsMonthly));
    }

    [Theory, BitAutoData]
    public async Task AddExistingOrganization_PropagatesAndShortCircuits_WhenReleaseFails(
        Provider provider,
        Organization organization,
        string key,
        SutProvider<ProviderBillingService> sutProvider)
    {
        // Arrange — Release throws before any other I/O. No Stripe cancel, no repo writes,
        // no event log should fire.
        provider.Type = ProviderType.Msp;
        organization.GatewayCustomerId = "cus_release_fails";
        organization.GatewaySubscriptionId = "sub_release_fails";

        var priceIncreaseScheduler = sutProvider.GetDependency<IPriceIncreaseScheduler>();
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var providerOrganizationRepository = sutProvider.GetDependency<IProviderOrganizationRepository>();
        var eventService = sutProvider.GetDependency<IEventService>();
        var subscriptionId = organization.GatewaySubscriptionId;

        priceIncreaseScheduler
            .Release(organization.GatewayCustomerId, subscriptionId, organization.Id)
            .ThrowsAsync(new StripeException("simulated release failure"));

        // Act
        await Assert.ThrowsAsync<StripeException>(() =>
            sutProvider.Sut.AddExistingOrganization(provider, organization, key));

        // Assert — nothing downstream of Release fired.
        await stripeAdapter.Received(0)
            .UpdateSubscriptionAsync(subscriptionId, Arg.Any<SubscriptionUpdateOptions>());
        await stripeAdapter.Received(0)
            .CancelSubscriptionAsync(subscriptionId, Arg.Any<SubscriptionCancelOptions>());
        await organizationRepository.Received(0).ReplaceAsync(Arg.Any<Organization>());
        await providerOrganizationRepository.Received(0).CreateAsync(Arg.Any<ProviderOrganization>());
        await eventService.Received(0).LogProviderOrganizationEventAsync(
            Arg.Any<ProviderOrganization>(), Arg.Any<EventType>());
    }

    [Theory, BitAutoData]
    public async Task AddExistingOrganization_StripeCancelSucceeds_OnlyWhenReleasePrecedesIt(
        Provider provider,
        Organization organization,
        string key,
        SutProvider<ProviderBillingService> sutProvider)
    {
        // Arrange — simulates the Stripe-side contract this PR exists to satisfy:
        // CancelSubscriptionAsync throws when an active SubscriptionSchedule is attached.
        // The mock returns success only after Release has run; without Release, the cancel
        // call throws the same error shape Stripe would produce in production. This locks
        // in the "Release must precede cancel" invariant at the contract level, not just
        // at the call-order level — a refactor that removes the Release call but preserves
        // call order would still fail this test.
        provider.Type = ProviderType.Msp;
        organization.GatewayCustomerId = "cus_stripe_contract";
        organization.GatewaySubscriptionId = "sub_stripe_contract";
        organization.PlanType = PlanType.EnterpriseAnnually2020;
        organization.Seats = 10;

        var releaseHasRun = false;
        var priceIncreaseScheduler = sutProvider.GetDependency<IPriceIncreaseScheduler>();
        priceIncreaseScheduler
            .When(s => s.Release(
                organization.GatewayCustomerId, organization.GatewaySubscriptionId, organization.Id))
            .Do(_ => releaseHasRun = true);

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();
        stripeAdapter
            .CancelSubscriptionAsync(organization.GatewaySubscriptionId, Arg.Any<SubscriptionCancelOptions>())
            .Returns(_ => releaseHasRun
                ? Task.FromResult(new Subscription
                {
                    Id = organization.GatewaySubscriptionId,
                    LatestInvoice = new Invoice { Status = StripeConstants.InvoiceStatus.Open }
                })
                : Task.FromException<Subscription>(new StripeException(
                    "This subscription is owned by an active subscription schedule.")));

        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(Arg.Any<PlanType>())
            .Returns(MockPlans.Get(PlanType.EnterpriseMonthly));
        sutProvider.GetDependency<IProviderPlanRepository>()
            .GetByProviderId(provider.Id)
            .Returns([
                new ProviderPlan
                {
                    PlanType = PlanType.EnterpriseMonthly,
                    SeatMinimum = 100,
                    AllocatedSeats = 0,
                    PurchasedSeats = 0
                }
            ]);
        sutProvider.GetDependency<IProviderOrganizationRepository>()
            .GetManyDetailsByProviderAsync(provider.Id)
            .Returns([]);
        sutProvider.GetDependency<ISubscriberService>()
            .GetCustomer(organization)
            .Returns(new Customer { Balance = 0 });

        // Act — must not throw. If Release were removed or moved after Cancel, the
        // arranged StripeException would surface and Assert.ThrowsAsync would be needed.
        await sutProvider.Sut.AddExistingOrganization(provider, organization, key);

        // Assert — both calls fired, Release first.
        await priceIncreaseScheduler.Received(1)
            .Release(organization.GatewayCustomerId, "sub_stripe_contract", organization.Id);
        await stripeAdapter.Received(1)
            .CancelSubscriptionAsync("sub_stripe_contract", Arg.Any<SubscriptionCancelOptions>());
    }

    #endregion
}
