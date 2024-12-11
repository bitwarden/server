using Bit.Core.Billing.Models;
using Xunit;

namespace Bit.Core.Test.Billing.Models;

public class BillingInfoTests
{
    [Fact]
    public void BillingInvoice_Amount_ShouldComeFrom_InvoiceTotal()
    {
        var invoice = new Stripe.Invoice { AmountDue = 1000, Total = 2000 };

        var billingInvoice = new BillingHistoryInfo.BillingInvoice(invoice);

        // Should have been set from Total
        Assert.Equal(20M, billingInvoice.Amount);
    }
}
