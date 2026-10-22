using Bit.Core.Billing;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Models.Sales;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Tax.Models;
using Bit.Core.Billing.Tax.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Subscriptions.Organization.Commands;
using Braintree;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Stripe;
using Xunit;
using Customer = Stripe.Customer;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;
using PlanFeatures = Bit.Core.Models.StaticStore.Plan;
using Subscription = Stripe.Subscription;

namespace Bit.Subscriptions.Organization.Test.Commands;

public class CreateOrganizationSubscriptionCommandTests
{
    private readonly IBraintreeGateway _braintreeGateway = Substitute.For<IBraintreeGateway>();
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly ISubscriberService _subscriberService = Substitute.For<ISubscriberService>();
    private readonly ISubscriptionDiscountService _subscriptionDiscountService = Substitute.For<ISubscriptionDiscountService>();
    private readonly ITaxService _taxService = Substitute.For<ITaxService>();
    private SubscriptionCreateOptions? _capturedSubscriptionOptions;
    private readonly CreateOrganizationSubscriptionCommand _command;

    public CreateOrganizationSubscriptionCommandTests() =>
        _command = new CreateOrganizationSubscriptionCommand(
            _braintreeGateway,
            NullLogger<CreateOrganizationSubscriptionCommand>.Instance,
            _pricingClient,
            _stripeAdapter,
            _subscriberService,
            _subscriptionDiscountService,
            _taxService);

    [Fact]
    public async Task Run_AppliesResultOntoOrganization()
    {
        StubSuccessfulPurchase();

        var organization = Organization();

        var subscription = await _command.Run(organization, Creation(privilegedControlsRequested: true, requestedSeatMinimum: 6));

        Assert.Equal("sub_test", subscription.Id);
        Assert.Equal(GatewayType.Stripe, organization.Gateway);
        Assert.Equal("cus_test", organization.GatewayCustomerId);
        Assert.Equal("sub_test", organization.GatewaySubscriptionId);
        Assert.True(organization.Enabled);
        Assert.True(organization.UsePam);
    }

    [Theory]
    [InlineData(6, 6)]      // approved promotional minimum is honored
    [InlineData(7, 10)]     // unapproved minimum falls back to the default
    [InlineData(null, 10)]  // no minimum falls back to the default
    public async Task Run_ResolvesPrivilegedControlsSeatMinimum(int? requested, int expected)
    {
        StubSuccessfulPurchase();

        var organization = Organization();

        await _command.Run(organization, Creation(privilegedControlsRequested: true, requestedSeatMinimum: requested));

        Assert.Equal(expected, organization.PamSeats);
        Assert.Equal(expected, organization.PamSeatMinimum);
        Assert.NotNull(_capturedSubscriptionOptions);
        Assert.Contains(_capturedSubscriptionOptions!.Items, i => i.Price == "pam-seat" && i.Quantity == expected);
    }

    [Fact]
    public async Task Run_PlanWithoutPrivilegedControlsSupport_ThrowsBadRequest()
    {
        _pricingClient.GetPlanOrThrow(PlanType.TeamsAnnually).Returns(new TestPlan(supportsPrivilegedControls: false));

        var organization = Organization();

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => _command.Run(organization, Creation(planType: PlanType.TeamsAnnually, privilegedControlsRequested: true, requestedSeatMinimum: 6)));
        Assert.Contains("Invalid Privileged Controls plan selected", exception.Message);
        await _stripeAdapter.DidNotReceiveWithAnyArgs().CreateCustomerAsync(default!);
        await _stripeAdapter.DidNotReceiveWithAnyArgs().CreateSubscriptionAsync(default!);
    }

    [Fact]
    public async Task Run_PrivilegedControlsWithSecretsManagerTrial_ThrowsBadRequest()
    {
        StubSuccessfulPurchase();

        var creation = Creation(privilegedControlsRequested: true, requestedSeatMinimum: 6, secretManagerRequested: true) with
        {
            IsFromSecretsManagerTrial = true
        };

        await Assert.ThrowsAsync<BadRequestException>(() => _command.Run(Organization(), creation));
        await _stripeAdapter.DidNotReceiveWithAnyArgs().CreateCustomerAsync(default!);
        await _stripeAdapter.DidNotReceiveWithAnyArgs().CreateSubscriptionAsync(default!);
    }

    [Fact]
    public async Task Run_PrivilegedControlsWithSecretsManager_BuildsBothLineItems()
    {
        StubSuccessfulPurchase();

        var organization = Organization();

        // SM + PC together is allowed on a net-new purchase; only the trial-initiation flow (upstream) forbids it.
        await _command.Run(organization,
            Creation(privilegedControlsRequested: true, requestedSeatMinimum: 6, secretManagerRequested: true));

        Assert.NotNull(_capturedSubscriptionOptions);
        Assert.Contains(_capturedSubscriptionOptions!.Items, i => i.Price == "sm-seat");
        Assert.Contains(_capturedSubscriptionOptions!.Items, i => i.Price == "pam-seat" && i.Quantity == 6);
    }

    [Fact]
    public async Task Run_BothCouponKinds_ThrowsBadRequest()
    {
        StubSuccessfulPurchase();

        var organization = Organization();
        var creation = Creation() with
        {
            CustomerSetup = new CustomerSetup { SystemCoupons = ["system"], DiscountCoupons = ["discount"] }
        };

        await Assert.ThrowsAsync<BadRequestException>(() => _command.Run(organization, creation));
        await _stripeAdapter.DidNotReceiveWithAnyArgs().CreateCustomerAsync(default!);
    }

    [Fact]
    public async Task Run_SystemCoupons_PassedToSubscriptionCreation_WithoutDiscountValidation()
    {
        StubSuccessfulPurchase(PlanType.FamiliesAnnually);

        var organization = Organization();
        var creation = Creation(PlanType.FamiliesAnnually) with
        {
            CustomerSetup = new CustomerSetup { SystemCoupons = ["system-coupon"] },
            Owner = new User()
        };

        await _command.Run(organization, creation);

        Assert.NotNull(_capturedSubscriptionOptions);
        Assert.Contains(_capturedSubscriptionOptions!.Discounts, d => d.Coupon == "system-coupon");
        await _subscriptionDiscountService.DidNotReceiveWithAnyArgs().ValidateDiscountEligibilityForUserAsync(default!, default!, default);
    }

    [Fact]
    public async Task Run_FamiliesDiscountCoupons_Eligible_PassedToSubscriptionCreation()
    {
        StubSuccessfulPurchase(PlanType.FamiliesAnnually);
        _subscriptionDiscountService
            .ValidateDiscountEligibilityForUserAsync(Arg.Any<User>(), Arg.Any<List<string>>(), DiscountTierType.Families)
            .Returns(true);

        var organization = Organization();
        var creation = Creation(PlanType.FamiliesAnnually) with
        {
            CustomerSetup = new CustomerSetup { DiscountCoupons = ["families-discount"] },
            Owner = new User()
        };

        await _command.Run(organization, creation);

        Assert.NotNull(_capturedSubscriptionOptions);
        Assert.Contains(_capturedSubscriptionOptions!.Discounts, d => d.Coupon == "families-discount");
    }

    [Fact]
    public async Task Run_ExpiredFamiliesDiscount_ThrowsBadRequest()
    {
        StubSuccessfulPurchase(PlanType.FamiliesAnnually);
        _subscriptionDiscountService
            .ValidateDiscountEligibilityForUserAsync(Arg.Any<User>(), Arg.Any<List<string>>(), DiscountTierType.Families)
            .Returns(false);

        var organization = Organization();
        var creation = Creation(PlanType.FamiliesAnnually) with
        {
            CustomerSetup = new CustomerSetup { DiscountCoupons = ["families-discount"] },
            Owner = new User()
        };

        await Assert.ThrowsAsync<BadRequestException>(() => _command.Run(organization, creation));
        await _stripeAdapter.DidNotReceiveWithAnyArgs().CreateCustomerAsync(default!);
        await _stripeAdapter.DidNotReceiveWithAnyArgs().CreateSubscriptionAsync(default!);
    }

    [Fact]
    public async Task Run_DiscountCouponsOnNonFamiliesPlan_Ignored()
    {
        StubSuccessfulPurchase(PlanType.EnterpriseAnnually);

        var organization = Organization();
        var creation = Creation(PlanType.EnterpriseAnnually) with
        {
            CustomerSetup = new CustomerSetup { DiscountCoupons = ["discount"] },
            Owner = new User()
        };

        await _command.Run(organization, creation);

        Assert.NotNull(_capturedSubscriptionOptions);
        Assert.Null(_capturedSubscriptionOptions!.Discounts);
        await _subscriptionDiscountService.DidNotReceiveWithAnyArgs().ValidateDiscountEligibilityForUserAsync(default!, default!, default);
    }

    [Fact]
    public async Task Run_BuildsPasswordManagerSeatLineItem()
    {
        StubSuccessfulPurchase();

        var organization = Organization();

        await _command.Run(organization, Creation());

        Assert.NotNull(_capturedSubscriptionOptions);
        Assert.Contains(_capturedSubscriptionOptions!.Items, i => i.Price == "pm-seat" && i.Quantity == 8);
    }

    [Fact]
    public async Task Run_PremiumAccessAndStorage_AddLineItems()
    {
        StubSuccessfulPurchase();

        var organization = Organization();

        await _command.Run(organization, Creation(premiumAccess: true, storage: 3));

        Assert.NotNull(_capturedSubscriptionOptions);
        Assert.Contains(_capturedSubscriptionOptions!.Items, i => i.Price == "pm-premium" && i.Quantity == 1);
        Assert.Contains(_capturedSubscriptionOptions!.Items, i => i.Price == "pm-storage" && i.Quantity == 3);
    }

    [Fact]
    public async Task Run_SecretsManager_AddsSeatAndServiceAccountLineItems()
    {
        StubSuccessfulPurchase();

        var organization = Organization();

        await _command.Run(organization, Creation(secretManagerRequested: true));

        Assert.NotNull(_capturedSubscriptionOptions);
        Assert.Contains(_capturedSubscriptionOptions!.Items, i => i.Price == "sm-seat" && i.Quantity == 4);
        Assert.Contains(_capturedSubscriptionOptions!.Items, i => i.Price == "sm-sa" && i.Quantity == 5);
    }

    [Fact]
    public async Task Run_NoPrivilegedControls_OmitsPamLineItem()
    {
        StubSuccessfulPurchase();

        var organization = Organization();

        await _command.Run(organization, Creation());

        Assert.NotNull(_capturedSubscriptionOptions);
        Assert.DoesNotContain(_capturedSubscriptionOptions!.Items, i => i.Price == "pam-seat");
    }

    [Theory]
    [InlineData(14)]
    [InlineData(30)]
    public async Task Run_TrialLength_OverridesPlanDefaultTrialPeriodDays(int trialDays)
    {
        StubSuccessfulPurchase();

        var organization = Organization();

        await _command.Run(organization, Creation(trialDays: trialDays));

        Assert.Equal(trialDays, _capturedSubscriptionOptions!.TrialPeriodDays);
    }

    [Fact]
    public async Task Run_NullTrialLength_UsesPlanDefaultTrialPeriodDays()
    {
        StubSuccessfulPurchase();

        var organization = Organization();

        await _command.Run(organization, Creation(privilegedControlsRequested: true, requestedSeatMinimum: 6));

        Assert.Equal(7, _capturedSubscriptionOptions!.TrialPeriodDays);
    }

    [Fact]
    public async Task Run_ZeroTrialLength_SkipsTrial()
    {
        StubSuccessfulPurchase();

        var organization = Organization();

        await _command.Run(organization,
            Creation(privilegedControlsRequested: true, requestedSeatMinimum: 6, trialDays: 0));

        Assert.Equal(0, _capturedSubscriptionOptions!.TrialPeriodDays);
        // No trial means no missing-payment-method cancel behavior, even without a payment method.
        Assert.Null(_capturedSubscriptionOptions!.TrialSettings);
    }

    [Fact]
    public async Task Run_NoPaymentMethodAndTrial_SetsMissingPaymentMethodCancel()
    {
        StubSuccessfulPurchase();

        var organization = Organization();

        // Default Creation uses a non-billable CustomerSetup (no payment method).
        await _command.Run(organization, Creation(trialDays: 7));

        Assert.NotNull(_capturedSubscriptionOptions!.TrialSettings);
        Assert.Equal("cancel", _capturedSubscriptionOptions!.TrialSettings!.EndBehavior!.MissingPaymentMethod);
    }

    [Fact]
    public async Task Run_WithPaymentMethodAndTrial_DoesNotSetMissingPaymentMethodCancel()
    {
        StubSuccessfulPurchase();

        var creation = Creation(trialDays: 7) with
        {
            CustomerSetup = new CustomerSetup
            {
                TokenizedPaymentSource = new TokenizedPaymentSource(PaymentMethodType.Card, "card-token"),
                TaxInformation = Tax()
            }
        };

        await _command.Run(Organization(), creation);

        Assert.NotNull(_capturedSubscriptionOptions);
        Assert.Null(_capturedSubscriptionOptions!.TrialSettings);
    }

    [Fact]
    public async Task Run_BillableSetupWithInvalidPaymentSource_ThrowsBadRequest()
    {
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(new TestPlan());

        var creation = Creation() with
        {
            CustomerSetup = new CustomerSetup
            {
                TokenizedPaymentSource = new TokenizedPaymentSource(PaymentMethodType.Card, ""),
                TaxInformation = Tax()
            }
        };

        await Assert.ThrowsAsync<BadRequestException>(() => _command.Run(Organization(), creation));
        await _stripeAdapter.DidNotReceiveWithAnyArgs().CreateCustomerAsync(default!);
        await _stripeAdapter.DidNotReceiveWithAnyArgs().CreateSubscriptionAsync(default!);
    }

    [Fact]
    public async Task Run_BillableSetupWithMissingTaxInformation_ThrowsBadRequest()
    {
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(new TestPlan());

        var creation = Creation() with
        {
            CustomerSetup = new CustomerSetup
            {
                TokenizedPaymentSource = new TokenizedPaymentSource(PaymentMethodType.Card, "card-token"),
                TaxInformation = Tax(country: "", postalCode: "")
            }
        };

        await Assert.ThrowsAsync<BadRequestException>(() => _command.Run(Organization(), creation));
        await _stripeAdapter.DidNotReceiveWithAnyArgs().CreateCustomerAsync(default!);
    }

    [Fact]
    public async Task Run_TaxIdTypeCannotBeResolved_ThrowsBadRequest()
    {
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(new TestPlan());
        _taxService.GetStripeTaxCode(Arg.Any<string>(), Arg.Any<string>()).Returns((string)null!);

        var creation = Creation() with
        {
            CustomerSetup = new CustomerSetup
            {
                TokenizedPaymentSource = new TokenizedPaymentSource(PaymentMethodType.Card, "card-token"),
                TaxInformation = Tax(taxId: "VAT123")
            }
        };

        await Assert.ThrowsAsync<BadRequestException>(() => _command.Run(Organization(), creation));
        await _stripeAdapter.DidNotReceiveWithAnyArgs().CreateCustomerAsync(default!);
    }

    [Fact]
    public async Task Run_BankAccountWithoutSetupIntent_ThrowsBadRequest()
    {
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(new TestPlan());
        _stripeAdapter.ListSetupIntentsAsync(Arg.Any<SetupIntentListOptions>()).Returns(new List<SetupIntent>());

        var creation = Creation() with
        {
            CustomerSetup = new CustomerSetup
            {
                TokenizedPaymentSource = new TokenizedPaymentSource(PaymentMethodType.BankAccount, "bank-token"),
                TaxInformation = Tax()
            }
        };

        await Assert.ThrowsAsync<BadRequestException>(() => _command.Run(Organization(), creation));
        await _stripeAdapter.DidNotReceiveWithAnyArgs().CreateCustomerAsync(default!);
    }

    [Theory]
    [InlineData(StripeConstants.ErrorCodes.CustomerTaxLocationInvalid)]
    [InlineData(StripeConstants.ErrorCodes.TaxIdInvalid)]
    public async Task Run_StripeRejectsCustomer_ThrowsBadRequest(string errorCode)
    {
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(new TestPlan());
        _stripeAdapter.CreateCustomerAsync(Arg.Any<CustomerCreateOptions>())
            .ThrowsAsync(new StripeException("rejected") { StripeError = new StripeError { Code = errorCode } });

        var creation = Creation() with
        {
            CustomerSetup = new CustomerSetup
            {
                TokenizedPaymentSource = new TokenizedPaymentSource(PaymentMethodType.Card, "card-token"),
                TaxInformation = Tax()
            }
        };

        await Assert.ThrowsAsync<BadRequestException>(() => _command.Run(Organization(), creation));
        await _stripeAdapter.DidNotReceiveWithAnyArgs().CreateSubscriptionAsync(default!);
    }

    [Fact]
    public async Task Run_CustomerCreationFails_RevertsBraintreeCustomer()
    {
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(new TestPlan());
        _subscriberService.CreateBraintreeCustomer(Arg.Any<ISubscriber>(), "paypal-token").Returns("braintree_test");
        _stripeAdapter.CreateCustomerAsync(Arg.Any<CustomerCreateOptions>())
            .ThrowsAsync(new InvalidOperationException("boom"));

        var creation = Creation() with
        {
            CustomerSetup = new CustomerSetup
            {
                TokenizedPaymentSource = new TokenizedPaymentSource(PaymentMethodType.PayPal, "paypal-token"),
                TaxInformation = Tax()
            }
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => _command.Run(Organization(), creation));
        await _braintreeGateway.Customer.Received(1).DeleteAsync("braintree_test");
    }

    [Fact]
    public async Task Run_SubscriptionCreationFails_RevertsCustomer()
    {
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(new TestPlan());
        _subscriberService.CreateBraintreeCustomer(Arg.Any<ISubscriber>(), "paypal-token").Returns("braintree_test");
        _stripeAdapter.CreateCustomerAsync(Arg.Any<CustomerCreateOptions>()).Returns(new Customer
        {
            Id = "cus_test",
            Metadata = new Dictionary<string, string> { [Utilities.BraintreeCustomerIdKey] = "braintree_test" }
        });
        _stripeAdapter.CreateSubscriptionAsync(Arg.Any<SubscriptionCreateOptions>())
            .ThrowsAsync(new InvalidOperationException("subscription creation failed"));

        var creation = Creation() with
        {
            CustomerSetup = new CustomerSetup
            {
                TokenizedPaymentSource = new TokenizedPaymentSource(PaymentMethodType.PayPal, "paypal-token"),
                TaxInformation = Tax()
            }
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => _command.Run(Organization(), creation));

        // A subscription failure must not orphan the customer created for it.
        await _stripeAdapter.Received(1).DeleteCustomerAsync("cus_test");
        await _braintreeGateway.Customer.Received(1).DeleteAsync("braintree_test");
    }

    private static OrganizationEntity Organization() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Test",
        BillingEmail = "test@example.com"
    };

    private static TaxInformation Tax(string country = "US", string postalCode = "12345", string taxId = "") =>
        new(country, postalCode, taxId, "", "", "", "", "");

    private void StubSuccessfulPurchase(PlanType planType = PlanType.EnterpriseAnnually)
    {
        _pricingClient.GetPlanOrThrow(planType).Returns(new TestPlan());
        _stripeAdapter.CreateCustomerAsync(Arg.Any<CustomerCreateOptions>()).Returns(new Customer { Id = "cus_test" });
        _stripeAdapter.CreateSubscriptionAsync(Arg.Do<SubscriptionCreateOptions>(o => _capturedSubscriptionOptions = o))
            .Returns(new Subscription { Id = "sub_test", Status = StripeConstants.SubscriptionStatus.Trialing });
    }

    private static OrganizationSubscriptionCreation Creation(
        PlanType? planType = null,
        bool privilegedControlsRequested = false,
        int? requestedSeatMinimum = null,
        bool secretManagerRequested = false,
        short? storage = null,
        bool premiumAccess = false,
        int? trialDays = null) => new()
        {
            PlanType = planType ?? PlanType.EnterpriseAnnually,
            PasswordManager = new OrganizationSubscriptionCreation.PasswordManagerPurchase
            {
                Seats = 8,
                Storage = storage,
                PremiumAccess = premiumAccess
            },
            SecretsManager = secretManagerRequested
            ? new OrganizationSubscriptionCreation.SecretsManagerPurchase { Seats = 4, ServiceAccounts = 5 }
            : null,
            PrivilegedControls = privilegedControlsRequested
            ? new OrganizationSubscriptionCreation.PrivilegedControlsPurchase { RequestedSeatMinimum = requestedSeatMinimum }
            : null,
            CustomerSetup = new CustomerSetup(),
            CloudRegion = "US",
            Trial = trialDays is { } days
            ? new OrganizationSubscriptionCreation.TrialSetup { Length = days }
            : new OrganizationSubscriptionCreation.TrialSetup()
        };

    private sealed record TestPlan : PlanFeatures
    {
        public TestPlan(bool supportsPrivilegedControls = true)
        {
            TrialPeriodDays = 7;
            PasswordManager = new PasswordManagerPlanFeatures
            {
                StripeSeatPlanId = "pm-seat",
                StripePremiumAccessPlanId = "pm-premium",
                StripeStoragePlanId = "pm-storage"
            };
            SecretsManager = new SecretsManagerPlanFeatures
            {
                StripeSeatPlanId = "sm-seat",
                StripeServiceAccountPlanId = "sm-sa"
            };
            if (supportsPrivilegedControls)
            {
                PrivilegedControls = new PrivilegedControlsPlanFeatures
                {
                    StripeSeatPlanId = "pam-seat",
                    DefaultSeatMinimum = 10,
                    PromotionalSeatMinimums = [4, 6, 8]
                };
            }
        }
    }
}
