using Bit.Identity.IdentityServer;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a custom <see cref="IClientProvider"/> for the given identifier to be called when a client id with
    /// the identifier is attempting authentication.
    /// </summary>
    /// <typeparam name="T">Your custom implementation of <see cref="IClientProvider"/>.</typeparam>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="identifier">
    /// The identifier to be used to invoke your client provider if a <c>client_id</c> is prefixed with your identifier
    /// then your <see cref="IClientProvider"/> implementation will be invoked with the data after the seperating <c>.</c>.
    /// </param>
    /// <returns>The <see cref="IServiceCollection"/> for additional chaining.</returns>
    public static IServiceCollection AddClientProvider<T>(this IServiceCollection services, string identifier)
        where T : class, IClientProvider
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        services.AddKeyedTransient<IClientProvider, T>(identifier);

        return services;
    }
}
