using Bit.Core.Models.Business;
using Xunit;

namespace Bit.Core.Test.Models.Business;

public class BillingInfoTests
{
    [Fact]
    public void BillingInvoice_Amount_ShouldComeFrom_InvoiceTotal()
    {
        var invoice = new Stripe.Invoice
        {
            AmountDue = 1000,
            Total = 2000,
        };

        var billingInvoice = new BillingInfo.BillingInvoice(invoice);

        // Should have been set from Total
        Assert.Equal(20M, billingInvoice.Amount);
    }
}
