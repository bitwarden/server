using Bit.Commercial.Infrastructure.EntityFramework.SecretsManager.Repositories;
using Bit.Core.SecretsManager.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Commercial.Infrastructure.EntityFramework.SecretsManager;

public static class SecretsManagerEfServiceCollectionExtensions
{
    public static void AddSecretsManagerEfRepositories(this IServiceCollection services)
    {
        services.AddSingleton<IAccessPolicyRepository, AccessPolicyRepository>();
        services.AddSingleton<ISecretRepository, SecretRepository>();
        services.AddSingleton<IProjectRepository, ProjectRepository>();
        services.AddSingleton<IServiceAccountRepository, ServiceAccountRepository>();
    }
}
