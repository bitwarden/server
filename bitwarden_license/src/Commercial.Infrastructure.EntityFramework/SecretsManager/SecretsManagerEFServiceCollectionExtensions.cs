using AutoMapper;
using Bit.Commercial.Infrastructure.EntityFramework.SecretsManager.Repositories;
using Bit.Core.Health;
using Bit.Core.SecretsManager.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Commercial.Infrastructure.EntityFramework.SecretsManager;

public static class SecretsManagerEfServiceCollectionExtensions
{
    public static void AddSecretsManagerEfRepositories(this IServiceCollection services)
    {
        services.AddSingleton<IAccessPolicyRepository, AccessPolicyRepository>();
        services.AddSingleton<ISecretRepository>(sp =>
        {
            var serviceScopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            var mapper = sp.GetRequiredService<IMapper>();
            return ActivityDecoratedProxy<ISecretRepository>.Create(new SecretRepository(serviceScopeFactory, mapper), sp);
        });
        services.AddSingleton<IProjectRepository, ProjectRepository>();
        services.AddSingleton<IServiceAccountRepository, ServiceAccountRepository>();
    }
}
