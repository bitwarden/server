using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bit.Core.Platform.Mailer;

#nullable enable

/// <summary>
/// Extension methods for adding the Mailer feature to the service collection.
/// </summary>
public static class MailerServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Mailer services to the <see cref="IServiceCollection"/>.
    /// This includes the mail renderer and mailer for sending templated emails.
    /// This method is safe to be run multiple times.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <returns>The <see cref="IServiceCollection"/> for additional chaining.</returns>
    public static IServiceCollection AddMailer(this IServiceCollection services)
    {
        services.TryAddSingleton<IMailRenderer, HandlebarMailRenderer>();
        services.TryAddSingleton<IMailer, Mailer>();

        return services;
    }
}
