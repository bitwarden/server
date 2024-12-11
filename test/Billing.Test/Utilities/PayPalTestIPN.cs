namespace Bit.Billing.Test.Utilities;

public enum IPNBody
{
    SuccessfulPayment,
    ECheckPayment,
    TransactionMissingEntityIds,
    NonUSDPayment,
    SuccessfulPaymentForOrganizationCredit,
    UnsupportedTransactionType,
    SuccessfulRefund,
    RefundMissingParentTransaction,
    SuccessfulPaymentForUserCredit,
}

public static class PayPalTestIPN
{
    public static async Task<string> GetAsync(IPNBody ipnBody)
    {
        var fileName = ipnBody switch
        {
            IPNBody.ECheckPayment => "echeck-payment.txt",
            IPNBody.NonUSDPayment => "non-usd-payment.txt",
            IPNBody.RefundMissingParentTransaction => "refund-missing-parent-transaction.txt",
            IPNBody.SuccessfulPayment => "successful-payment.txt",
            IPNBody.SuccessfulPaymentForOrganizationCredit => "successful-payment-org-credit.txt",
            IPNBody.SuccessfulRefund => "successful-refund.txt",
            IPNBody.SuccessfulPaymentForUserCredit => "successful-payment-user-credit.txt",
            IPNBody.TransactionMissingEntityIds => "transaction-missing-entity-ids.txt",
            IPNBody.UnsupportedTransactionType => "unsupported-transaction-type.txt",
        };

        var content = await EmbeddedResourceReader.ReadAsync("IPN", fileName);

        return content.Replace("\n", string.Empty);
    }
}
