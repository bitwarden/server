using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Caches;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Payment.Commands;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Services;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Test.Billing.Extensions;
using Braintree;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stripe;
using Xunit;
using Address = Stripe.Address;
using Customer = Stripe.Customer;
using PaymentMethod = Stripe.PaymentMethod;

namespace Bit.Core.Test.Billing.Payment.Commands;

using static StripeConstants;

public class UpdatePaymentMethodCommandTests
{
    private readonly IBraintreeGateway _braintreeGateway = Substitute.For<IBraintreeGateway>();
    private readonly IGlobalSettings _globalSettings = Substitute.For<IGlobalSettings>();
    private readonly ISetupIntentCache _setupIntentCache = Substitute.For<ISetupIntentCache>();
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly ISubscriberService _subscriberService = Substitute.For<ISubscriberService>();
    private readonly UpdatePaymentMethodCommand _command;

    public UpdatePaymentMethodCommandTests()
    {
        _command = new UpdatePaymentMethodCommand(
            _braintreeGateway,
            _globalSettings,
            Substitute.For<ILogger<UpdatePaymentMethodCommand>>(),
            _setupIntentCache,
            _stripeAdapter,
            _subscriberService);
    }

    [Fact]
    public async Task Run_BankAccount_MakesCorrectInvocations_ReturnsMaskedBankAccount()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            GatewayCustomerId = "cus_123"
        };

        var customer = new Customer
        {
            Address = new Address
            {
                Country = "US",
                PostalCode = "12345"
            },
            Metadata = new Dictionary<string, string>()
        };

        _subscriberService.GetCustomer(organization).Returns(customer);

        const string token = "TOKEN";

        var setupIntent = new SetupIntent
        {
            Id = "seti_123",
            PaymentMethod =
                new PaymentMethod
                {
                    Type = "us_bank_account",
                    UsBankAccount = new PaymentMethodUsBankAccount { BankName = "Chase", Last4 = "9999" }
                },
            NextAction = new SetupIntentNextAction
            {
                VerifyWithMicrodeposits = new SetupIntentNextActionVerifyWithMicrodeposits()
            },
            Status = "requires_action"
        };

        _stripeAdapter.SetupIntentList(Arg.Is<SetupIntentListOptions>(options =>
            options.PaymentMethod == token && options.HasExpansions("data.payment_method"))).Returns([setupIntent]);

        var result = await _command.Run(organization,
            new TokenizedPaymentMethod { Type = TokenizablePaymentMethodType.BankAccount, Token = token }, new BillingAddress
            {
                Country = "US",
                PostalCode = "12345"
            });

        Assert.True(result.IsT0);
        var maskedPaymentMethod = result.AsT0;
        Assert.True(maskedPaymentMethod.IsT0);
        var maskedBankAccount = maskedPaymentMethod.AsT0;
        Assert.Equal("Chase", maskedBankAccount.BankName);
        Assert.Equal("9999", maskedBankAccount.Last4);
        Assert.False(maskedBankAccount.Verified);

        await _setupIntentCache.Received(1).Set(organization.Id, setupIntent.Id);
    }

    [Fact]
    public async Task Run_BankAccount_NoCurrentCustomer_MakesCorrectInvocations_ReturnsMaskedBankAccount()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid()
        };

        var customer = new Customer
        {
            Address = new Address
            {
                Country = "US",
                PostalCode = "12345"
            },
            Metadata = new Dictionary<string, string>()
        };

        _subscriberService.GetCustomer(organization).Returns(customer);

        const string token = "TOKEN";

        var setupIntent = new SetupIntent
        {
            Id = "seti_123",
            PaymentMethod =
                new PaymentMethod
                {
                    Type = "us_bank_account",
                    UsBankAccount = new PaymentMethodUsBankAccount { BankName = "Chase", Last4 = "9999" }
                },
            NextAction = new SetupIntentNextAction
            {
                VerifyWithMicrodeposits = new SetupIntentNextActionVerifyWithMicrodeposits()
            },
            Status = "requires_action"
        };

        _stripeAdapter.SetupIntentList(Arg.Is<SetupIntentListOptions>(options =>
            options.PaymentMethod == token && options.HasExpansions("data.payment_method"))).Returns([setupIntent]);

        var result = await _command.Run(organization,
            new TokenizedPaymentMethod { Type = TokenizablePaymentMethodType.BankAccount, Token = token }, new BillingAddress
            {
                Country = "US",
                PostalCode = "12345"
            });

        Assert.True(result.IsT0);
        var maskedPaymentMethod = result.AsT0;
        Assert.True(maskedPaymentMethod.IsT0);
        var maskedBankAccount = maskedPaymentMethod.AsT0;
        Assert.Equal("Chase", maskedBankAccount.BankName);
        Assert.Equal("9999", maskedBankAccount.Last4);
        Assert.False(maskedBankAccount.Verified);

        await _subscriberService.Received(1).CreateStripeCustomer(organization);

        await _setupIntentCache.Received(1).Set(organization.Id, setupIntent.Id);
    }

    [Fact]
    public async Task Run_BankAccount_StripeToPayPal_MakesCorrectInvocations_ReturnsMaskedBankAccount()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            GatewayCustomerId = "cus_123"
        };

        var customer = new Customer
        {
            Address = new Address
            {
                Country = "US",
                PostalCode = "12345"
            },
            Id = "cus_123",
            Metadata = new Dictionary<string, string>
            {
                [MetadataKeys.BraintreeCustomerId] = "braintree_customer_id"
            }
        };

        _subscriberService.GetCustomer(organization).Returns(customer);

        const string token = "TOKEN";

        var setupIntent = new SetupIntent
        {
            Id = "seti_123",
            PaymentMethod =
                new PaymentMethod
                {
                    Type = "us_bank_account",
                    UsBankAccount = new PaymentMethodUsBankAccount { BankName = "Chase", Last4 = "9999" }
                },
            NextAction = new SetupIntentNextAction
            {
                VerifyWithMicrodeposits = new SetupIntentNextActionVerifyWithMicrodeposits()
            },
            Status = "requires_action"
        };

        _stripeAdapter.SetupIntentList(Arg.Is<SetupIntentListOptions>(options =>
            options.PaymentMethod == token && options.HasExpansions("data.payment_method"))).Returns([setupIntent]);

        var result = await _command.Run(organization,
            new TokenizedPaymentMethod { Type = TokenizablePaymentMethodType.BankAccount, Token = token }, new BillingAddress
            {
                Country = "US",
                PostalCode = "12345"
            });

        Assert.True(result.IsT0);
        var maskedPaymentMethod = result.AsT0;
        Assert.True(maskedPaymentMethod.IsT0);
        var maskedBankAccount = maskedPaymentMethod.AsT0;
        Assert.Equal("Chase", maskedBankAccount.BankName);
        Assert.Equal("9999", maskedBankAccount.Last4);
        Assert.False(maskedBankAccount.Verified);

        await _setupIntentCache.Received(1).Set(organization.Id, setupIntent.Id);
        await _stripeAdapter.Received(1).CustomerUpdateAsync(customer.Id, Arg.Is<CustomerUpdateOptions>(options =>
            options.Metadata[MetadataKeys.BraintreeCustomerId] == string.Empty &&
            options.Metadata[MetadataKeys.RetiredBraintreeCustomerId] == "braintree_customer_id"));
    }

    [Fact]
    public async Task Run_Card_MakesCorrectInvocations_ReturnsMaskedCard()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            GatewayCustomerId = "cus_123"
        };

        var customer = new Customer
        {
            Address = new Address
            {
                Country = "US",
                PostalCode = "12345"
            },
            Id = "cus_123",
            Metadata = new Dictionary<string, string>()
        };

        _subscriberService.GetCustomer(organization).Returns(customer);

        const string token = "TOKEN";

        _stripeAdapter
            .PaymentMethodAttachAsync(token,
                Arg.Is<PaymentMethodAttachOptions>(options => options.Customer == customer.Id))
            .Returns(new PaymentMethod
            {
                Type = "card",
                Card = new PaymentMethodCard
                {
                    Brand = "visa",
                    Last4 = "9999",
                    ExpMonth = 1,
                    ExpYear = 2028
                }
            });

        var result = await _command.Run(organization,
            new TokenizedPaymentMethod { Type = TokenizablePaymentMethodType.Card, Token = token }, new BillingAddress
            {
                Country = "US",
                PostalCode = "12345"
            });

        Assert.True(result.IsT0);
        var maskedPaymentMethod = result.AsT0;
        Assert.True(maskedPaymentMethod.IsT1);
        var maskedCard = maskedPaymentMethod.AsT1;
        Assert.Equal("visa", maskedCard.Brand);
        Assert.Equal("9999", maskedCard.Last4);
        Assert.Equal("01/2028", maskedCard.Expiration);

        await _stripeAdapter.Received(1).CustomerUpdateAsync(customer.Id,
            Arg.Is<CustomerUpdateOptions>(options => options.InvoiceSettings.DefaultPaymentMethod == token));
    }

    [Fact]
    public async Task Run_Card_PropagateBillingAddress_MakesCorrectInvocations_ReturnsMaskedCard()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            GatewayCustomerId = "cus_123"
        };

        var customer = new Customer
        {
            Id = "cus_123",
            Metadata = new Dictionary<string, string>()
        };

        _subscriberService.GetCustomer(organization).Returns(customer);

        const string token = "TOKEN";

        _stripeAdapter
            .PaymentMethodAttachAsync(token,
                Arg.Is<PaymentMethodAttachOptions>(options => options.Customer == customer.Id))
            .Returns(new PaymentMethod
            {
                Type = "card",
                Card = new PaymentMethodCard
                {
                    Brand = "visa",
                    Last4 = "9999",
                    ExpMonth = 1,
                    ExpYear = 2028
                }
            });

        var result = await _command.Run(organization,
            new TokenizedPaymentMethod { Type = TokenizablePaymentMethodType.Card, Token = token }, new BillingAddress
            {
                Country = "US",
                PostalCode = "12345"
            });

        Assert.True(result.IsT0);
        var maskedPaymentMethod = result.AsT0;
        Assert.True(maskedPaymentMethod.IsT1);
        var maskedCard = maskedPaymentMethod.AsT1;
        Assert.Equal("visa", maskedCard.Brand);
        Assert.Equal("9999", maskedCard.Last4);
        Assert.Equal("01/2028", maskedCard.Expiration);

        await _stripeAdapter.Received(1).CustomerUpdateAsync(customer.Id,
            Arg.Is<CustomerUpdateOptions>(options => options.InvoiceSettings.DefaultPaymentMethod == token));

        await _stripeAdapter.Received(1).CustomerUpdateAsync(customer.Id,
            Arg.Is<CustomerUpdateOptions>(options => options.Address.Country == "US" && options.Address.PostalCode == "12345"));
    }

    [Fact]
    public async Task Run_PayPal_ExistingBraintreeCustomer_MakesCorrectInvocations_ReturnsMaskedPayPalAccount()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            GatewayCustomerId = "cus_123"
        };

        var customer = new Customer
        {
            Address = new Address
            {
                Country = "US",
                PostalCode = "12345"
            },
            Id = "cus_123",
            Metadata = new Dictionary<string, string>
            {
                [MetadataKeys.BraintreeCustomerId] = "braintree_customer_id"
            }
        };

        _subscriberService.GetCustomer(organization).Returns(customer);

        var customerGateway = Substitute.For<ICustomerGateway>();
        var braintreeCustomer = Substitute.For<Braintree.Customer>();
        braintreeCustomer.Id.Returns("braintree_customer_id");
        var existing = Substitute.For<PayPalAccount>();
        existing.Email.Returns("user@gmail.com");
        existing.IsDefault.Returns(true);
        existing.Token.Returns("EXISTING");
        braintreeCustomer.PaymentMethods.Returns([existing]);
        customerGateway.FindAsync("braintree_customer_id").Returns(braintreeCustomer);
        _braintreeGateway.Customer.Returns(customerGateway);

        var paymentMethodGateway = Substitute.For<IPaymentMethodGateway>();
        var updated = Substitute.For<PayPalAccount>();
        updated.Email.Returns("user@gmail.com");
        updated.Token.Returns("UPDATED");
        var updatedResult = Substitute.For<Result<Braintree.PaymentMethod>>();
        updatedResult.Target.Returns(updated);
        paymentMethodGateway.CreateAsync(Arg.Is<PaymentMethodRequest>(options =>
                options.CustomerId == braintreeCustomer.Id && options.PaymentMethodNonce == "TOKEN"))
            .Returns(updatedResult);
        _braintreeGateway.PaymentMethod.Returns(paymentMethodGateway);

        var result = await _command.Run(organization,
            new TokenizedPaymentMethod { Type = TokenizablePaymentMethodType.PayPal, Token = "TOKEN" },
            new BillingAddress { Country = "US", PostalCode = "12345" });

        Assert.True(result.IsT0);
        var maskedPaymentMethod = result.AsT0;
        Assert.True(maskedPaymentMethod.IsT2);
        var maskedPayPalAccount = maskedPaymentMethod.AsT2;
        Assert.Equal("user@gmail.com", maskedPayPalAccount.Email);

        await customerGateway.Received(1).UpdateAsync(braintreeCustomer.Id,
            Arg.Is<CustomerRequest>(options => options.DefaultPaymentMethodToken == updated.Token));
        await paymentMethodGateway.Received(1).DeleteAsync(existing.Token);
    }

    [Fact]
    public async Task Run_PayPal_NewBraintreeCustomer_MakesCorrectInvocations_ReturnsMaskedPayPalAccount()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            GatewayCustomerId = "cus_123"
        };

        var customer = new Customer
        {
            Address = new Address
            {
                Country = "US",
                PostalCode = "12345"
            },
            Id = "cus_123",
            Metadata = new Dictionary<string, string>()
        };

        _subscriberService.GetCustomer(organization).Returns(customer);

        _globalSettings.BaseServiceUri.Returns(new GlobalSettings.BaseServiceUriSettings(new GlobalSettings())
        {
            CloudRegion = "US"
        });

        var customerGateway = Substitute.For<ICustomerGateway>();
        var braintreeCustomer = Substitute.For<Braintree.Customer>();
        braintreeCustomer.Id.Returns("braintree_customer_id");
        var payPalAccount = Substitute.For<PayPalAccount>();
        payPalAccount.Email.Returns("user@gmail.com");
        payPalAccount.IsDefault.Returns(true);
        payPalAccount.Token.Returns("NONCE");
        braintreeCustomer.PaymentMethods.Returns([payPalAccount]);
        var createResult = Substitute.For<Result<Braintree.Customer>>();
        createResult.Target.Returns(braintreeCustomer);
        customerGateway.CreateAsync(Arg.Is<CustomerRequest>(options =>
            options.Id.StartsWith(organization.BraintreeCustomerIdPrefix() + organization.Id.ToString("N").ToLower()) &&
            options.CustomFields[organization.BraintreeIdField()] == organization.Id.ToString() &&
            options.CustomFields[organization.BraintreeCloudRegionField()] == "US" &&
            options.Email == organization.BillingEmailAddress() &&
            options.PaymentMethodNonce == "TOKEN")).Returns(createResult);
        _braintreeGateway.Customer.Returns(customerGateway);

        var result = await _command.Run(organization,
            new TokenizedPaymentMethod { Type = TokenizablePaymentMethodType.PayPal, Token = "TOKEN" },
            new BillingAddress { Country = "US", PostalCode = "12345" });

        Assert.True(result.IsT0);
        var maskedPaymentMethod = result.AsT0;
        Assert.True(maskedPaymentMethod.IsT2);
        var maskedPayPalAccount = maskedPaymentMethod.AsT2;
        Assert.Equal("user@gmail.com", maskedPayPalAccount.Email);

        await _stripeAdapter.Received(1).CustomerUpdateAsync(customer.Id,
            Arg.Is<CustomerUpdateOptions>(options =>
                options.Metadata[MetadataKeys.BraintreeCustomerId] == "braintree_customer_id"));
    }
}
