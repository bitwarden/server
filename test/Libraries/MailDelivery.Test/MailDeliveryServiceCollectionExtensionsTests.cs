using Bit.Core.Platform.Mail.Delivery;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Bit.MailDelivery.Test;

public class MailDeliveryServiceCollectionExtensionsTests
{
    [Fact]
    public void AddMailDelivery_NothingConfigured_ResolvesNoop()
    {
        var sut = Resolve(_ => { });

        Assert.IsType<NoopMailDeliveryService>(sut);
    }

    [Fact]
    public void AddMailDelivery_WhitespaceSettings_TreatedAsUnset()
    {
        var sut = Resolve(options =>
        {
            options.Smtp.Host = " ";
            options.SendGridApiKey = " ";
            options.Amazon.AccessKeySecret = " ";
        });

        Assert.IsType<NoopMailDeliveryService>(sut);
    }

    [Fact]
    public void AddMailDelivery_SmtpOnly_ResolvesSmtp()
    {
        // Self-hosted installations only configure SMTP.
        var sut = Resolve(options =>
        {
            options.ReplyToEmail = "no-reply@example.com";
            options.Smtp.Host = "smtp.example.com";
        });

        Assert.IsType<MailKitSmtpMailDeliveryService>(sut);
    }

    [Fact]
    public void AddMailDelivery_SendGridWithoutAmazonSes_DoesNotUseSendGrid()
    {
        var sut = Resolve(options =>
        {
            options.ReplyToEmail = "no-reply@example.com";
            options.Smtp.Host = "smtp.example.com";
            options.SendGridApiKey = "api-key";
        });

        Assert.IsType<MailKitSmtpMailDeliveryService>(sut);
    }

    [Fact]
    public void AddMailDelivery_AmazonSes_ResolvesAmazonSes()
    {
        var sut = Resolve(options =>
        {
            ConfigureAmazonSes(options);
            options.Smtp.Host = "smtp.example.com";
        });

        Assert.IsType<AmazonSesMailDeliveryService>(sut);
    }

    [Fact]
    public void AddMailDelivery_AmazonSesAndSendGrid_ResolvesMultiService()
    {
        var sut = Resolve(options =>
        {
            ConfigureAmazonSes(options);
            options.SendGridApiKey = "api-key";
        });

        Assert.IsType<MultiServiceMailDeliveryService>(sut);
    }

    [Fact]
    public void AddMailDelivery_ExistingRegistration_IsNotReplaced()
    {
        var existing = Substitute.For<IMailDeliveryService>();
        var services = CreateServices(options => options.Smtp.Host = "smtp.example.com");
        services.AddSingleton(existing);

        services.AddMailDelivery();

        using var provider = services.BuildServiceProvider();
        Assert.Same(existing, provider.GetRequiredService<IMailDeliveryService>());
    }

    private static void ConfigureAmazonSes(MailDeliveryOptions options)
    {
        options.ReplyToEmail = "no-reply@example.com";
        options.Amazon.AccessKeyId = "access-key-id";
        options.Amazon.AccessKeySecret = "access-key-secret";
        options.Amazon.Region = "us-east-1";
    }

    private static IMailDeliveryService Resolve(Action<MailDeliveryOptions> configure)
    {
        var services = CreateServices(configure);
        services.AddMailDelivery();

        // Intentionally not disposed: the resolved service is returned for type assertions.
        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
        return provider.GetRequiredService<IMailDeliveryService>();
    }

    private static ServiceCollection CreateServices(Action<MailDeliveryOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<IHostEnvironment>());
        services.Configure(configure);
        return services;
    }
}
