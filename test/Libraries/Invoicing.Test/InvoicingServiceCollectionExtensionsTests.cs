using Bitwarden.Server.Sdk.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Bit.Invoicing.Test;

public class InvoicingServiceCollectionExtensionsTests
{
    [Fact]
    public void AddInvoicing_RegistersOwnedFeatureFlagsAsKnownFlags()
    {
        var services = new ServiceCollection();

        services.AddInvoicing();

        var options = services.BuildServiceProvider()
            .GetRequiredService<IOptions<FeatureFlagOptions>>().Value;
        Assert.Contains(InvoicingFeatureFlags.PM36631_PreviewDrivenCart, options.KnownFlags);
    }
}
