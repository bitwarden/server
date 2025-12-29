using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Caches;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Payment.Queries;
using Bit.Core.Billing.Services;
using Bit.Core.Test.Billing.Extensions;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Payment.Queries;

using static StripeConstants;

public class HasPaymentMethodQueryTests
{
    private readonly ISetupIntentCache _setupIntentCache = Substitute.For<ISetupIntentCache>();
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly ISubscriberService _subscriberService = Substitute.For<ISubscriberService>();
    private readonly HasPaymentMethodQuery _query;

    public HasPaymentMethodQueryTests()
    {
        _query = new HasPaymentMethodQuery(
            _setupIntentCache,
            _stripeAdapter,
            _subscriberService);
    }

    [Fact]
    public async Task Run_NoCustomer_ReturnsFalse()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid()
        };

        _subscriberService.GetCustomer(organization).ReturnsNull();
        _setupIntentCache.GetSetupIntentIdForSubscriber(organization.Id).Returns((string)null);

        var hasPaymentMethod = await _query.Run(organization);

        Assert.False(hasPaymentMethod);
    }

    [Fact]
    public async Task Run_NoCustomer_WithUnverifiedBankAccount_ReturnsTrue()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid()
        };

        _subscriberService.GetCustomer(organization).ReturnsNull();
        _setupIntentCache.GetSetupIntentIdForSubscriber(organization.Id).Returns("seti_123");

        _stripeAdapter
            .GetSetupIntentAsync("seti_123",
                Arg.Is<SetupIntentGetOptions>(options => options.HasExpansions("payment_method")))
            .Returns(new SetupIntent
            {
                Status = "requires_action",
                NextAction = new SetupIntentNextAction
                {
                    VerifyWithMicrodeposits = new SetupIntentNextActionVerifyWithMicrodeposits()
                },
                PaymentMethod = new PaymentMethod
                {
                    UsBankAccount = new PaymentMethodUsBankAccount()
                }
            });

        var hasPaymentMethod = await _query.Run(organization);

        Assert.True(hasPaymentMethod);
    }

    [Fact]
    public async Task Run_NoPaymentMethod_ReturnsFalse()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid()
        };

        var customer = new Customer
        {
            InvoiceSettings = new CustomerInvoiceSettings(),
            Metadata = new Dictionary<string, string>()
        };

        _subscriberService.GetCustomer(organization).Returns(customer);

        var hasPaymentMethod = await _query.Run(organization);

        Assert.False(hasPaymentMethod);
    }

    [Fact]
    public async Task Run_HasDefaultPaymentMethodId_ReturnsTrue()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid()
        };

        var customer = new Customer
        {
            InvoiceSettings = new CustomerInvoiceSettings
            {
                DefaultPaymentMethodId = "pm_123"
            },
            Metadata = new Dictionary<string, string>()
        };

        _subscriberService.GetCustomer(organization).Returns(customer);

        var hasPaymentMethod = await _query.Run(organization);

        Assert.True(hasPaymentMethod);
    }

    [Fact]
    public async Task Run_HasDefaultSourceId_ReturnsTrue()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid()
        };

        var customer = new Customer
        {
            DefaultSourceId = "card_123",
            InvoiceSettings = new CustomerInvoiceSettings(),
            Metadata = new Dictionary<string, string>()
        };

        _subscriberService.GetCustomer(organization).Returns(customer);

        var hasPaymentMethod = await _query.Run(organization);

        Assert.True(hasPaymentMethod);
    }

    [Fact]
    public async Task Run_HasUnverifiedBankAccount_ReturnsTrue()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid()
        };

        var customer = new Customer
        {
            InvoiceSettings = new CustomerInvoiceSettings(),
            Metadata = new Dictionary<string, string>()
        };

        _subscriberService.GetCustomer(organization).Returns(customer);
        _setupIntentCache.GetSetupIntentIdForSubscriber(organization.Id).Returns("seti_123");

        _stripeAdapter
            .GetSetupIntentAsync("seti_123",
                Arg.Is<SetupIntentGetOptions>(options => options.HasExpansions("payment_method")))
            .Returns(new SetupIntent
            {
                Status = "requires_action",
                NextAction = new SetupIntentNextAction
                {
                    VerifyWithMicrodeposits = new SetupIntentNextActionVerifyWithMicrodeposits()
                },
                PaymentMethod = new PaymentMethod
                {
                    UsBankAccount = new PaymentMethodUsBankAccount()
                }
            });

        var hasPaymentMethod = await _query.Run(organization);

        Assert.True(hasPaymentMethod);
    }

    [Fact]
    public async Task Run_HasBraintreeCustomerId_ReturnsTrue()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid()
        };

        var customer = new Customer
        {
            InvoiceSettings = new CustomerInvoiceSettings(),
            Metadata = new Dictionary<string, string>
            {
                [MetadataKeys.BraintreeCustomerId] = "braintree_customer_id"
            }
        };

        _subscriberService.GetCustomer(organization).Returns(customer);

        var hasPaymentMethod = await _query.Run(organization);

        Assert.True(hasPaymentMethod);
    }

    [Fact]
    public async Task Run_NoSetupIntentId_ReturnsFalse()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid()
        };

        var customer = new Customer
        {
            InvoiceSettings = new CustomerInvoiceSettings(),
            Metadata = new Dictionary<string, string>()
        };

        _subscriberService.GetCustomer(organization).Returns(customer);
        _setupIntentCache.GetSetupIntentIdForSubscriber(organization.Id).Returns((string)null);

        var hasPaymentMethod = await _query.Run(organization);

        Assert.False(hasPaymentMethod);
    }

    [Fact]
    public async Task Run_SetupIntentNotBankAccount_ReturnsFalse()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid()
        };

        var customer = new Customer
        {
            InvoiceSettings = new CustomerInvoiceSettings(),
            Metadata = new Dictionary<string, string>()
        };

        _subscriberService.GetCustomer(organization).Returns(customer);
        _setupIntentCache.GetSetupIntentIdForSubscriber(organization.Id).Returns("seti_123");

        _stripeAdapter
            .GetSetupIntentAsync("seti_123",
                Arg.Is<SetupIntentGetOptions>(options => options.HasExpansions("payment_method")))
            .Returns(new SetupIntent
            {
                PaymentMethod = new PaymentMethod
                {
                    Type = "card"
                },
                Status = "succeeded"
            });

        var hasPaymentMethod = await _query.Run(organization);

        Assert.False(hasPaymentMethod);
    }
}
