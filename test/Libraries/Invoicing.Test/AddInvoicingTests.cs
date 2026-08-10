using Bit.Core.Billing.Services;
using Bit.Invoicing.InvoicePreviews;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Bit.Invoicing.Test;

public class AddInvoicingTests
{
    [Fact]
    public void AddInvoicing_RegistersTheServiceClientAndBuilder()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IStripeAdapter>());
        services.AddLogging();

        services.AddInvoicing();
        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IInvoicePreviewService>());
        Assert.NotNull(provider.GetService<InvoicePreviewBuilder>());
    }
}
