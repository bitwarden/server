using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Bit.Core.Platform.Mail.Delivery;

/// <summary>
/// Registers mail delivery services.
/// </summary>
public static class MailDeliveryServiceCollectionExtensions
{
    /// <summary>
    /// Registers an <see cref="IMailDeliveryService"/> that sends through the provider configured in
    /// <see cref="MailDeliveryOptions"/>, or drops mail when no provider is configured. The provider is
    /// chosen when the service is first resolved. The host is responsible for configuring
    /// <see cref="MailDeliveryOptions"/>.
    /// </summary>
    public static IServiceCollection AddMailDelivery(this IServiceCollection services)
    {
        services.AddOptions<MailDeliveryOptions>();

        services.TryAddSingleton<AmazonSesMailDeliveryService>();
        services.TryAddSingleton<SendGridMailDeliveryService>();
        services.TryAddSingleton<MultiServiceMailDeliveryService>();
        services.TryAddSingleton<MailKitSmtpMailDeliveryService>();
        services.TryAddSingleton<NoopMailDeliveryService>();
        services.TryAddSingleton<IMailDeliveryService>(ResolveMailDeliveryService);

        return services;
    }

    /// <summary>
    /// Configures <see cref="MailDeliveryOptions"/> from the legacy <c>globalSettings</c> configuration section.
    /// Only use this in a service that already reads its configuration from <c>globalSettings</c>; new services
    /// should configure <see cref="MailDeliveryOptions"/> directly. This does not register mail delivery itself;
    /// call <see cref="AddMailDelivery"/> as well.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reads <c>globalSettings:mail</c>, <c>globalSettings:amazon</c>, <c>globalSettings:siteName</c>, and
    /// <c>globalSettings:projectName</c> from <see cref="IConfiguration"/>. Values under
    /// <c>dev:selfHostOverride:globalSettings</c> only apply when the host registers GlobalSettingsBridge's
    /// <c>ConfigureDevSelfHostOverride()</c>.
    /// </para>
    /// <para>
    /// The placeholder values <c>SECRET</c> and <c>REPLACE</c> (case-insensitive) are treated as unset for the SMTP
    /// host, username, and password, the SendGrid API key, and the Amazon access key secret. This runs as a
    /// post-configure step, so it also applies to values set by any other configuration of
    /// <see cref="MailDeliveryOptions"/>.
    /// </para>
    /// </remarks>
    public static IServiceCollection ConfigureMailDeliveryOptionsFromGlobalSettings(this IServiceCollection services)
    {
        services.AddOptions<MailDeliveryOptions>()
            .Configure<IConfiguration>(BindFromGlobalSettings)
            .PostConfigure(ClearGlobalSettingsPlaceholders);

        return services;
    }

    private static void BindFromGlobalSettings(MailDeliveryOptions options, IConfiguration configuration)
    {
        var globalSettings = configuration.GetSection("GlobalSettings");
        globalSettings.GetSection("Mail").Bind(options);
        globalSettings.GetSection("Amazon").Bind(options.Amazon);
        options.SiteName = globalSettings["SiteName"];
        options.ProjectName = globalSettings["ProjectName"];
    }

    private static void ClearGlobalSettingsPlaceholders(MailDeliveryOptions options)
    {
        static string? ValueOrNull(string? setting)
        {
            var normalizedSetting = setting?.ToLowerInvariant();
            return normalizedSetting is "secret" or "replace" ? null : setting;
        }

        options.Smtp.Host = ValueOrNull(options.Smtp.Host);
        options.Smtp.Username = ValueOrNull(options.Smtp.Username);
        options.Smtp.Password = ValueOrNull(options.Smtp.Password);
        options.SendGridApiKey = ValueOrNull(options.SendGridApiKey);
        options.Amazon.AccessKeySecret = ValueOrNull(options.Amazon.AccessKeySecret);
    }

    private static IMailDeliveryService ResolveMailDeliveryService(IServiceProvider serviceProvider)
    {
        var options = serviceProvider.GetRequiredService<IOptions<MailDeliveryOptions>>().Value;

        var awsConfigured = !string.IsNullOrWhiteSpace(options.Amazon.AccessKeySecret);
        if (awsConfigured && !string.IsNullOrWhiteSpace(options.SendGridApiKey))
        {
            return serviceProvider.GetRequiredService<MultiServiceMailDeliveryService>();
        }

        if (awsConfigured)
        {
            return serviceProvider.GetRequiredService<AmazonSesMailDeliveryService>();
        }

        if (!string.IsNullOrWhiteSpace(options.Smtp.Host))
        {
            return serviceProvider.GetRequiredService<MailKitSmtpMailDeliveryService>();
        }

        return serviceProvider.GetRequiredService<NoopMailDeliveryService>();
    }
}
