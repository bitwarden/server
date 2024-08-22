using Bit.Core.Billing.Models;
using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.Billing.Services;

public interface ISubscriberService
{
    /// <summary>
    /// Cancels a subscriber's subscription while including user-provided feedback via the <paramref name="offboardingSurveyResponse"/>.
    /// If the <paramref name="cancelImmediately"/> flag is <see langword="false"/>,
    /// this command sets the subscription's <b>"cancel_at_end_of_period"</b> property to <see langword="true"/>.
    /// Otherwise, this command cancels the subscription immediately.
    /// </summary>
    /// <param name="subscriber">The subscriber with the subscription to cancel.</param>
    /// <param name="offboardingSurveyResponse">An <see cref="OffboardingSurveyResponse"/> DTO containing user-provided feedback on why they are cancelling the subscription.</param>
    /// <param name="cancelImmediately">A flag indicating whether to cancel the subscription immediately or at the end of the subscription period.</param>
    Task CancelSubscription(
        ISubscriber subscriber,
        OffboardingSurveyResponse offboardingSurveyResponse,
        bool cancelImmediately);

    /// <summary>
    /// Retrieves the account credit, a masked representation of the default payment method and the tax information for the
    /// provided <paramref name="subscriber"/>.
    /// </summary>
    /// <param name="subscriber">The subscriber to retrieve payment information for.</param>
    /// <returns>A <see cref="PaymentInformationDTO"/> containing the subscriber's account credit, masked payment method and tax information.</returns>
    Task<PaymentInformationDTO> GetPaymentInformation(
        ISubscriber subscriber);

    /// <summary>
    /// Attempts to remove a subscriber's saved payment method. If the Stripe <see cref="Stripe.Customer"/> representing the
    /// <paramref name="subscriber"/> contains a valid <b>"btCustomerId"</b> key in its <see cref="Stripe.Customer.Metadata"/> property,
    /// this command will attempt to remove the Braintree <see cref="Braintree.PaymentMethod"/>. Otherwise, it will attempt to remove the
    /// Stripe <see cref="Stripe.PaymentMethod"/>.
    /// </summary>
    /// <param name="subscriber">The subscriber to remove the saved payment method for.</param>
    Task RemovePaymentMethod(ISubscriber subscriber);

    /// <summary>
    /// Updates the payment method for the provided <paramref name="subscriber"/> using the <paramref name="tokenizedPaymentMethod"/>.
    /// The following payment method types are supported: [<see cref="PaymentMethodType.Card"/>, <see cref="PaymentMethodType.BankAccount"/>, <see cref="PaymentMethodType.PayPal"/>].
    /// For each type, updating the payment method will attempt to establish a new payment method using the token in the <see cref="TokenizedPaymentMethodDTO"/>. Then, it will
    /// remove the exising payment method(s) linked to the subscriber's customer.
    /// </summary>
    /// <param name="subscriber">The subscriber to update the payment method for.</param>
    /// <param name="tokenizedPaymentMethod">A DTO representing a tokenized payment method.</param>
    Task UpdatePaymentMethod(
        ISubscriber subscriber,
        TokenizedPaymentMethodDTO tokenizedPaymentMethod);

    /// <summary>
    /// Updates the tax information for the provided <paramref name="subscriber"/>.
    /// </summary>
    /// <param name="subscriber">The <paramref name="subscriber"/> to update the tax information for.</param>
    /// <param name="taxInformation">A <see cref="TaxInformation"/> representing the <paramref name="subscriber"/>'s updated tax information.</param>
    Task UpdateTaxInformation(
        ISubscriber subscriber,
        TaxInformation taxInformation);

    /// <summary>
    /// Verifies the subscriber's pending bank account using the provided <paramref name="microdeposits"/>.
    /// </summary>
    /// <param name="subscriber">The subscriber to verify the bank account for.</param>
    /// <param name="microdeposits">Deposits made to the subscriber's bank account in order to ensure they have access to it.
    /// <a href="https://docs.stripe.com/payments/ach-debit/set-up-payment">Learn more.</a></param>
    /// <returns></returns>
    Task VerifyBankAccount(
        ISubscriber subscriber,
        (long, long) microdeposits);
}
