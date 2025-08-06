using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Caches;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Payment.Queries;
using Bit.Core.Billing.Services;
using Bit.Core.Services;
using Bit.Core.Test.Billing.Extensions;
using Braintree;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Stripe;
using Xunit;
using Customer = Stripe.Customer;
using PaymentMethod = Stripe.PaymentMethod;

namespace Bit.Core.Test.Billing.Payment.Queries;

using static StripeConstants;

public class GetPaymentMethodQueryTests
{
    private readonly IBraintreeGateway _braintreeGateway = Substitute.For<IBraintreeGateway>();
    private readonly ISetupIntentCache _setupIntentCache = Substitute.For<ISetupIntentCache>();
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly ISubscriberService _subscriberService = Substitute.For<ISubscriberService>();
    private readonly GetPaymentMethodQuery _query;

    public GetPaymentMethodQueryTests()
    {
        _query = new GetPaymentMethodQuery(
            _braintreeGateway,
            Substitute.For<ILogger<GetPaymentMethodQuery>>(),
            _setupIntentCache,
            _stripeAdapter,
            _subscriberService);
    }

    [Fact]
    public async Task Run_NoCustomer_ReturnsNull()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid()
        };

        _subscriberService.GetCustomer(organization,
            Arg.Is<CustomerGetOptions>(options =>
                options.HasExpansions("default_source", "invoice_settings.default_payment_method"))).ReturnsNull();

        var maskedPaymentMethod = await _query.Run(organization);

        Assert.Null(maskedPaymentMethod);
    }

    [Fact]
    public async Task Run_NoPaymentMethod_ReturnsNull()
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

        _subscriberService.GetCustomer(organization,
            Arg.Is<CustomerGetOptions>(options =>
                options.HasExpansions("default_source", "invoice_settings.default_payment_method"))).Returns(customer);

        var maskedPaymentMethod = await _query.Run(organization);

        Assert.Null(maskedPaymentMethod);
    }

    [Fact]
    public async Task Run_BankAccount_FromPaymentMethod_ReturnsMaskedBankAccount()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid()
        };

        var customer = new Customer
        {
            InvoiceSettings = new CustomerInvoiceSettings
            {
                DefaultPaymentMethod = new PaymentMethod
                {
                    Type = "us_bank_account",
                    UsBankAccount = new PaymentMethodUsBankAccount { BankName = "Chase", Last4 = "9999" }
                }
            },
            Metadata = new Dictionary<string, string>()
        };

        _subscriberService.GetCustomer(organization,
            Arg.Is<CustomerGetOptions>(options =>
                options.HasExpansions("default_source", "invoice_settings.default_payment_method"))).Returns(customer);

        var maskedPaymentMethod = await _query.Run(organization);

        Assert.NotNull(maskedPaymentMethod);
        Assert.True(maskedPaymentMethod.IsT0);
        var maskedBankAccount = maskedPaymentMethod.AsT0;
        Assert.Equal("Chase", maskedBankAccount.BankName);
        Assert.Equal("9999", maskedBankAccount.Last4);
        Assert.True(maskedBankAccount.Verified);
    }

    [Fact]
    public async Task Run_BankAccount_FromSource_ReturnsMaskedBankAccount()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid()
        };

        var customer = new Customer
        {
            DefaultSource = new BankAccount
            {
                BankName = "Chase",
                Last4 = "9999",
                Status = "verified"
            },
            InvoiceSettings = new CustomerInvoiceSettings(),
            Metadata = new Dictionary<string, string>()
        };

        _subscriberService.GetCustomer(organization,
            Arg.Is<CustomerGetOptions>(options =>
                options.HasExpansions("default_source", "invoice_settings.default_payment_method"))).Returns(customer);

        var maskedPaymentMethod = await _query.Run(organization);

        Assert.NotNull(maskedPaymentMethod);
        Assert.True(maskedPaymentMethod.IsT0);
        var maskedBankAccount = maskedPaymentMethod.AsT0;
        Assert.Equal("Chase", maskedBankAccount.BankName);
        Assert.Equal("9999", maskedBankAccount.Last4);
        Assert.True(maskedBankAccount.Verified);
    }

    [Fact]
    public async Task Run_BankAccount_FromSetupIntent_ReturnsMaskedBankAccount()
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

        _subscriberService.GetCustomer(organization,
            Arg.Is<CustomerGetOptions>(options =>
                options.HasExpansions("default_source", "invoice_settings.default_payment_method"))).Returns(customer);

        _setupIntentCache.Get(organization.Id).Returns("seti_123");

        _stripeAdapter
            .SetupIntentGet("seti_123",
                Arg.Is<SetupIntentGetOptions>(options => options.HasExpansions("payment_method"))).Returns(
                new SetupIntent
                {
                    PaymentMethod = new PaymentMethod
                    {
                        Type = "us_bank_account",
                        UsBankAccount = new PaymentMethodUsBankAccount { BankName = "Chase", Last4 = "9999" }
                    },
                    NextAction = new SetupIntentNextAction
                    {
                        VerifyWithMicrodeposits = new SetupIntentNextActionVerifyWithMicrodeposits()
                    },
                    Status = "requires_action"
                });

        var maskedPaymentMethod = await _query.Run(organization);

        Assert.NotNull(maskedPaymentMethod);
        Assert.True(maskedPaymentMethod.IsT0);
        var maskedBankAccount = maskedPaymentMethod.AsT0;
        Assert.Equal("Chase", maskedBankAccount.BankName);
        Assert.Equal("9999", maskedBankAccount.Last4);
        Assert.False(maskedBankAccount.Verified);
    }

    [Fact]
    public async Task Run_Card_FromPaymentMethod_ReturnsMaskedCard()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid()
        };

        var customer = new Customer
        {
            InvoiceSettings = new CustomerInvoiceSettings
            {
                DefaultPaymentMethod = new PaymentMethod
                {
                    Type = "card",
                    Card = new PaymentMethodCard
                    {
                        Brand = "visa",
                        Last4 = "9999",
                        ExpMonth = 1,
                        ExpYear = 2028
                    }
                }
            },
            Metadata = new Dictionary<string, string>()
        };

        _subscriberService.GetCustomer(organization,
            Arg.Is<CustomerGetOptions>(options =>
                options.HasExpansions("default_source", "invoice_settings.default_payment_method"))).Returns(customer);

        var maskedPaymentMethod = await _query.Run(organization);

        Assert.NotNull(maskedPaymentMethod);
        Assert.True(maskedPaymentMethod.IsT1);
        var maskedCard = maskedPaymentMethod.AsT1;
        Assert.Equal("visa", maskedCard.Brand);
        Assert.Equal("9999", maskedCard.Last4);
        Assert.Equal("01/2028", maskedCard.Expiration);
    }

    [Fact]
    public async Task Run_Card_FromSource_ReturnsMaskedCard()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid()
        };

        var customer = new Customer
        {
            DefaultSource = new Card
            {
                Brand = "visa",
                Last4 = "9999",
                ExpMonth = 1,
                ExpYear = 2028
            },
            InvoiceSettings = new CustomerInvoiceSettings(),
            Metadata = new Dictionary<string, string>()
        };

        _subscriberService.GetCustomer(organization,
            Arg.Is<CustomerGetOptions>(options =>
                options.HasExpansions("default_source", "invoice_settings.default_payment_method"))).Returns(customer);

        var maskedPaymentMethod = await _query.Run(organization);

        Assert.NotNull(maskedPaymentMethod);
        Assert.True(maskedPaymentMethod.IsT1);
        var maskedCard = maskedPaymentMethod.AsT1;
        Assert.Equal("visa", maskedCard.Brand);
        Assert.Equal("9999", maskedCard.Last4);
        Assert.Equal("01/2028", maskedCard.Expiration);
    }

    [Fact]
    public async Task Run_Card_FromSourceCard_ReturnsMaskedCard()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid()
        };

        var customer = new Customer
        {
            DefaultSource = new Source
            {
                Card = new SourceCard
                {
                    Brand = "Visa",
                    Last4 = "9999",
                    ExpMonth = 1,
                    ExpYear = 2028
                }
            },
            InvoiceSettings = new CustomerInvoiceSettings(),
            Metadata = new Dictionary<string, string>()
        };

        _subscriberService.GetCustomer(organization,
            Arg.Is<CustomerGetOptions>(options =>
                options.HasExpansions("default_source", "invoice_settings.default_payment_method"))).Returns(customer);

        var maskedPaymentMethod = await _query.Run(organization);

        Assert.NotNull(maskedPaymentMethod);
        Assert.True(maskedPaymentMethod.IsT1);
        var maskedCard = maskedPaymentMethod.AsT1;
        Assert.Equal("visa", maskedCard.Brand);
        Assert.Equal("9999", maskedCard.Last4);
        Assert.Equal("01/2028", maskedCard.Expiration);
    }

    [Fact]
    public async Task Run_PayPalAccount_ReturnsMaskedPayPalAccount()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid()
        };

        var customer = new Customer
        {
            Metadata = new Dictionary<string, string>
            {
                [MetadataKeys.BraintreeCustomerId] = "braintree_customer_id"
            }
        };

        _subscriberService.GetCustomer(organization,
            Arg.Is<CustomerGetOptions>(options =>
                options.HasExpansions("default_source", "invoice_settings.default_payment_method"))).Returns(customer);

        var customerGateway = Substitute.For<ICustomerGateway>();
        var braintreeCustomer = Substitute.For<Braintree.Customer>();
        var payPalAccount = Substitute.For<PayPalAccount>();
        payPalAccount.Email.Returns("user@gmail.com");
        payPalAccount.IsDefault.Returns(true);
        braintreeCustomer.PaymentMethods.Returns([payPalAccount]);
        customerGateway.FindAsync("braintree_customer_id").Returns(braintreeCustomer);
        _braintreeGateway.Customer.Returns(customerGateway);

        var maskedPaymentMethod = await _query.Run(organization);

        Assert.NotNull(maskedPaymentMethod);
        Assert.True(maskedPaymentMethod.IsT2);
        var maskedPayPalAccount = maskedPaymentMethod.AsT2;
        Assert.Equal("user@gmail.com", maskedPayPalAccount.Email);
    }

    #region GetPaymentMethodDescription Tests

    [Fact]
    public void GetPaymentMethodDescription_PayPalAccount_ReturnsCorrectDescription()
    {
        // Arrange
        var customer = new Customer
        {
            Metadata = new Dictionary<string, string>
            {
                [MetadataKeys.BraintreeCustomerId] = "braintree_customer_id"
            }
        };

        // Act
        var result = _query.GetPaymentMethodDescription(customer);

        // Assert
        Assert.Equal("PayPal account", result);
    }

    [Fact]
    public void GetPaymentMethodDescription_CreditCard_ReturnsCorrectDescription()
    {
        // Arrange
        var customer = new Customer
        {
            InvoiceSettings = new CustomerInvoiceSettings
            {
                DefaultPaymentMethod = new PaymentMethod
                {
                    Type = "card",
                    Card = new PaymentMethodCard { Last4 = "1234" }
                }
            },
            Metadata = new Dictionary<string, string>()
        };

        // Act
        var result = _query.GetPaymentMethodDescription(customer);

        // Assert
        Assert.Equal("Credit card ending in 1234", result);
    }

    [Fact]
    public void GetPaymentMethodDescription_BankAccount_ReturnsCorrectDescription()
    {
        // Arrange
        var customer = new Customer
        {
            InvoiceSettings = new CustomerInvoiceSettings
            {
                DefaultPaymentMethod = new PaymentMethod
                {
                    Type = "us_bank_account",
                    UsBankAccount = new PaymentMethodUsBankAccount { Last4 = "5678" }
                }
            },
            Metadata = new Dictionary<string, string>()
        };

        // Act
        var result = _query.GetPaymentMethodDescription(customer);

        // Assert
        Assert.Equal("Bank account ending in 5678", result);
    }

    [Fact]
    public void GetPaymentMethodDescription_UnknownPaymentMethodType_ReturnsGenericDescription()
    {
        // Arrange
        var customer = new Customer
        {
            InvoiceSettings = new CustomerInvoiceSettings
            {
                DefaultPaymentMethod = new PaymentMethod
                {
                    Type = "unknown_type"
                }
            },
            Metadata = new Dictionary<string, string>()
        };

        // Act
        var result = _query.GetPaymentMethodDescription(customer);

        // Assert
        Assert.Equal("Payment method", result);
    }

    [Fact]
    public void GetPaymentMethodDescription_DefaultSourceCard_ReturnsCorrectDescription()
    {
        // Arrange
        var customer = new Customer
        {
            DefaultSource = new Card { Last4 = "9999" },
            InvoiceSettings = new CustomerInvoiceSettings(),
            Metadata = new Dictionary<string, string>()
        };

        // Act
        var result = _query.GetPaymentMethodDescription(customer);

        // Assert
        Assert.Equal("Credit card ending in 9999", result);
    }

    [Fact]
    public void GetPaymentMethodDescription_DefaultSourceBankAccount_ReturnsCorrectDescription()
    {
        // Arrange
        var customer = new Customer
        {
            DefaultSource = new BankAccount { Last4 = "8888" },
            InvoiceSettings = new CustomerInvoiceSettings(),
            Metadata = new Dictionary<string, string>()
        };

        // Act
        var result = _query.GetPaymentMethodDescription(customer);

        // Assert
        Assert.Equal("Bank account ending in 8888", result);
    }

    [Fact]
    public void GetPaymentMethodDescription_NoPaymentMethod_ReturnsNull()
    {
        // Arrange
        var customer = new Customer
        {
            InvoiceSettings = new CustomerInvoiceSettings(),
            Metadata = new Dictionary<string, string>()
        };

        // Act
        var result = _query.GetPaymentMethodDescription(customer);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region HasPaymentMethod Tests

    [Fact]
    public void HasPaymentMethod_PayPalAccount_ReturnsTrue()
    {
        // Arrange
        var customer = new Customer
        {
            Metadata = new Dictionary<string, string>
            {
                [MetadataKeys.BraintreeCustomerId] = "braintree_customer_id"
            }
        };

        // Act
        var result = _query.HasPaymentMethod(customer);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasPaymentMethod_DefaultPaymentMethod_ReturnsTrue()
    {
        // Arrange
        var customer = new Customer
        {
            InvoiceSettings = new CustomerInvoiceSettings
            {
                DefaultPaymentMethod = new PaymentMethod
                {
                    Type = "card"
                }
            },
            Metadata = new Dictionary<string, string>()
        };

        // Act
        var result = _query.HasPaymentMethod(customer);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasPaymentMethod_DefaultSource_ReturnsTrue()
    {
        // Arrange
        var customer = new Customer
        {
            DefaultSource = new Card(),
            InvoiceSettings = new CustomerInvoiceSettings(),
            Metadata = new Dictionary<string, string>()
        };

        // Act
        var result = _query.HasPaymentMethod(customer);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasPaymentMethod_NoPaymentMethod_ReturnsFalse()
    {
        // Arrange
        var customer = new Customer
        {
            InvoiceSettings = new CustomerInvoiceSettings(),
            Metadata = new Dictionary<string, string>()
        };

        // Act
        var result = _query.HasPaymentMethod(customer);

        // Assert
        Assert.False(result);
    }

    #endregion
}
