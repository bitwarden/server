using Bit.Commercial.Infrastructure.EntityFramework.Repositories;
using Bit.Core.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Commercial.Infrastructure.EntityFramework;

public static class CommercialEFServiceCollectionExtensions
{
    public static void AddCommercialEFRepositories(this IServiceCollection services)
    {
        services.AddSingleton<IAccessPolicyRepository, AccessPolicyRepository>();
        services.AddSingleton<ISecretRepository, SecretRepository>();
        services.AddSingleton<IProjectRepository, ProjectRepository>();
        services.AddSingleton<IServiceAccountRepository, ServiceAccountRepository>();
    }
}
