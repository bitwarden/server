using Bit.Api.AdminConsole.Authorization;
using Bit.OrganizationKeys.Commands;
using Bit.OrganizationKeys.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bit.OrganizationKeys;

/// <summary>Registration entry point for the organization key feature library.</summary>
public static class OrganizationKeysServiceCollectionExtensions
{
    /// <summary>
    /// Registers the organization key services and the organization authorization they depend on. The host still
    /// registers the Core repositories the services consume.
    /// </summary>
    public static IServiceCollection AddOrganizationKeys(this IServiceCollection services)
    {
        services.AddOrganizationAuthorization();
        services.TryAddScoped<ISetOrganizationKeyIdCommand, SetOrganizationKeyIdCommand>();
        services.TryAddScoped<SetOrganizationKeyIdHandler>();
        return services;
    }
}
