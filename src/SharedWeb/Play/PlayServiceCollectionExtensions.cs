using Bit.Core.Repositories;
using Bit.SharedWeb.Play.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.SharedWeb.Play;

public static class PlayServiceCollectionExtensions
{
    /// <summary>
    /// Adds PlayId tracking decorators for User and Organization repositories using Dapper implementations.
    /// This replaces the standard repository implementations with tracking versions
    /// that record created entities for test data cleanup. Only call when TestPlayIdTrackingEnabled is true.
    /// </summary>
    public static void AddPlayIdTrackingDapperRepositories(this IServiceCollection services)
    {
        services.AddSingleton<IOrganizationRepository, DapperTestOrganizationTrackingOrganizationRepository>();
        services.AddSingleton<IUserRepository, DapperTestUserTrackingUserRepository>();
    }

    /// <summary>
    /// Adds PlayId tracking decorators for User and Organization repositories using EntityFramework implementations.
    /// This replaces the standard repository implementations with tracking versions
    /// that record created entities for test data cleanup. Only call when TestPlayIdTrackingEnabled is true.
    /// </summary>
    public static void AddPlayIdTrackingEFRepositories(this IServiceCollection services)
    {
        services.AddSingleton<IOrganizationRepository, EFTestOrganizationTrackingOrganizationRepository>();
        services.AddSingleton<IUserRepository, EFTestUserTrackingUserRepository>();
    }
}
