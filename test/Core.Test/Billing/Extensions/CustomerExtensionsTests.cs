using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Extensions;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Extensions;

public class CustomerExtensionsTests
{
    [Fact]
    public void ApprovedToPayByInvoice_NullMetadata_ReturnsFalse()
    {
        // PM-40292: a deleted Stripe customer retrieve returns a stub with null Metadata.
        // The unguarded TryGetValue previously NRE'd here, crashing the Provider admin page.
        var customer = new Customer { Metadata = null };

        Assert.False(customer.ApprovedToPayByInvoice());
    }

    [Fact]
    public void ApprovedToPayByInvoice_Approved_ReturnsTrue()
    {
        var customer = new Customer
        {
            Metadata = new Dictionary<string, string> { [StripeConstants.MetadataKeys.InvoiceApproved] = "1" }
        };

        Assert.True(customer.ApprovedToPayByInvoice());
    }

    [Fact]
    public void ApprovedToPayByInvoice_NotApproved_ReturnsFalse()
    {
        var customer = new Customer
        {
            Metadata = new Dictionary<string, string> { [StripeConstants.MetadataKeys.InvoiceApproved] = "0" }
        };

        Assert.False(customer.ApprovedToPayByInvoice());
    }

    [Fact]
    public void ApprovedToPayByInvoice_KeyMissing_ReturnsFalse()
    {
        var customer = new Customer { Metadata = new Dictionary<string, string>() };

        Assert.False(customer.ApprovedToPayByInvoice());
    }
}
