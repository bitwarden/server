using System.Reflection;
using Bit.Seeder;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bit.SeederApi.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Dynamically registers all scene types that implement IScene<TRequest> from the Seeder assembly.
    /// Scenes are registered as keyed scoped services using their class name as the key.
    /// </summary>
    public static IServiceCollection AddScenes(this IServiceCollection services)
    {
        var seederAssembly = Assembly.Load("Seeder");
        var sceneTypes = seederAssembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } &&
                        t.GetInterfaces().Any(i => i.IsGenericType &&
                                                   i.GetGenericTypeDefinition().Name == "IScene`1"));

        foreach (var sceneType in sceneTypes)
        {
            services.TryAddScoped(sceneType);
            services.TryAddKeyedScoped(typeof(IScene), sceneType.Name, (sp, _) => sp.GetRequiredService(sceneType));
        }

        return services;
    }

    /// <summary>
    /// Dynamically registers all query types that implement IQuery<TRequest> from the Seeder assembly.
    /// Queries are registered as keyed scoped services using their class name as the key.
    /// </summary>
    public static IServiceCollection AddQueries(this IServiceCollection services)
    {
        var seederAssembly = Assembly.Load("Seeder");
        var queryTypes = seederAssembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } &&
                        t.GetInterfaces().Any(i => i.IsGenericType &&
                                                   i.GetGenericTypeDefinition().Name == "IQuery`1"));

        foreach (var queryType in queryTypes)
        {
            services.TryAddScoped(queryType);
            services.TryAddKeyedScoped(typeof(IQuery), queryType.Name, (sp, _) => sp.GetRequiredService(queryType));
        }

        return services;
    }
}
