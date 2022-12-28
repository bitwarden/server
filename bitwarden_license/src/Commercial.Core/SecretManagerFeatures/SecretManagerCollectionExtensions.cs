using Bit.Commercial.Core.SecretManagerFeatures.AccessPolicies;
using Bit.Commercial.Core.SecretManagerFeatures.AccessTokens;
using Bit.Commercial.Core.SecretManagerFeatures.Projects;
using Bit.Commercial.Core.SecretManagerFeatures.Secrets;
using Bit.Commercial.Core.SecretManagerFeatures.ServiceAccounts;
using Bit.Core.SecretManagerFeatures.AccessPolicies.Interfaces;
using Bit.Core.SecretManagerFeatures.AccessTokens.Interfaces;
using Bit.Core.SecretManagerFeatures.Projects.Interfaces;
using Bit.Core.SecretManagerFeatures.Secrets.Interfaces;
using Bit.Core.SecretManagerFeatures.ServiceAccounts.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Commercial.Core.SecretManagerFeatures;

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
