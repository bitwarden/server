using Bit.Commercial.Core.SecretManager.AccessPolicies;
using Bit.Commercial.Core.SecretManager.AccessTokens;
using Bit.Commercial.Core.SecretManager.Projects;
using Bit.Commercial.Core.SecretManager.Secrets;
using Bit.Commercial.Core.SecretManager.ServiceAccounts;
using Bit.Core.SecretManagerFeatures.AccessPolicies.Interfaces;
using Bit.Core.SecretManagerFeatures.AccessTokens.Interfaces;
using Bit.Core.SecretManagerFeatures.Projects.Interfaces;
using Bit.Core.SecretManagerFeatures.Secrets.Interfaces;
using Bit.Core.SecretManagerFeatures.ServiceAccounts.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Commercial.Core.SecretManager;

public static class SecretManagerCollectionExtensions
{
    public static void AddSecretManagerServices(this IServiceCollection services)
    {
        services.AddScoped<ICreateSecretCommand, CreateSecretCommand>();
        services.AddScoped<IUpdateSecretCommand, UpdateSecretCommand>();
        services.AddScoped<IDeleteSecretCommand, DeleteSecretCommand>();
        services.AddScoped<ICreateProjectCommand, CreateProjectCommand>();
        services.AddScoped<IUpdateProjectCommand, UpdateProjectCommand>();
        services.AddScoped<IDeleteProjectCommand, DeleteProjectCommand>();
        services.AddScoped<ICreateServiceAccountCommand, CreateServiceAccountCommand>();
        services.AddScoped<IUpdateServiceAccountCommand, UpdateServiceAccountCommand>();
        services.AddScoped<ICreateAccessTokenCommand, CreateAccessTokenCommand>();
        services.AddScoped<ICreateAccessPoliciesCommand, CreateAccessPoliciesCommand>();
        services.AddScoped<IUpdateAccessPolicyCommand, UpdateAccessPolicyCommand>();
        services.AddScoped<IDeleteAccessPolicyCommand, DeleteAccessPolicyCommand>();
    }
}
