﻿using Bit.Commercial.Core.SecretsManager.AuthorizationHandlers.AccessPolicies;
using Bit.Commercial.Core.SecretsManager.AuthorizationHandlers.Projects;
using Bit.Commercial.Core.SecretsManager.AuthorizationHandlers.Secrets;
using Bit.Commercial.Core.SecretsManager.AuthorizationHandlers.ServiceAccounts;
using Bit.Commercial.Core.SecretsManager.Commands.AccessPolicies;
using Bit.Commercial.Core.SecretsManager.Commands.AccessTokens;
using Bit.Commercial.Core.SecretsManager.Commands.Porting;
using Bit.Commercial.Core.SecretsManager.Commands.Projects;
using Bit.Commercial.Core.SecretsManager.Commands.Requests;
using Bit.Commercial.Core.SecretsManager.Commands.Secrets;
using Bit.Commercial.Core.SecretsManager.Commands.ServiceAccounts;
using Bit.Commercial.Core.SecretsManager.Commands.Trash;
using Bit.Commercial.Core.SecretsManager.Queries;
using Bit.Commercial.Core.SecretsManager.Queries.AccessPolicies;
using Bit.Commercial.Core.SecretsManager.Queries.Projects;
using Bit.Commercial.Core.SecretsManager.Queries.Secrets;
using Bit.Commercial.Core.SecretsManager.Queries.ServiceAccounts;
using Bit.Core.SecretsManager.Commands.AccessPolicies.Interfaces;
using Bit.Core.SecretsManager.Commands.AccessTokens.Interfaces;
using Bit.Core.SecretsManager.Commands.Porting.Interfaces;
using Bit.Core.SecretsManager.Commands.Projects.Interfaces;
using Bit.Core.SecretsManager.Commands.Requests.Interfaces;
using Bit.Core.SecretsManager.Commands.Secrets.Interfaces;
using Bit.Core.SecretsManager.Commands.ServiceAccounts.Interfaces;
using Bit.Core.SecretsManager.Commands.Trash.Interfaces;
using Bit.Core.SecretsManager.Queries.AccessPolicies.Interfaces;
using Bit.Core.SecretsManager.Queries.Interfaces;
using Bit.Core.SecretsManager.Queries.Projects.Interfaces;
using Bit.Core.SecretsManager.Queries.Secrets.Interfaces;
using Bit.Core.SecretsManager.Queries.ServiceAccounts.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Commercial.Core.SecretsManager;

public static class SecretsManagerCollectionExtensions
{
    public static void AddSecretsManagerServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationHandler, ProjectAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, SecretAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, ServiceAccountAuthorizationHandler>();
        services.AddScoped<
            IAuthorizationHandler,
            ProjectPeopleAccessPoliciesAuthorizationHandler
        >();
        services.AddScoped<
            IAuthorizationHandler,
            ServiceAccountPeopleAccessPoliciesAuthorizationHandler
        >();
        services.AddScoped<
            IAuthorizationHandler,
            ServiceAccountGrantedPoliciesAuthorizationHandler
        >();
        services.AddScoped<
            IAuthorizationHandler,
            ProjectServiceAccountsAccessPoliciesAuthorizationHandler
        >();
        services.AddScoped<
            IAuthorizationHandler,
            SecretAccessPoliciesUpdatesAuthorizationHandler
        >();
        services.AddScoped<IAuthorizationHandler, BulkSecretAuthorizationHandler>();
        services.AddScoped<IAccessClientQuery, AccessClientQuery>();
        services.AddScoped<IMaxProjectsQuery, MaxProjectsQuery>();
        services.AddScoped<ISameOrganizationQuery, SameOrganizationQuery>();
        services.AddScoped<IServiceAccountSecretsDetailsQuery, ServiceAccountSecretsDetailsQuery>();
        services.AddScoped<
            IServiceAccountGrantedPolicyUpdatesQuery,
            ServiceAccountGrantedPolicyUpdatesQuery
        >();
        services.AddScoped<ISecretAccessPoliciesUpdatesQuery, SecretAccessPoliciesUpdatesQuery>();
        services.AddScoped<ISecretsSyncQuery, SecretsSyncQuery>();
        services.AddScoped<
            IProjectServiceAccountsAccessPoliciesUpdatesQuery,
            ProjectServiceAccountsAccessPoliciesUpdatesQuery
        >();
        services.AddScoped<ICreateSecretCommand, CreateSecretCommand>();
        services.AddScoped<IUpdateSecretCommand, UpdateSecretCommand>();
        services.AddScoped<IDeleteSecretCommand, DeleteSecretCommand>();
        services.AddScoped<ICreateProjectCommand, CreateProjectCommand>();
        services.AddScoped<IRequestSMAccessCommand, RequestSMAccessCommand>();
        services.AddScoped<IUpdateProjectCommand, UpdateProjectCommand>();
        services.AddScoped<IDeleteProjectCommand, DeleteProjectCommand>();
        services.AddScoped<ICreateServiceAccountCommand, CreateServiceAccountCommand>();
        services.AddScoped<IUpdateServiceAccountCommand, UpdateServiceAccountCommand>();
        services.AddScoped<IDeleteServiceAccountsCommand, DeleteServiceAccountsCommand>();
        services.AddScoped<
            ICountNewServiceAccountSlotsRequiredQuery,
            CountNewServiceAccountSlotsRequiredQuery
        >();
        services.AddScoped<IRevokeAccessTokensCommand, RevokeAccessTokensCommand>();
        services.AddScoped<ICreateAccessTokenCommand, CreateAccessTokenCommand>();
        services.AddScoped<IImportCommand, ImportCommand>();
        services.AddScoped<IEmptyTrashCommand, EmptyTrashCommand>();
        services.AddScoped<IRestoreTrashCommand, RestoreTrashCommand>();
        services.AddScoped<
            IUpdateServiceAccountGrantedPoliciesCommand,
            UpdateServiceAccountGrantedPoliciesCommand
        >();
        services.AddScoped<
            IUpdateProjectServiceAccountsAccessPoliciesCommand,
            UpdateProjectServiceAccountsAccessPoliciesCommand
        >();
    }
}
