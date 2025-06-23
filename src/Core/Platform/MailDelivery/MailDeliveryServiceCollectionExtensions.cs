using Amazon;
using Amazon.SimpleEmail;
using Bit.Core.Platform.MailDelivery;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SendGrid;

namespace Microsoft.Extensions.DependencyInjection;

public static class MailDeliveryServiceCollectionExtensions
{
    /// <summary>
    /// This method registers the best available <see cref="IMailDeliveryService"/> based on the current configuration.
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddMailDelivery(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<SmtpMailOptions>()
            .BindConfiguration("GlobalSettings:Mail:Smtp");

        services.TryAddSingleton(TimeProvider.System);

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IPostConfigureOptions<SmtpMailOptions>, PostConfigureSmtpMailOptions>()
        );

        // These should never actually be created unless configured
        services.TryAddSingleton<IAmazonSimpleEmailService>(sp =>
        {
            var globalSettings = sp.GetRequiredService<GlobalSettings>();
            return new AmazonSimpleEmailServiceClient(
                globalSettings.Amazon.AccessKeyId,
                globalSettings.Amazon.AccessKeySecret,
                RegionEndpoint.GetBySystemName(globalSettings.Amazon.Region)
            );
        });
        services.TryAddSingleton<AmazonSesMailDeliveryService>();

        // SendGrid services
        services.TryAddSingleton<ISendGridClient>(sp =>
        {
            var globalSettings = sp.GetRequiredService<GlobalSettings>();
            return new SendGridClient(globalSettings.Mail.SendGridApiKey, globalSettings.Mail.SendGridApiHost);
        });
        services.TryAddSingleton<SendGridMailDeliveryService>();

        // Smtp Service
        services.TryAddSingleton<MailKitSmtpMailDeliveryService>();

        services.TryAddSingleton<IMailDeliveryService>(sp =>
        {
            var settings = sp.GetRequiredService<GlobalSettings>();

            var awsConfigured = CoreHelpers.SettingHasValue(settings.Amazon?.AccessKeySecret);

            if (awsConfigured && CoreHelpers.SettingHasValue(settings.Mail?.SendGridApiKey))
            {
                return new MultiServiceMailDeliveryService(
                    settings,
                    sp.GetRequiredService<AmazonSesMailDeliveryService>(),
                    sp.GetRequiredService<SendGridMailDeliveryService>()
                );
            }
            else if (awsConfigured)
            {
                return sp.GetRequiredService<AmazonSesMailDeliveryService>();
            }
            else if (CoreHelpers.SettingHasValue(settings.Mail?.Smtp?.Host))
            {
                return sp.GetRequiredService<MailKitSmtpMailDeliveryService>();
            }

            // Fallback to Noop Service
            return new NoopMailDeliveryService();
        });

        // TODO: Configure standard resilience
        services.AddHttpClient(OAuthHandler.HttpClientName);

        return services;
    }
}
