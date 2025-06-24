#nullable enable

using Bit.Core.Platform.MailDelivery;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bit.Core.Test.Platform.MailDelivery;

public class MailDeliveryServiceCollectionExtensionTests
{
    [Fact]
    public void OnlyAmazonConfigured_ReturnsAmazonMailDeliveryService()
    {
        var provider = BuildProvider(new Dictionary<string, string?>
        {
            { "GlobalSettings:Amazon:AccessKeySecret", "some_secret" },
            { "GlobalSettings:Amazon:AccessKeyId", "some_id" },
            { "GlobalSettings:Amazon:Region", "us-west-1" },
        });

        var mailDeliveryService = provider.GetRequiredService<IMailDeliveryService>();
        Assert.IsType<AmazonSesMailDeliveryService>(mailDeliveryService);
    }

    [Fact]
    public void AmazonConfigured_SendGridConfigured_ReturnsMulti()
    {
        var provider = BuildProvider(new Dictionary<string, string?>
        {
            { "GlobalSettings:Amazon:AccessKeySecret", "some_secret" },
            { "GlobalSettings:Amazon:AccessKeyId", "some_id" },
            { "GlobalSettings:Amazon:Region", "us-west-1" },
            { "GlobalSettings:Mail:SendGridApiKey", "send_grid_api_key" },
        });

        var mailDeliveryService = provider.GetRequiredService<IMailDeliveryService>();
        Assert.IsType<MultiServiceMailDeliveryService>(mailDeliveryService);
    }

    private static IServiceProvider BuildProvider(Dictionary<string, string?> data)
    {
        var services = new ServiceCollection();

        services.AddTestHostServices(data);

        // Thing being tested:
        services.AddMailDelivery();

        return services.BuildServiceProvider();
    }
}
