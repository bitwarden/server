using Bit.Core.Billing.Services;
using Bit.Invoicing.InvoicePreviews;
using Bitwarden.Server.Sdk.Environment;
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
        // SelfHosted defaults to false on the substitute, so this exercises the cloud path.
        services.AddSingleton(Substitute.For<IBitwardenEnvironment>());
        services.AddLogging();

        services.AddInvoicing();
        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IInvoicePreviewService>());
        Assert.NotNull(provider.GetService<InvoicePreviewBuilder>());
    }

    [Fact]
    public void AddInvoicing_ResolvingInvoicePreviewServiceInSelfHost_Throws()
    {
        var services = new ServiceCollection();
        var environment = Substitute.For<IBitwardenEnvironment>();
        environment.SelfHosted.Returns(true);
        services.AddSingleton(environment);
        // Registered so the only reason resolution can fail is the self-host guard, not a missing dependency.
        services.AddSingleton(Substitute.For<IStripeAdapter>());
        services.AddLogging();

        services.AddInvoicing();
        var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<IInvoicePreviewService>());
        Assert.Contains("self-host", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
