using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bit.CurrentUser;

/// <summary>
/// Dependency injection entry point for the CurrentUser library.
/// </summary>
public static class CurrentUserServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ICurrentUserContext"/>, backed by the current <c>HttpContext</c>.
    /// Uses TryAdd so a host may pre-register a test double.
    /// </summary>
    public static IServiceCollection AddCurrentUser(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.TryAddScoped<ICurrentUserContext, HttpCurrentUserContext>();
        return services;
    }
}
