using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Models.Sales;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Organizations.Services;
using Bit.Core.Billing.Payment.Queries;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
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

    #region Finalize - Trial Settings

    [Theory, BitAutoData]
    public async Task NoPaymentMethodAndTrialPeriod_SetsMissingPaymentMethodCancelBehavior(
        Organization organization,
        User owner,
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
            SubscriptionSetup = subscriptionSetup,
            Owner = owner
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
        User owner,
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
            SubscriptionSetup = subscriptionSetup,
            Owner = owner
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
        User owner,
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
            SubscriptionSetup = subscriptionSetup,
            Owner = owner
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

    #region Finalize - Coupon Validation

    [Theory, BitAutoData]
    public async Task Finalize_WithValidCoupon_SuccessfullyCreatesSubscription(
        Organization organization,
        User owner,
        SutProvider<OrganizationBillingService> sutProvider)
    {
        // Arrange
        var plan = MockPlans.Get(PlanType.FamiliesAnnually);
        organization.PlanType = PlanType.FamiliesAnnually;
        organization.GatewayCustomerId = "cus_test123";
        organization.GatewaySubscriptionId = null;

        var customerSetup = new CustomerSetup
        {
            DiscountCoupons = ["VALID_COUPON"]
        };

        var subscriptionSetup = new SubscriptionSetup
        {
            PlanType = PlanType.FamiliesAnnually,
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
            CustomerSetup = customerSetup,
            SubscriptionSetup = subscriptionSetup,
            Owner = owner
        };

        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(PlanType.FamiliesAnnually)
            .Returns(plan);

        sutProvider.GetDependency<ISubscriptionDiscountService>()
            .ValidateDiscountEligibilityForUserAsync(
                owner,
                Arg.Is<IReadOnlyList<string>>(a => a.SequenceEqual(new[] { "VALID_COUPON" })),
                DiscountTierType.Families)
            .Returns(true);

        sutProvider.GetDependency<IHasPaymentMethodQuery>()
            .Run(organization)
            .Returns(true);

        var customer = new Customer
        {
            Id = "cus_test123",
            Tax = new CustomerTax { AutomaticTax = StripeConstants.AutomaticTaxStatus.Supported }
        };

        sutProvider.GetDependency<ISubscriberService>()
            .GetCustomerOrThrow(organization, Arg.Any<CustomerGetOptions>())
            .Returns(customer);

        sutProvider.GetDependency<IStripeAdapter>()
            .CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>())
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
        await sutProvider.GetDependency<ISubscriptionDiscountService>()
            .Received(1)
            .ValidateDiscountEligibilityForUserAsync(
                owner,
                Arg.Is<IReadOnlyList<string>>(a => a.SequenceEqual(new[] { "VALID_COUPON" })),
                DiscountTierType.Families);

        await sutProvider.GetDependency<IStripeAdapter>()
            .Received(1)
            .CreateSubscriptionAsync(Arg.Is<SubscriptionCreateOptions>(opts =>
                opts.Discounts != null && opts.Discounts.Count == 1 && opts.Discounts[0].Coupon == "VALID_COUPON"));
    }

    [Theory, BitAutoData]
    public async Task Finalize_WithInvalidCoupon_ThrowsBadRequestException(
        Organization organization,
        User owner,
        SutProvider<OrganizationBillingService> sutProvider)
    {
        // Arrange
        var plan = MockPlans.Get(PlanType.FamiliesAnnually);
        organization.PlanType = PlanType.FamiliesAnnually;
        organization.GatewayCustomerId = "cus_test123";
        organization.GatewaySubscriptionId = null;

        var customerSetup = new CustomerSetup
        {
            DiscountCoupons = ["INVALID_COUPON"]
        };

        var subscriptionSetup = new SubscriptionSetup
        {
            PlanType = PlanType.FamiliesAnnually,
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
            CustomerSetup = customerSetup,
            SubscriptionSetup = subscriptionSetup,
            Owner = owner
        };

        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(PlanType.FamiliesAnnually)
            .Returns(plan);

        // Return false to simulate invalid coupon
        sutProvider.GetDependency<ISubscriptionDiscountService>()
            .ValidateDiscountEligibilityForUserAsync(
                owner,
                Arg.Is<IReadOnlyList<string>>(a => a.SequenceEqual(new[] { "INVALID_COUPON" })),
                DiscountTierType.Families)
            .Returns(false);

        sutProvider.GetDependency<IHasPaymentMethodQuery>()
            .Run(organization)
            .Returns(true);

        var customer = new Customer
        {
            Id = "cus_test123",
            Tax = new CustomerTax { AutomaticTax = StripeConstants.AutomaticTaxStatus.Supported }
        };

        sutProvider.GetDependency<ISubscriberService>()
            .GetCustomerOrThrow(organization, Arg.Any<CustomerGetOptions>())
            .Returns(customer);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.Finalize(sale));
        Assert.Equal("Discount expired. Please review your cart total and try again", exception.Message);

        await sutProvider.GetDependency<ISubscriptionDiscountService>()
            .Received(1)
            .ValidateDiscountEligibilityForUserAsync(
                owner,
                Arg.Is<IReadOnlyList<string>>(a => a.SequenceEqual(new[] { "INVALID_COUPON" })),
                DiscountTierType.Families);

        // Verify subscription was NOT created
        await sutProvider.GetDependency<IStripeAdapter>()
            .DidNotReceive()
            .CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>());
    }

    [Theory, BitAutoData]
    public async Task Finalize_WithNullCoupon_SkipsValidation(
        Organization organization,
        User owner,
        SutProvider<OrganizationBillingService> sutProvider)
    {
        // Arrange
        var plan = MockPlans.Get(PlanType.TeamsAnnually);
        organization.PlanType = PlanType.TeamsAnnually;
        organization.GatewayCustomerId = "cus_test123";
        organization.GatewaySubscriptionId = null;

        var customerSetup = new CustomerSetup
        {
            DiscountCoupons = null
        };

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
            CustomerSetup = customerSetup,
            SubscriptionSetup = subscriptionSetup,
            Owner = owner
        };

        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(PlanType.TeamsAnnually)
            .Returns(plan);

        sutProvider.GetDependency<IHasPaymentMethodQuery>()
            .Run(organization)
            .Returns(true);

        var customer = new Customer
        {
            Id = "cus_test123",
            Tax = new CustomerTax { AutomaticTax = StripeConstants.AutomaticTaxStatus.Supported }
        };

        sutProvider.GetDependency<ISubscriberService>()
            .GetCustomerOrThrow(organization, Arg.Any<CustomerGetOptions>())
            .Returns(customer);

        sutProvider.GetDependency<IStripeAdapter>()
            .CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>())
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

        // Assert - Validation should NOT be called
        await sutProvider.GetDependency<ISubscriptionDiscountService>()
            .DidNotReceive()
            .ValidateDiscountEligibilityForUserAsync(Arg.Any<User>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<DiscountTierType>());

        // Subscription should still be created
        await sutProvider.GetDependency<IStripeAdapter>()
            .Received(1)
            .CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>());
    }

    [Theory, BitAutoData]
    public async Task Finalize_WithCouponOutsideDateRange_ThrowsBadRequestException(
        Organization organization,
        User owner,
        SutProvider<OrganizationBillingService> sutProvider)
    {
        // Arrange
        var plan = MockPlans.Get(PlanType.FamiliesAnnually);
        organization.PlanType = PlanType.FamiliesAnnually;
        organization.GatewayCustomerId = "cus_test123";
        organization.GatewaySubscriptionId = null;

        var customerSetup = new CustomerSetup
        {
            DiscountCoupons = ["EXPIRED_COUPON"]
        };

        var subscriptionSetup = new SubscriptionSetup
        {
            PlanType = PlanType.FamiliesAnnually,
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
            CustomerSetup = customerSetup,
            SubscriptionSetup = subscriptionSetup,
            Owner = owner
        };

        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(PlanType.FamiliesAnnually)
            .Returns(plan);

        // Return false to simulate expired coupon (outside valid date range)
        sutProvider.GetDependency<ISubscriptionDiscountService>()
            .ValidateDiscountEligibilityForUserAsync(
                owner,
                Arg.Is<IReadOnlyList<string>>(a => a.SequenceEqual(new[] { "EXPIRED_COUPON" })),
                DiscountTierType.Families)
            .Returns(false);

        sutProvider.GetDependency<IHasPaymentMethodQuery>()
            .Run(organization)
            .Returns(true);

        var customer = new Customer
        {
            Id = "cus_test123",
            Tax = new CustomerTax { AutomaticTax = StripeConstants.AutomaticTaxStatus.Supported }
        };

        sutProvider.GetDependency<ISubscriberService>()
            .GetCustomerOrThrow(organization, Arg.Any<CustomerGetOptions>())
            .Returns(customer);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.Finalize(sale));
        Assert.Equal("Discount expired. Please review your cart total and try again", exception.Message);

        await sutProvider.GetDependency<ISubscriptionDiscountService>()
            .Received(1)
            .ValidateDiscountEligibilityForUserAsync(
                owner,
                Arg.Is<IReadOnlyList<string>>(a => a.SequenceEqual(new[] { "EXPIRED_COUPON" })),
                DiscountTierType.Families);

        // Verify subscription was NOT created
        await sutProvider.GetDependency<IStripeAdapter>()
            .DidNotReceive()
            .CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>());
    }

    [Theory, BitAutoData]
    public async Task Finalize_WithMultipleValidCoupons_AppliesAllToSubscription(
        Organization organization,
        User owner,
        SutProvider<OrganizationBillingService> sutProvider)
    {
        // Arrange
        var plan = MockPlans.Get(PlanType.FamiliesAnnually);
        organization.PlanType = PlanType.FamiliesAnnually;
        organization.GatewayCustomerId = "cus_test123";
        organization.GatewaySubscriptionId = null;

        var customerSetup = new CustomerSetup
        {
            DiscountCoupons = ["COUPON_ONE", "COUPON_TWO"]
        };

        var subscriptionSetup = new SubscriptionSetup
        {
            PlanType = PlanType.FamiliesAnnually,
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
            CustomerSetup = customerSetup,
            SubscriptionSetup = subscriptionSetup,
            Owner = owner
        };

        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(PlanType.FamiliesAnnually)
            .Returns(plan);

        sutProvider.GetDependency<ISubscriptionDiscountService>()
            .ValidateDiscountEligibilityForUserAsync(
                owner,
                Arg.Is<IReadOnlyList<string>>(a => a.SequenceEqual(new[] { "COUPON_ONE", "COUPON_TWO" })),
                DiscountTierType.Families)
            .Returns(true);

        sutProvider.GetDependency<IHasPaymentMethodQuery>()
            .Run(organization)
            .Returns(true);

        var customer = new Customer
        {
            Id = "cus_test123",
            Tax = new CustomerTax { AutomaticTax = StripeConstants.AutomaticTaxStatus.Supported }
        };

        sutProvider.GetDependency<ISubscriberService>()
            .GetCustomerOrThrow(organization, Arg.Any<CustomerGetOptions>())
            .Returns(customer);

        sutProvider.GetDependency<IStripeAdapter>()
            .CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>())
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
        await sutProvider.GetDependency<ISubscriptionDiscountService>()
            .Received(1)
            .ValidateDiscountEligibilityForUserAsync(
                owner,
                Arg.Is<IReadOnlyList<string>>(a => a.SequenceEqual(new[] { "COUPON_ONE", "COUPON_TWO" })),
                DiscountTierType.Families);

        await sutProvider.GetDependency<IStripeAdapter>()
            .Received(1)
            .CreateSubscriptionAsync(Arg.Is<SubscriptionCreateOptions>(opts =>
                opts.Discounts != null &&
                opts.Discounts.Count == 2 &&
                opts.Discounts.Any(d => d.Coupon == "COUPON_ONE") &&
                opts.Discounts.Any(d => d.Coupon == "COUPON_TWO")));
    }

    [Theory, BitAutoData]
    public async Task Finalize_WithOneInvalidCoupon_ThrowsBadRequestException(
        Organization organization,
        User owner,
        SutProvider<OrganizationBillingService> sutProvider)
    {
        // Arrange
        var plan = MockPlans.Get(PlanType.FamiliesAnnually);
        organization.PlanType = PlanType.FamiliesAnnually;
        organization.GatewayCustomerId = "cus_test123";
        organization.GatewaySubscriptionId = null;

        var customerSetup = new CustomerSetup
        {
            DiscountCoupons = ["VALID_COUPON", "INVALID_COUPON"]
        };

        var subscriptionSetup = new SubscriptionSetup
        {
            PlanType = PlanType.FamiliesAnnually,
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
            CustomerSetup = customerSetup,
            SubscriptionSetup = subscriptionSetup,
            Owner = owner
        };

        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(PlanType.FamiliesAnnually)
            .Returns(plan);

        sutProvider.GetDependency<ISubscriptionDiscountService>()
            .ValidateDiscountEligibilityForUserAsync(
                owner,
                Arg.Is<IReadOnlyList<string>>(a => a.SequenceEqual(new[] { "VALID_COUPON", "INVALID_COUPON" })),
                DiscountTierType.Families)
            .Returns(false);

        sutProvider.GetDependency<IHasPaymentMethodQuery>()
            .Run(organization)
            .Returns(true);

        var customer = new Customer
        {
            Id = "cus_test123",
            Tax = new CustomerTax { AutomaticTax = StripeConstants.AutomaticTaxStatus.Supported }
        };

        sutProvider.GetDependency<ISubscriberService>()
            .GetCustomerOrThrow(organization, Arg.Any<CustomerGetOptions>())
            .Returns(customer);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.Finalize(sale));
        Assert.Equal("Discount expired. Please review your cart total and try again", exception.Message);

        // Verify subscription was NOT created
        await sutProvider.GetDependency<IStripeAdapter>()
            .DidNotReceive()
            .CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>());
    }

    [Theory, BitAutoData]
    public async Task Finalize_BusinessWithExemptStatus_DoesNotUpdateTaxExemption(
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

        var customer = new Customer
        {
            Id = "cus_test123",
            Tax = new CustomerTax { AutomaticTax = StripeConstants.AutomaticTaxStatus.Supported },
            Address = new Address { Country = "DE" },
            TaxExempt = StripeConstants.TaxExempt.Exempt
        };

        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(PlanType.TeamsAnnually)
            .Returns(plan);

        sutProvider.GetDependency<ISubscriberService>()
            .GetCustomerOrThrow(organization, Arg.Any<CustomerGetOptions>())
            .Returns(customer);

        sutProvider.GetDependency<IStripeAdapter>()
            .CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>())
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
            .DidNotReceive()
            .UpdateCustomerAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>());
    }

    [Theory, BitAutoData]
    public async Task Finalize_WithSMTrialSystemCoupon_AppliesSmStandaloneToSubscription(
        Organization organization,
        User owner,
        SutProvider<OrganizationBillingService> sutProvider)
    {
        organization.PlanType = PlanType.TeamsAnnually;
        organization.GatewayCustomerId = "cus_test123";
        organization.GatewaySubscriptionId = null;

        var customerSetup = new CustomerSetup
        {
            SystemCoupons = [StripeConstants.CouponIDs.SecretsManagerStandalone],
            DiscountCoupons = null
        };

        var subscriptionSetup = new SubscriptionSetup
        {
            PlanType = PlanType.TeamsAnnually,
            PasswordManagerOptions = new SubscriptionSetup.PasswordManager { Seats = 5 },
            SecretsManagerOptions = null,
            SkipTrial = false
        };

        var sale = new OrganizationSale
        {
            Organization = organization,
            CustomerSetup = customerSetup,
            SubscriptionSetup = subscriptionSetup,
            Owner = owner
        };

        var plan = MockPlans.Get(PlanType.TeamsAnnually);
        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(PlanType.TeamsAnnually)
            .Returns(plan);

        sutProvider.GetDependency<IHasPaymentMethodQuery>()
            .Run(organization)
            .Returns(true);

        var customer = new Customer
        {
            Id = "cus_test123",
            Tax = new CustomerTax { AutomaticTax = StripeConstants.AutomaticTaxStatus.Supported }
        };

        sutProvider.GetDependency<ISubscriberService>()
            .GetCustomerOrThrow(organization, Arg.Any<CustomerGetOptions>())
            .Returns(customer);

        sutProvider.GetDependency<IStripeAdapter>()
            .CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>())
            .Returns(new Subscription
            {
                Id = "sub_test123",
                Status = StripeConstants.SubscriptionStatus.Active
            });

        await sutProvider.Sut.Finalize(sale);

        await sutProvider.GetDependency<ISubscriptionDiscountService>()
            .DidNotReceive()
            .ValidateDiscountEligibilityForUserAsync(
                Arg.Any<User>(),
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<DiscountTierType>());

        await sutProvider.GetDependency<IStripeAdapter>()
            .Received(1)
            .CreateSubscriptionAsync(Arg.Is<SubscriptionCreateOptions>(opts =>
                opts.Discounts != null &&
                opts.Discounts.Count == 1 &&
                opts.Discounts[0].Coupon == StripeConstants.CouponIDs.SecretsManagerStandalone));
    }

    [Theory, BitAutoData]
    public async Task Finalize_WithBothSystemCouponsAndDiscountCoupons_ThrowsBadRequestException(
        Organization organization,
        User owner,
        SutProvider<OrganizationBillingService> sutProvider)
    {
        organization.PlanType = PlanType.FamiliesAnnually;
        organization.GatewayCustomerId = null;
        organization.GatewaySubscriptionId = null;

        var customerSetup = new CustomerSetup
        {
            SystemCoupons = [StripeConstants.CouponIDs.SecretsManagerStandalone],
            DiscountCoupons = ["DISCOUNT_COUPON"]
        };

        var subscriptionSetup = new SubscriptionSetup
        {
            PlanType = PlanType.FamiliesAnnually,
            PasswordManagerOptions = new SubscriptionSetup.PasswordManager { Seats = 5 },
            SecretsManagerOptions = null,
            SkipTrial = false
        };

        var sale = new OrganizationSale
        {
            Organization = organization,
            CustomerSetup = customerSetup,
            SubscriptionSetup = subscriptionSetup,
            Owner = owner
        };

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.Finalize(sale));

        await sutProvider.GetDependency<IStripeAdapter>()
            .DidNotReceive()
            .CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>());
    }

    [Theory, BitAutoData]
    public async Task Finalize_WithDiscountCouponsOnFamiliesPlan_ValidatesAndApplies(
        Organization organization,
        User owner,
        SutProvider<OrganizationBillingService> sutProvider)
    {
        organization.PlanType = PlanType.FamiliesAnnually;
        organization.GatewayCustomerId = "cus_test123";
        organization.GatewaySubscriptionId = null;

        var customerSetup = new CustomerSetup
        {
            SystemCoupons = null,
            DiscountCoupons = ["DISCOUNT_COUPON"]
        };

        var subscriptionSetup = new SubscriptionSetup
        {
            PlanType = PlanType.FamiliesAnnually,
            PasswordManagerOptions = new SubscriptionSetup.PasswordManager { Seats = 5 },
            SecretsManagerOptions = null,
            SkipTrial = false
        };

        var sale = new OrganizationSale
        {
            Organization = organization,
            CustomerSetup = customerSetup,
            SubscriptionSetup = subscriptionSetup,
            Owner = owner
        };

        var plan = MockPlans.Get(PlanType.FamiliesAnnually);
        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(PlanType.FamiliesAnnually)
            .Returns(plan);

        sutProvider.GetDependency<ISubscriptionDiscountService>()
            .ValidateDiscountEligibilityForUserAsync(
                owner,
                Arg.Is<IReadOnlyList<string>>(a => a.SequenceEqual(new[] { "DISCOUNT_COUPON" })),
                DiscountTierType.Families)
            .Returns(true);

        sutProvider.GetDependency<IHasPaymentMethodQuery>()
            .Run(organization)
            .Returns(true);

        var customer = new Customer
        {
            Id = "cus_test123",
            Tax = new CustomerTax { AutomaticTax = StripeConstants.AutomaticTaxStatus.Supported }
        };

        sutProvider.GetDependency<ISubscriberService>()
            .GetCustomerOrThrow(organization, Arg.Any<CustomerGetOptions>())
            .Returns(customer);

        sutProvider.GetDependency<IStripeAdapter>()
            .CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>())
            .Returns(new Subscription
            {
                Id = "sub_test123",
                Status = StripeConstants.SubscriptionStatus.Active
            });

        await sutProvider.Sut.Finalize(sale);

        await sutProvider.GetDependency<ISubscriptionDiscountService>()
            .Received(1)
            .ValidateDiscountEligibilityForUserAsync(
                owner,
                Arg.Is<IReadOnlyList<string>>(a => a.SequenceEqual(new[] { "DISCOUNT_COUPON" })),
                DiscountTierType.Families);

        await sutProvider.GetDependency<IStripeAdapter>()
            .Received(1)
            .CreateSubscriptionAsync(Arg.Is<SubscriptionCreateOptions>(opts =>
                opts.Discounts != null &&
                opts.Discounts.Count == 1 &&
                opts.Discounts[0].Coupon == "DISCOUNT_COUPON"));
    }

    [Theory, BitAutoData]
    public async Task Finalize_WithDiscountCouponsOnNonFamiliesPlan_CouponsAreIgnored(
        Organization organization,
        User owner,
        SutProvider<OrganizationBillingService> sutProvider)
    {
        organization.PlanType = PlanType.TeamsAnnually;
        organization.GatewayCustomerId = "cus_test123";
        organization.GatewaySubscriptionId = null;

        var customerSetup = new CustomerSetup
        {
            SystemCoupons = null,
            DiscountCoupons = ["DISCOUNT_COUPON"]
        };

        var subscriptionSetup = new SubscriptionSetup
        {
            PlanType = PlanType.TeamsAnnually,
            PasswordManagerOptions = new SubscriptionSetup.PasswordManager { Seats = 5 },
            SecretsManagerOptions = null,
            SkipTrial = false
        };

        var sale = new OrganizationSale
        {
            Organization = organization,
            CustomerSetup = customerSetup,
            SubscriptionSetup = subscriptionSetup,
            Owner = owner
        };

        var plan = MockPlans.Get(PlanType.TeamsAnnually);
        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(PlanType.TeamsAnnually)
            .Returns(plan);

        sutProvider.GetDependency<IHasPaymentMethodQuery>()
            .Run(organization)
            .Returns(true);

        var customer = new Customer
        {
            Id = "cus_test123",
            Tax = new CustomerTax { AutomaticTax = StripeConstants.AutomaticTaxStatus.Supported }
        };

        sutProvider.GetDependency<ISubscriberService>()
            .GetCustomerOrThrow(organization, Arg.Any<CustomerGetOptions>())
            .Returns(customer);

        sutProvider.GetDependency<IStripeAdapter>()
            .CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>())
            .Returns(new Subscription
            {
                Id = "sub_test123",
                Status = StripeConstants.SubscriptionStatus.Active
            });

        await sutProvider.Sut.Finalize(sale);

        await sutProvider.GetDependency<ISubscriptionDiscountService>()
            .DidNotReceive()
            .ValidateDiscountEligibilityForUserAsync(
                Arg.Any<User>(),
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<DiscountTierType>());

        await sutProvider.GetDependency<IStripeAdapter>()
            .Received(1)
            .CreateSubscriptionAsync(Arg.Is<SubscriptionCreateOptions>(opts =>
                opts.Discounts == null || opts.Discounts.Count == 0));
    }

    #endregion

    [Theory, BitAutoData]
    public async Task Finalize_SwissBusinessWithReverse_CorrectsTaxExemptToNone(
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

        var customer = new Customer
        {
            Id = "cus_test123",
            Tax = new CustomerTax { AutomaticTax = StripeConstants.AutomaticTaxStatus.Supported },
            Address = new Address { Country = "CH" },
            TaxExempt = StripeConstants.TaxExempt.Reverse
        };

        var correctedCustomer = new Customer
        {
            Id = "cus_test123",
            Tax = new CustomerTax { AutomaticTax = StripeConstants.AutomaticTaxStatus.Supported },
            Address = new Address { Country = "CH" },
            TaxExempt = StripeConstants.TaxExempt.None
        };

        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(PlanType.TeamsAnnually)
            .Returns(plan);

        sutProvider.GetDependency<ISubscriberService>()
            .GetCustomerOrThrow(organization, Arg.Any<CustomerGetOptions>())
            .Returns(customer);

        sutProvider.GetDependency<IStripeAdapter>()
            .UpdateCustomerAsync(customer.Id, Arg.Is<CustomerUpdateOptions>(options =>
                options.TaxExempt == StripeConstants.TaxExempt.None))
            .Returns(correctedCustomer);

        sutProvider.GetDependency<IStripeAdapter>()
            .CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>())
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
            .UpdateCustomerAsync("cus_test123",
                Arg.Is<CustomerUpdateOptions>(options =>
                    options.TaxExempt == StripeConstants.TaxExempt.None));
    }

    [Theory, BitAutoData]
    public async Task Finalize_USBusinessWithReverseExempt_CorrectsTaxExemptToNone(
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

        var customer = new Customer
        {
            Id = "cus_test123",
            Tax = new CustomerTax { AutomaticTax = StripeConstants.AutomaticTaxStatus.Supported },
            Address = new Address { Country = "US" },
            TaxExempt = StripeConstants.TaxExempt.Reverse
        };

        var correctedCustomer = new Customer
        {
            Id = "cus_test123",
            Tax = new CustomerTax { AutomaticTax = StripeConstants.AutomaticTaxStatus.Supported },
            Address = new Address { Country = "US" },
            TaxExempt = StripeConstants.TaxExempt.None
        };

        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(PlanType.TeamsAnnually)
            .Returns(plan);

        sutProvider.GetDependency<ISubscriberService>()
            .GetCustomerOrThrow(organization, Arg.Any<CustomerGetOptions>())
            .Returns(customer);

        sutProvider.GetDependency<IStripeAdapter>()
            .UpdateCustomerAsync(customer.Id, Arg.Is<CustomerUpdateOptions>(options =>
                options.TaxExempt == StripeConstants.TaxExempt.None))
            .Returns(correctedCustomer);

        sutProvider.GetDependency<IStripeAdapter>()
            .CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>())
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

        // Assert: UpdateCustomerAsync called with TaxExempt = "none" to correct the erroneous "reverse"
        await sutProvider.GetDependency<IStripeAdapter>()
            .Received(1)
            .UpdateCustomerAsync(customer.Id, Arg.Is<CustomerUpdateOptions>(options =>
                options.TaxExempt == StripeConstants.TaxExempt.None));
    }

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
    public async Task UpdateOrganizationNameAndEmail_WhenNameIsLong_UsesFullName(
        Organization organization,
        SutProvider<OrganizationBillingService> sutProvider)
    {
        // Arrange
        var longName = "This is a very long organization name that exceeds thirty characters";
        organization.Name = longName;

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
        Assert.Equal(longName, customField.Value);
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationNameAndEmail_WhenGatewayCustomerIdIsNull_LogsWarningAndReturns(
        Organization organization,
        SutProvider<OrganizationBillingService> sutProvider)
    {
        // Arrange
        organization.GatewayCustomerId = null;
        organization.Name = "Test Organization";
        organization.BillingEmail = "billing@example.com";
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        // Act
        await sutProvider.Sut.UpdateOrganizationNameAndEmail(organization);

        // Assert
        await stripeAdapter.DidNotReceive().UpdateCustomerAsync(
            Arg.Any<string>(),
            Arg.Any<CustomerUpdateOptions>());
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationNameAndEmail_WhenGatewayCustomerIdIsEmpty_LogsWarningAndReturns(
        Organization organization,
        SutProvider<OrganizationBillingService> sutProvider)
    {
        // Arrange
        organization.GatewayCustomerId = "";
        organization.Name = "Test Organization";
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        // Act
        await sutProvider.Sut.UpdateOrganizationNameAndEmail(organization);

        // Assert
        await stripeAdapter.DidNotReceive().UpdateCustomerAsync(
            Arg.Any<string>(),
            Arg.Any<CustomerUpdateOptions>());
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationNameAndEmail_WhenNameIsNull_LogsWarningAndReturns(
        Organization organization,
        SutProvider<OrganizationBillingService> sutProvider)
    {
        // Arrange
        organization.Name = null;
        organization.GatewayCustomerId = "cus_test123";
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        // Act
        await sutProvider.Sut.UpdateOrganizationNameAndEmail(organization);

        // Assert
        await stripeAdapter.DidNotReceive().UpdateCustomerAsync(
            Arg.Any<string>(),
            Arg.Any<CustomerUpdateOptions>());
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationNameAndEmail_WhenNameIsEmpty_LogsWarningAndReturns(
        Organization organization,
        SutProvider<OrganizationBillingService> sutProvider)
    {
        // Arrange
        organization.Name = "";
        organization.GatewayCustomerId = "cus_test123";
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        // Act
        await sutProvider.Sut.UpdateOrganizationNameAndEmail(organization);

        // Assert
        await stripeAdapter.DidNotReceive().UpdateCustomerAsync(
            Arg.Any<string>(),
            Arg.Any<CustomerUpdateOptions>());
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationNameAndEmail_WhenBillingEmailIsNull_UpdatesWithNull(
        Organization organization,
        SutProvider<OrganizationBillingService> sutProvider)
    {
        // Arrange
        organization.Name = "Test Organization";
        organization.BillingEmail = null;
        organization.GatewayCustomerId = "cus_test123";
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        // Act
        await sutProvider.Sut.UpdateOrganizationNameAndEmail(organization);

        // Assert
        await stripeAdapter.Received(1).UpdateCustomerAsync(
            organization.GatewayCustomerId,
            Arg.Is<CustomerUpdateOptions>(options =>
                options.Email == null &&
                options.Description == organization.Name));
    }
}
