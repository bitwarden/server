using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Commands.Implementations;
using Bit.Core.Enums;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;
using static Bit.Core.Test.Billing.Utilities;
using BT = Braintree;
using S = Stripe;

namespace Bit.Core.Test.Billing.Commands;

[SutProviderCustomize]
public class RemovePaymentMethodCommandTests
{
    [Theory, BitAutoData]
    public async Task RemovePaymentMethod_NullOrganization_ArgumentNullException(
        SutProvider<RemovePaymentMethodCommand> sutProvider) =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => sutProvider.Sut.RemovePaymentMethod(null));

    [Theory, BitAutoData]
    public async Task RemovePaymentMethod_NonStripeGateway_ContactSupport(
        Organization organization,
        SutProvider<RemovePaymentMethodCommand> sutProvider)
    {
        organization.Gateway = GatewayType.BitPay;

        await ThrowsContactSupportAsync(() => sutProvider.Sut.RemovePaymentMethod(organization));
    }

    [Theory, BitAutoData]
    public async Task RemovePaymentMethod_NoGatewayCustomerId_ContactSupport(
        Organization organization,
        SutProvider<RemovePaymentMethodCommand> sutProvider)
    {
        organization.Gateway = GatewayType.Stripe;
        organization.GatewayCustomerId = null;

        await ThrowsContactSupportAsync(() => sutProvider.Sut.RemovePaymentMethod(organization));
    }

    [Theory, BitAutoData]
    public async Task RemovePaymentMethod_NoStripeCustomer_ContactSupport(
        Organization organization,
        SutProvider<RemovePaymentMethodCommand> sutProvider)
    {
        organization.Gateway = GatewayType.Stripe;

        sutProvider.GetDependency<IStripeAdapter>()
            .CustomerGetAsync(organization.GatewayCustomerId, Arg.Any<S.CustomerGetOptions>())
            .ReturnsNull();

        await ThrowsContactSupportAsync(() => sutProvider.Sut.RemovePaymentMethod(organization));
    }

    [Theory, BitAutoData]
    public async Task RemovePaymentMethod_Braintree_NoCustomer_ContactSupport(
        Organization organization,
        SutProvider<RemovePaymentMethodCommand> sutProvider)
    {
        organization.Gateway = GatewayType.Stripe;

        const string braintreeCustomerId = "1";

        var stripeCustomer = new S.Customer
        {
            Metadata = new Dictionary<string, string>
            {
                { "btCustomerId", braintreeCustomerId }
            }
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .CustomerGetAsync(organization.GatewayCustomerId, Arg.Any<S.CustomerGetOptions>())
            .Returns(stripeCustomer);

        var (braintreeGateway, customerGateway, paymentMethodGateway) = Setup(sutProvider.GetDependency<BT.IBraintreeGateway>());

        customerGateway.FindAsync(braintreeCustomerId).ReturnsNull();

        braintreeGateway.Customer.Returns(customerGateway);

        await ThrowsContactSupportAsync(() => sutProvider.Sut.RemovePaymentMethod(organization));

        await customerGateway.Received(1).FindAsync(braintreeCustomerId);

        await customerGateway.DidNotReceiveWithAnyArgs()
            .UpdateAsync(Arg.Any<string>(), Arg.Any<BT.CustomerRequest>());

        await paymentMethodGateway.DidNotReceiveWithAnyArgs().DeleteAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task RemovePaymentMethod_Braintree_NoPaymentMethod_NoOp(
        Organization organization,
        SutProvider<RemovePaymentMethodCommand> sutProvider)
    {
        organization.Gateway = GatewayType.Stripe;

        const string braintreeCustomerId = "1";

        var stripeCustomer = new S.Customer
        {
            Metadata = new Dictionary<string, string>
            {
                { "btCustomerId", braintreeCustomerId }
            }
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .CustomerGetAsync(organization.GatewayCustomerId, Arg.Any<S.CustomerGetOptions>())
            .Returns(stripeCustomer);

        var (_, customerGateway, paymentMethodGateway) = Setup(sutProvider.GetDependency<BT.IBraintreeGateway>());

        var braintreeCustomer = Substitute.For<BT.Customer>();

        braintreeCustomer.PaymentMethods.Returns(Array.Empty<BT.PaymentMethod>());

        customerGateway.FindAsync(braintreeCustomerId).Returns(braintreeCustomer);

        await sutProvider.Sut.RemovePaymentMethod(organization);

        await customerGateway.Received(1).FindAsync(braintreeCustomerId);

        await customerGateway.DidNotReceiveWithAnyArgs().UpdateAsync(Arg.Any<string>(), Arg.Any<BT.CustomerRequest>());

        await paymentMethodGateway.DidNotReceiveWithAnyArgs().DeleteAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task RemovePaymentMethod_Braintree_CustomerUpdateFails_ContactSupport(
        Organization organization,
        SutProvider<RemovePaymentMethodCommand> sutProvider)
    {
        organization.Gateway = GatewayType.Stripe;

        const string braintreeCustomerId = "1";
        const string braintreePaymentMethodToken = "TOKEN";

        var stripeCustomer = new S.Customer
        {
            Metadata = new Dictionary<string, string>
            {
                { "btCustomerId", braintreeCustomerId }
            }
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .CustomerGetAsync(organization.GatewayCustomerId, Arg.Any<S.CustomerGetOptions>())
            .Returns(stripeCustomer);

        var (_, customerGateway, paymentMethodGateway) = Setup(sutProvider.GetDependency<BT.IBraintreeGateway>());

        var braintreeCustomer = Substitute.For<BT.Customer>();

        var paymentMethod = Substitute.For<BT.PaymentMethod>();
        paymentMethod.Token.Returns(braintreePaymentMethodToken);
        paymentMethod.IsDefault.Returns(true);

        braintreeCustomer.PaymentMethods.Returns(new[]
        {
            paymentMethod
        });

        customerGateway.FindAsync(braintreeCustomerId).Returns(braintreeCustomer);

        var updateBraintreeCustomerResult = Substitute.For<BT.Result<BT.Customer>>();
        updateBraintreeCustomerResult.IsSuccess().Returns(false);

        customerGateway.UpdateAsync(
                braintreeCustomerId,
                Arg.Is<BT.CustomerRequest>(request => request.DefaultPaymentMethodToken == null))
            .Returns(updateBraintreeCustomerResult);

        await ThrowsContactSupportAsync(() => sutProvider.Sut.RemovePaymentMethod(organization));

        await customerGateway.Received(1).FindAsync(braintreeCustomerId);

        await customerGateway.Received(1).UpdateAsync(braintreeCustomerId, Arg.Is<BT.CustomerRequest>(request =>
            request.DefaultPaymentMethodToken == null));

        await paymentMethodGateway.DidNotReceiveWithAnyArgs().DeleteAsync(paymentMethod.Token);

        await customerGateway.DidNotReceive().UpdateAsync(braintreeCustomerId, Arg.Is<BT.CustomerRequest>(request =>
            request.DefaultPaymentMethodToken == paymentMethod.Token));
    }

    [Theory, BitAutoData]
    public async Task RemovePaymentMethod_Braintree_PaymentMethodDeleteFails_RollBack_ContactSupport(
        Organization organization,
        SutProvider<RemovePaymentMethodCommand> sutProvider)
    {
        organization.Gateway = GatewayType.Stripe;

        const string braintreeCustomerId = "1";
        const string braintreePaymentMethodToken = "TOKEN";

        var stripeCustomer = new S.Customer
        {
            Metadata = new Dictionary<string, string>
            {
                { "btCustomerId", braintreeCustomerId }
            }
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .CustomerGetAsync(organization.GatewayCustomerId, Arg.Any<S.CustomerGetOptions>())
            .Returns(stripeCustomer);

        var (_, customerGateway, paymentMethodGateway) = Setup(sutProvider.GetDependency<BT.IBraintreeGateway>());

        var braintreeCustomer = Substitute.For<BT.Customer>();

        var paymentMethod = Substitute.For<BT.PaymentMethod>();
        paymentMethod.Token.Returns(braintreePaymentMethodToken);
        paymentMethod.IsDefault.Returns(true);

        braintreeCustomer.PaymentMethods.Returns(new[]
        {
            paymentMethod
        });

        customerGateway.FindAsync(braintreeCustomerId).Returns(braintreeCustomer);

        var updateBraintreeCustomerResult = Substitute.For<BT.Result<BT.Customer>>();
        updateBraintreeCustomerResult.IsSuccess().Returns(true);

        customerGateway.UpdateAsync(braintreeCustomerId, Arg.Any<BT.CustomerRequest>())
            .Returns(updateBraintreeCustomerResult);

        var deleteBraintreePaymentMethodResult = Substitute.For<BT.Result<BT.PaymentMethod>>();
        deleteBraintreePaymentMethodResult.IsSuccess().Returns(false);

        paymentMethodGateway.DeleteAsync(paymentMethod.Token).Returns(deleteBraintreePaymentMethodResult);

        await ThrowsContactSupportAsync(() => sutProvider.Sut.RemovePaymentMethod(organization));

        await customerGateway.Received(1).FindAsync(braintreeCustomerId);

        await customerGateway.Received(1).UpdateAsync(braintreeCustomerId, Arg.Is<BT.CustomerRequest>(request =>
            request.DefaultPaymentMethodToken == null));

        await paymentMethodGateway.Received(1).DeleteAsync(paymentMethod.Token);

        await customerGateway.Received(1).UpdateAsync(braintreeCustomerId, Arg.Is<BT.CustomerRequest>(request =>
            request.DefaultPaymentMethodToken == paymentMethod.Token));
    }

    [Theory, BitAutoData]
    public async Task RemovePaymentMethod_Stripe_Legacy_RemovesSources(
        Organization organization,
        SutProvider<RemovePaymentMethodCommand> sutProvider)
    {
        organization.Gateway = GatewayType.Stripe;

        const string bankAccountId = "bank_account_id";
        const string cardId = "card_id";

        var sources = new List<S.IPaymentSource>
        {
            new S.BankAccount { Id = bankAccountId }, new S.Card { Id = cardId }
        };

        var stripeCustomer = new S.Customer { Sources = new S.StripeList<S.IPaymentSource> { Data = sources } };

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        stripeAdapter
            .CustomerGetAsync(organization.GatewayCustomerId, Arg.Any<S.CustomerGetOptions>())
            .Returns(stripeCustomer);

        stripeAdapter
            .PaymentMethodListAutoPagingAsync(Arg.Any<S.PaymentMethodListOptions>())
            .Returns(GetPaymentMethodsAsync(new List<S.PaymentMethod>()));

        await sutProvider.Sut.RemovePaymentMethod(organization);

        await stripeAdapter.Received(1).BankAccountDeleteAsync(stripeCustomer.Id, bankAccountId);

        await stripeAdapter.Received(1).CardDeleteAsync(stripeCustomer.Id, cardId);

        await stripeAdapter.DidNotReceiveWithAnyArgs()
            .PaymentMethodDetachAsync(Arg.Any<string>(), Arg.Any<S.PaymentMethodDetachOptions>());
    }

    [Theory, BitAutoData]
    public async Task RemovePaymentMethod_Stripe_DetachesPaymentMethods(
        Organization organization,
        SutProvider<RemovePaymentMethodCommand> sutProvider)
    {
        organization.Gateway = GatewayType.Stripe;
        const string bankAccountId = "bank_account_id";
        const string cardId = "card_id";

        var sources = new List<S.IPaymentSource>();

        var stripeCustomer = new S.Customer { Sources = new S.StripeList<S.IPaymentSource> { Data = sources } };

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        stripeAdapter
            .CustomerGetAsync(organization.GatewayCustomerId, Arg.Any<S.CustomerGetOptions>())
            .Returns(stripeCustomer);

        stripeAdapter
            .PaymentMethodListAutoPagingAsync(Arg.Any<S.PaymentMethodListOptions>())
            .Returns(GetPaymentMethodsAsync(new List<S.PaymentMethod>
            {
                new ()
                {
                    Id = bankAccountId
                },
                new ()
                {
                    Id = cardId
                }
            }));

        await sutProvider.Sut.RemovePaymentMethod(organization);

        await stripeAdapter.DidNotReceiveWithAnyArgs().BankAccountDeleteAsync(Arg.Any<string>(), Arg.Any<string>());

        await stripeAdapter.DidNotReceiveWithAnyArgs().CardDeleteAsync(Arg.Any<string>(), Arg.Any<string>());

        await stripeAdapter.Received(1)
            .PaymentMethodDetachAsync(bankAccountId, Arg.Any<S.PaymentMethodDetachOptions>());

        await stripeAdapter.Received(1)
            .PaymentMethodDetachAsync(cardId, Arg.Any<S.PaymentMethodDetachOptions>());
    }

    private static async IAsyncEnumerable<S.PaymentMethod> GetPaymentMethodsAsync(
        IEnumerable<S.PaymentMethod> paymentMethods)
    {
        foreach (var paymentMethod in paymentMethods)
        {
            yield return paymentMethod;
        }

        await Task.CompletedTask;
    }

    private static (BT.IBraintreeGateway, BT.ICustomerGateway, BT.IPaymentMethodGateway) Setup(
        BT.IBraintreeGateway braintreeGateway)
    {
        var customerGateway = Substitute.For<BT.ICustomerGateway>();
        var paymentMethodGateway = Substitute.For<BT.IPaymentMethodGateway>();

        braintreeGateway.Customer.Returns(customerGateway);
        braintreeGateway.PaymentMethod.Returns(paymentMethodGateway);

        return (braintreeGateway, customerGateway, paymentMethodGateway);
    }
}
