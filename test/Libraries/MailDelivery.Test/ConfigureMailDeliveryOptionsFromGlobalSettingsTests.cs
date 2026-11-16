using Bit.Core.Platform.Mail.Delivery;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Bit.MailDelivery.Test;

public class ConfigureMailDeliveryOptionsFromGlobalSettingsTests
{
    [Fact]
    public void BindsFromGlobalSettingsSections()
    {
        var options = ResolveOptions(new Dictionary<string, string?>
        {
            { "globalSettings:siteName", "Bitwarden" },
            { "globalSettings:projectName", "Api" },
            { "globalSettings:mail:replyToEmail", "no-reply@example.com" },
            { "globalSettings:mail:amazonConfigSetName", "config-set" },
            { "globalSettings:mail:sendGridApiKey", "sendgrid-key" },
            { "globalSettings:mail:sendGridApiHost", "https://sendgrid.example.com" },
            { "globalSettings:mail:sendGridPercentage", "25" },
            { "globalSettings:mail:smtp:host", "smtp.example.com" },
            { "globalSettings:mail:smtp:port", "587" },
            { "globalSettings:mail:smtp:startTls", "true" },
            { "globalSettings:mail:smtp:ssl", "true" },
            { "globalSettings:mail:smtp:sslOverride", "true" },
            { "globalSettings:mail:smtp:username", "user" },
            { "globalSettings:mail:smtp:password", "password" },
            { "globalSettings:mail:smtp:trustServer", "true" },
            { "globalSettings:amazon:accessKeyId", "key-id" },
            { "globalSettings:amazon:accessKeySecret", "key-secret" },
            { "globalSettings:amazon:region", "us-east-1" },
        });

        Assert.Equal("Bitwarden", options.SiteName);
        Assert.Equal("Api", options.ProjectName);
        Assert.Equal("no-reply@example.com", options.ReplyToEmail);
        Assert.Equal("config-set", options.AmazonConfigSetName);
        Assert.Equal("sendgrid-key", options.SendGridApiKey);
        Assert.Equal("https://sendgrid.example.com", options.SendGridApiHost);
        Assert.Equal(25, options.SendGridPercentage);
        Assert.Equal("smtp.example.com", options.Smtp.Host);
        Assert.Equal(587, options.Smtp.Port);
        Assert.True(options.Smtp.StartTls);
        Assert.True(options.Smtp.Ssl);
        Assert.True(options.Smtp.SslOverride);
        Assert.Equal("user", options.Smtp.Username);
        Assert.Equal("password", options.Smtp.Password);
        Assert.True(options.Smtp.TrustServer);
        Assert.Equal("key-id", options.Amazon.AccessKeyId);
        Assert.Equal("key-secret", options.Amazon.AccessKeySecret);
        Assert.Equal("us-east-1", options.Amazon.Region);
    }

    [Theory]
    [InlineData("SECRET")]
    [InlineData("REPLACE")]
    [InlineData("secret")]
    public void ClearsPlaceholderSettings(string placeholder)
    {
        var options = ResolveOptions(new Dictionary<string, string?>
        {
            { "globalSettings:mail:sendGridApiKey", placeholder },
            { "globalSettings:mail:smtp:host", placeholder },
            { "globalSettings:mail:smtp:username", placeholder },
            { "globalSettings:mail:smtp:password", placeholder },
            { "globalSettings:amazon:accessKeySecret", placeholder },
        });

        Assert.Null(options.SendGridApiKey);
        Assert.Null(options.Smtp.Host);
        Assert.Null(options.Smtp.Username);
        Assert.Null(options.Smtp.Password);
        Assert.Null(options.Amazon.AccessKeySecret);
    }

    [Fact]
    public void DevSelfHostOverrideSourceNotRegistered_IgnoresSelfHostOverride()
    {
        var options = ResolveOptions(new Dictionary<string, string?>
        {
            { "developSelfHosted", "true" },
            { "globalSettings:mail:smtp:host", "smtp.example.com" },
            { "dev:selfHostOverride:globalSettings:mail:smtp:host", "localhost" },
        });

        Assert.Equal("smtp.example.com", options.Smtp.Host);
    }

    [Fact]
    public void DevSelfHostOverrideSourceRegistered_AppliesSelfHostOverride()
    {
        using var host = new HostBuilder()
            .UseEnvironment(Environments.Development)
            .ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "developSelfHosted", "true" },
                { "globalSettings:siteName", "Bitwarden" },
                { "globalSettings:mail:replyToEmail", "no-reply@example.com" },
                { "globalSettings:mail:smtp:host", "smtp.example.com" },
                { "dev:selfHostOverride:globalSettings:siteName", "Self-hosted" },
                { "dev:selfHostOverride:globalSettings:mail:smtp:host", "localhost" },
            }))
            .ConfigureDevSelfHostOverride()
            .ConfigureServices(services => services.ConfigureMailDeliveryOptionsFromGlobalSettings())
            .Build();

        var options = host.Services.GetRequiredService<IOptions<MailDeliveryOptions>>().Value;

        Assert.Equal("Self-hosted", options.SiteName);
        Assert.Equal("no-reply@example.com", options.ReplyToEmail);
        Assert.Equal("localhost", options.Smtp.Host);
    }

    [Fact]
    public void ClearsPlaceholdersFromOtherConfiguration()
    {
        var options = ResolveOptions(
            new Dictionary<string, string?>(),
            configure: services => services.Configure<MailDeliveryOptions>(o => o.Smtp.Host = "REPLACE"));

        Assert.Null(options.Smtp.Host);
    }

    private static MailDeliveryOptions ResolveOptions(
        Dictionary<string, string?> settings, Action<IServiceCollection>? configure = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        configure?.Invoke(services);
        services.ConfigureMailDeliveryOptionsFromGlobalSettings();

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<MailDeliveryOptions>>().Value;
    }
}
