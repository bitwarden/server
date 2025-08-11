using Bit.Core.Vault.Commands;
using Bit.Core.Vault.Commands.Interfaces;
using Bit.Core.Vault.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Vault;

public static class VaultServiceCollectionExtensions
{
    public static IServiceCollection AddVaultServices(this IServiceCollection services)
    {
        services.AddVaultQueries();

        return services;
    }

    private static void AddVaultQueries(this IServiceCollection services)
    {
        services.AddScoped<IOrganizationCiphersQuery, OrganizationCiphersQuery>();
        services.AddScoped<IGetTaskDetailsForUserQuery, GetTaskDetailsForUserQuery>();
        services.AddScoped<IMarkTaskAsCompleteCommand, MarkTaskAsCompletedCommand>();
        services.AddScoped<IGetCipherPermissionsForUserQuery, GetCipherPermissionsForUserQuery>();
        services.AddScoped<IGetTasksForOrganizationQuery, GetTasksForOrganizationQuery>();
        services.AddScoped<IGetSecurityTasksNotificationDetailsQuery, GetSecurityTasksNotificationDetailsQuery>();
        services.AddScoped<ICreateManyTaskNotificationsCommand, CreateManyTaskNotificationsCommand>();
        services.AddScoped<ICreateManyTasksCommand, CreateManyTasksCommand>();
        services.AddScoped<IArchiveCiphersCommand, ArchiveCiphersCommand>();
        services.AddScoped<IUnarchiveCiphersCommand, UnarchiveCiphersCommand>();
        services.AddScoped<IMarkNotificationsForTaskAsDeletedCommand, MarkNotificationsForTaskAsDeletedCommand>();
    }
}
