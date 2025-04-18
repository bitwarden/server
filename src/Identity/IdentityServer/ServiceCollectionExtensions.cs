using Bit.Identity.IdentityServer;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddClientProvider<T>(this IServiceCollection services, string identifier)
        where T : class, IClientProvider
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        // TODO: Track name so that if they register the same one twice it's an error?

        // TODO: We could allow customization of service lifetime
        services.AddKeyedTransient<IClientProvider, T>(identifier);

        return services;
    }
}
