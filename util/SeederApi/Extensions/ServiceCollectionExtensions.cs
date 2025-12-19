using System.Reflection;
using Bit.Seeder;
using Bit.SeederApi.Commands;
using Bit.SeederApi.Commands.Interfaces;
using Bit.SeederApi.Execution;
using Bit.SeederApi.Queries;
using Bit.SeederApi.Queries.Interfaces;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bit.SeederApi.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers SeederApi executors, commands, and queries.
    /// </summary>
    public static IServiceCollection AddSeederApiServices(this IServiceCollection services)
    {
        services.AddScoped<ISceneExecutor, SceneExecutor>();
        services.AddScoped<IQueryExecutor, QueryExecutor>();

        services.AddScoped<IDestroySceneCommand, DestroySceneCommand>();
        services.AddScoped<IDestroyBatchScenesCommand, DestroyBatchScenesCommand>();

        services.AddScoped<IGetAllPlayIdsQuery, GetAllPlayIdsQuery>();

        return services;
    }

    /// <summary>
    /// Dynamically registers all scene types that implement IScene&lt;TRequest&gt; from the Seeder assembly.
    /// Scenes are registered as keyed scoped services using their class name as the key.
    /// </summary>
    public static IServiceCollection AddScenes(this IServiceCollection services)
    {
        var iSceneType1 = typeof(IScene<>);
        var iSceneType2 = typeof(IScene<,>);
        var isIScene = (Type t) => t == iSceneType1 || t == iSceneType2;

        var seederAssembly = Assembly.Load("Seeder");
        var sceneTypes = seederAssembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } &&
                        t.GetInterfaces().Any(i => i.IsGenericType &&
                                                   isIScene(i.GetGenericTypeDefinition())));

        foreach (var sceneType in sceneTypes)
        {
            services.TryAddScoped(sceneType);
            services.TryAddKeyedScoped(typeof(IScene), sceneType.Name, (sp, _) => sp.GetRequiredService(sceneType));
        }

        return services;
    }

    /// <summary>
    /// Dynamically registers all query types that implement IQuery&lt;TRequest&gt; from the Seeder assembly.
    /// Queries are registered as keyed scoped services using their class name as the key.
    /// </summary>
    public static IServiceCollection AddQueries(this IServiceCollection services)
    {
        var iQueryType = typeof(IQuery<,>);
        var seederAssembly = Assembly.Load("Seeder");
        var queryTypes = seederAssembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } &&
                        t.GetInterfaces().Any(i => i.IsGenericType &&
                                                   i.GetGenericTypeDefinition() == iQueryType));

        foreach (var queryType in queryTypes)
        {
            services.TryAddScoped(queryType);
            services.TryAddKeyedScoped(typeof(IQuery), queryType.Name, (sp, _) => sp.GetRequiredService(queryType));
        }

        return services;
    }
}
