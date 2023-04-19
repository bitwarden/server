using Bit.Commercial.Core.SecretsManager.AuthorizationHandlers.Projects;
using Bit.Commercial.Core.SecretsManager.AuthorizationHandlers.ServiceAccounts;
using Bit.Commercial.Core.SecretsManager.Commands.AccessPolicies;
using Bit.Commercial.Core.SecretsManager.Commands.AccessTokens;
using Bit.Commercial.Core.SecretsManager.Commands.Porting;
using Bit.Commercial.Core.SecretsManager.Commands.Projects;
using Bit.Commercial.Core.SecretsManager.Commands.Secrets;
using Bit.Commercial.Core.SecretsManager.Commands.ServiceAccounts;
using Bit.Commercial.Core.SecretsManager.Commands.Trash;
using Bit.Core.SecretsManager.Commands.AccessPolicies.Interfaces;
using Bit.Core.SecretsManager.Commands.AccessTokens.Interfaces;
using Bit.Core.SecretsManager.Commands.Porting.Interfaces;
using Bit.Core.SecretsManager.Commands.Projects.Interfaces;
using Bit.Core.SecretsManager.Commands.Secrets.Interfaces;
using Bit.Core.SecretsManager.Commands.ServiceAccounts.Interfaces;
using Bit.Core.SecretsManager.Commands.Trash.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Commercial.Core.SecretsManager;

public static class SecretsManagerCollectionExtensions
{
    public static void AddSecretsManagerServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationHandler, ProjectAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, ServiceAccountAuthorizationHandler>();
        services.AddScoped<ICreateSecretCommand, CreateSecretCommand>();
        services.AddScoped<IUpdateSecretCommand, UpdateSecretCommand>();
        services.AddScoped<IDeleteSecretCommand, DeleteSecretCommand>();
        services.AddScoped<ICreateProjectCommand, CreateProjectCommand>();
        services.AddScoped<IUpdateProjectCommand, UpdateProjectCommand>();
        services.AddScoped<IDeleteProjectCommand, DeleteProjectCommand>();
        services.AddScoped<ICreateServiceAccountCommand, CreateServiceAccountCommand>();
        services.AddScoped<IUpdateServiceAccountCommand, UpdateServiceAccountCommand>();
        services.AddScoped<IDeleteServiceAccountsCommand, DeleteServiceAccountsCommand>();
        services.AddScoped<IRevokeAccessTokensCommand, RevokeAccessTokensCommand>();
        services.AddScoped<ICreateAccessTokenCommand, CreateAccessTokenCommand>();
        services.AddScoped<ICreateAccessPoliciesCommand, CreateAccessPoliciesCommand>();
        services.AddScoped<IUpdateAccessPolicyCommand, UpdateAccessPolicyCommand>();
        services.AddScoped<IDeleteAccessPolicyCommand, DeleteAccessPolicyCommand>();
        services.AddScoped<IImportCommand, ImportCommand>();
        services.AddScoped<IEmptyTrashCommand, EmptyTrashCommand>();
        services.AddScoped<IRestoreTrashCommand, RestoreTrashCommand>();
    }
}
