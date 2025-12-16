using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Models.Sales;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Organizations.Services;
using Bit.Core.Billing.Payment.Queries;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Test.Billing.Mocks;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Services;

[SutProviderCustomize]
public class OrganizationBillingServiceTests
{
    #region GetMetadata

    [Theory, BitAutoData]
    public async Task GetMetadata_Succeeds(
        Guid organizationId,
        Organization organization,
        SutProvider<OrganizationBillingService> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);
        sutProvider.GetDependency<IPricingClient>().ListPlans().Returns(MockPlans.Plans.ToList());

        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(organization.PlanType)
            .Returns(MockPlans.Get(organization.PlanType));

        var subscriberService = sutProvider.GetDependency<ISubscriberService>();
        var organizationSeatCount = new OrganizationSeatCounts { Users = 1, Sponsored = 0 };
        var customer = new Customer();

        subscriberService
            .GetCustomer(organization)
            .Returns(customer);

        subscriberService.GetSubscription(organization, Arg.Is<SubscriptionGetOptions>(options =>
            options.Expand.Contains("discounts.coupon.applies_to"))).Returns(new Subscription
            {
                Discounts =
            [
                new Discount
                {
                    Coupon = new Coupon
                    {
                        Id = StripeConstants.CouponIDs.SecretsManagerStandalone,
                        AppliesTo = new CouponAppliesTo
                        {
                            Products = ["product_id"]
                        }
                    }
                }
            ],
                Items = new StripeList<SubscriptionItem>
                {
                    Data =
                [
                    new SubscriptionItem
                    {
                        Plan = new Plan
                        {
                            ProductId = "product_id"
                        }
                    }
                ]
                }
            });

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationSeatCounts { Users = 1, Sponsored = 0 });

        var metadata = await sutProvider.Sut.GetMetadata(organizationId);

        Assert.True(metadata!.IsOnSecretsManagerStandalone);
    }

    #endregion

    #region GetMetadata - Null Customer or Subscription

    [Theory, BitAutoData]
    public async Task GetMetadata_WhenCustomerOrSubscriptionIsNull_ReturnsDefaultMetadata(
        Guid organizationId,
        Organization organization,
        SutProvider<OrganizationBillingService> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);

        sutProvider.GetDependency<IPricingClient>().ListPlans().Returns(MockPlans.Plans.ToList());

        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(organization.PlanType)
            .Returns(MockPlans.Get(organization.PlanType));

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationSeatCounts { Users = 1, Sponsored = 0 });

        var subscriberService = sutProvider.GetDependency<ISubscriberService>();

        // Set up subscriber service to return null for customer
        subscriberService
            .GetCustomer(organization)
            .Returns((Customer)null);

        // Set up subscriber service to return null for subscription
        subscriberService.GetSubscription(organization, Arg.Is<SubscriptionGetOptions>(options =>
            options.Expand.Contains("discounts.coupon.applies_to"))).Returns((Subscription)null);

        var metadata = await sutProvider.Sut.GetMetadata(organizationId);

        Assert.NotNull(metadata);
        Assert.False(metadata!.IsOnSecretsManagerStandalone);
        Assert.Equal(1, metadata.OrganizationOccupiedSeats);
    }

    #endregion

    #region Finalize - Trial Settings

    [Theory, BitAutoData]
    public async Task NoPaymentMethodAndTrialPeriod_SetsMissingPaymentMethodCancelBehavior(
        Organization organization,
        SutProvider<OrganizationBillingService> sutProvider)
    {
        // Arrange
        var plan = MockPlans.Get(PlanType.TeamsAnnually);
        organization.PlanType = PlanType.TeamsAnnually;
        organization.GatewayCustomerId = "cus_test123";
        organization.GatewaySubscriptionId = null;

        var subscriptionSetup = new SubscriptionSetup
        {
            PlanType = PlanType.TeamsAnnually,
            PasswordManagerOptions = new SubscriptionSetup.PasswordManager
            {
                Seats = 5,
                Storage = null,
                PremiumAccess = false
            },
            SecretsManagerOptions = null,
            SkipTrial = false
        };

        var sale = new OrganizationSale
        {
            Organization = organization,
            SubscriptionSetup = subscriptionSetup
        };

        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(PlanType.TeamsAnnually)
            .Returns(plan);

        sutProvider.GetDependency<IHasPaymentMethodQuery>()
            .Run(organization)
            .Returns(false);

        var customer = new Customer
        {
            Id = "cus_test123",
            Tax = new CustomerTax { AutomaticTax = StripeConstants.AutomaticTaxStatus.Supported }
        };

        sutProvider.GetDependency<ISubscriberService>()
            .GetCustomerOrThrow(organization, Arg.Any<CustomerGetOptions>())
            .Returns(customer);

        SubscriptionCreateOptions capturedOptions = null;
        sutProvider.GetDependency<IStripeAdapter>()
            .CreateSubscriptionAsync(Arg.Do<SubscriptionCreateOptions>(options => capturedOptions = options))
            .Returns(new Subscription
            {
                Id = "sub_test123",
                Status = StripeConstants.SubscriptionStatus.Trialing
            });

        sutProvider.GetDependency<IOrganizationRepository>()
            .ReplaceAsync(organization)
            .Returns(Task.CompletedTask);

        // Act
        await sutProvider.Sut.Finalize(sale);

        // Assert
        await sutProvider.GetDependency<IStripeAdapter>()
            .Received(1)
            .CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>());

        Assert.NotNull(capturedOptions);
        Assert.Equal(7, capturedOptions.TrialPeriodDays);
        Assert.NotNull(capturedOptions.TrialSettings);
        Assert.NotNull(capturedOptions.TrialSettings.EndBehavior);
        Assert.Equal("cancel", capturedOptions.TrialSettings.EndBehavior.MissingPaymentMethod);
    }

    [Theory, BitAutoData]
    public async Task NoPaymentMethodButNoTrial_DoesNotSetMissingPaymentMethodBehavior(
        Organization organization,
        SutProvider<OrganizationBillingService> sutProvider)
    {
        // Arrange
        var plan = MockPlans.Get(PlanType.TeamsAnnually);
        organization.PlanType = PlanType.TeamsAnnually;
        organization.GatewayCustomerId = "cus_test123";
        organization.GatewaySubscriptionId = null;

        var subscriptionSetup = new SubscriptionSetup
        {
            PlanType = PlanType.TeamsAnnually,
            PasswordManagerOptions = new SubscriptionSetup.PasswordManager
            {
                Seats = 5,
                Storage = null,
                PremiumAccess = false
            },
            SecretsManagerOptions = null,
            SkipTrial = true // This will result in TrialPeriodDays = 0
        };

        var sale = new OrganizationSale
        {
            Organization = organization,
            SubscriptionSetup = subscriptionSetup
        };

        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(PlanType.TeamsAnnually)
            .Returns(plan);

        sutProvider.GetDependency<IHasPaymentMethodQuery>()
            .Run(organization)
            .Returns(false);

        var customer = new Customer
        {
            Id = "cus_test123",
            Tax = new CustomerTax { AutomaticTax = StripeConstants.AutomaticTaxStatus.Supported }
        };

        sutProvider.GetDependency<ISubscriberService>()
            .GetCustomerOrThrow(organization, Arg.Any<CustomerGetOptions>())
            .Returns(customer);

        SubscriptionCreateOptions capturedOptions = null;
        sutProvider.GetDependency<IStripeAdapter>()
            .CreateSubscriptionAsync(Arg.Do<SubscriptionCreateOptions>(options => capturedOptions = options))
            .Returns(new Subscription
            {
                Id = "sub_test123",
                Status = StripeConstants.SubscriptionStatus.Active
            });

        sutProvider.GetDependency<IOrganizationRepository>()
            .ReplaceAsync(organization)
            .Returns(Task.CompletedTask);

        // Act
        await sutProvider.Sut.Finalize(sale);

        // Assert
        await sutProvider.GetDependency<IStripeAdapter>()
            .Received(1)
            .CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>());

        Assert.NotNull(capturedOptions);
        Assert.Equal(0, capturedOptions.TrialPeriodDays);
        Assert.Null(capturedOptions.TrialSettings);
    }

    [Theory, BitAutoData]
    public async Task HasPaymentMethodAndTrialPeriod_DoesNotSetMissingPaymentMethodBehavior(
        Organization organization,
        SutProvider<OrganizationBillingService> sutProvider)
    {
        // Arrange
        var plan = MockPlans.Get(PlanType.TeamsAnnually);
        organization.PlanType = PlanType.TeamsAnnually;
        organization.GatewayCustomerId = "cus_test123";
        organization.GatewaySubscriptionId = null;

        var subscriptionSetup = new SubscriptionSetup
        {
            PlanType = PlanType.TeamsAnnually,
            PasswordManagerOptions = new SubscriptionSetup.PasswordManager
            {
                Seats = 5,
                Storage = null,
                PremiumAccess = false
            },
            SecretsManagerOptions = null,
            SkipTrial = false
        };

        var sale = new OrganizationSale
        {
            Organization = organization,
            SubscriptionSetup = subscriptionSetup
        };

        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(PlanType.TeamsAnnually)
            .Returns(plan);

        sutProvider.GetDependency<IHasPaymentMethodQuery>()
            .Run(organization)
            .Returns(true); // Has payment method

        var customer = new Customer
        {
            Id = "cus_test123",
            Tax = new CustomerTax { AutomaticTax = StripeConstants.AutomaticTaxStatus.Supported }
        };

        sutProvider.GetDependency<ISubscriberService>()
            .GetCustomerOrThrow(organization, Arg.Any<CustomerGetOptions>())
            .Returns(customer);

        SubscriptionCreateOptions capturedOptions = null;
        sutProvider.GetDependency<IStripeAdapter>()
            .CreateSubscriptionAsync(Arg.Do<SubscriptionCreateOptions>(options => capturedOptions = options))
            .Returns(new Subscription
            {
                Id = "sub_test123",
                Status = StripeConstants.SubscriptionStatus.Trialing
            });

        sutProvider.GetDependency<IOrganizationRepository>()
            .ReplaceAsync(organization)
            .Returns(Task.CompletedTask);

        // Act
        await sutProvider.Sut.Finalize(sale);

        // Assert
        await sutProvider.GetDependency<IStripeAdapter>()
            .Received(1)
            .CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>());

        Assert.NotNull(capturedOptions);
        Assert.Equal(7, capturedOptions.TrialPeriodDays);
        Assert.Null(capturedOptions.TrialSettings);
    }

    #endregion

    [Theory, BitAutoData]
    public async Task UpdateOrganizationNameAndEmail_UpdatesStripeCustomer(
        Organization organization,
        SutProvider<OrganizationBillingService> sutProvider)
    {
        organization.Name = "Short name";

        CustomerUpdateOptions capturedOptions = null;
        sutProvider.GetDependency<IStripeAdapter>()
            .UpdateCustomerAsync(
                Arg.Is<string>(id => id == organization.GatewayCustomerId),
                Arg.Do<CustomerUpdateOptions>(options => capturedOptions = options))
            .Returns(new Customer());

        // Act
        await sutProvider.Sut.UpdateOrganizationNameAndEmail(organization);

        // Assert
        await sutProvider.GetDependency<IStripeAdapter>()
            .Received(1)
            .UpdateCustomerAsync(
                organization.GatewayCustomerId,
                Arg.Any<CustomerUpdateOptions>());

        Assert.NotNull(capturedOptions);
        Assert.Equal(organization.BillingEmail, capturedOptions.Email);
        Assert.Equal(organization.DisplayName(), capturedOptions.Description);
        Assert.NotNull(capturedOptions.InvoiceSettings);
        Assert.NotNull(capturedOptions.InvoiceSettings.CustomFields);
        Assert.Single(capturedOptions.InvoiceSettings.CustomFields);

        var customField = capturedOptions.InvoiceSettings.CustomFields.First();
        Assert.Equal(organization.SubscriberType(), customField.Name);
        Assert.Equal(organization.DisplayName(), customField.Value);
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationNameAndEmail_WhenNameIsLong_TruncatesTo30Characters(
        Organization organization,
        SutProvider<OrganizationBillingService> sutProvider)
    {
        // Arrange
        organization.Name = "This is a very long organization name that exceeds thirty characters";

        CustomerUpdateOptions capturedOptions = null;
        sutProvider.GetDependency<IStripeAdapter>()
            .UpdateCustomerAsync(
                Arg.Is<string>(id => id == organization.GatewayCustomerId),
                Arg.Do<CustomerUpdateOptions>(options => capturedOptions = options))
            .Returns(new Customer());

        // Act
        await sutProvider.Sut.UpdateOrganizationNameAndEmail(organization);

        // Assert
        await sutProvider.GetDependency<IStripeAdapter>()
            .Received(1)
            .UpdateCustomerAsync(
                organization.GatewayCustomerId,
                Arg.Any<CustomerUpdateOptions>());

        Assert.NotNull(capturedOptions);
        Assert.NotNull(capturedOptions.InvoiceSettings);
        Assert.NotNull(capturedOptions.InvoiceSettings.CustomFields);

        var customField = capturedOptions.InvoiceSettings.CustomFields.First();
        Assert.Equal(30, customField.Value.Length);

        var expectedCustomFieldDisplayName = "This is a very long organizati";
        Assert.Equal(expectedCustomFieldDisplayName, customField.Value);
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationNameAndEmail_WhenGatewayCustomerIdIsNull_ThrowsBillingException(
        Organization organization,
        SutProvider<OrganizationBillingService> sutProvider)
    {
        // Arrange
        organization.GatewayCustomerId = null;
        organization.Name = "Test Organization";
        organization.BillingEmail = "billing@example.com";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BillingException>(
            () => sutProvider.Sut.UpdateOrganizationNameAndEmail(organization));

        Assert.Contains("Cannot update an organization in Stripe without a GatewayCustomerId.", exception.Response);

        await sutProvider.GetDependency<IStripeAdapter>()
            .DidNotReceiveWithAnyArgs()
            .UpdateCustomerAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>());
    }
}
