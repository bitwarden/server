using Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.OrganizationFeatures.OrganizationUsers;

public static class OrganizationUserCollectionExtensions
{
    public static void AddOrganizationUserServices(this IServiceCollection services)
    {
        services.AddOrganizationUserCommands();
    }

    private static void AddOrganizationUserCommands(this IServiceCollection services)
    {
        services.AddScoped<IDeleteOrganizationUserCommand, DeleteOrganizationUserCommand>();
        services.AddScoped<IUpdateOrganizationUserCommand, UpdateOrganizationUserCommand>();
        services.AddScoped<IUpdateOrganizationUserGroupsCommand, UpdateOrganizationUserGroupsCommand>();
    }
}
