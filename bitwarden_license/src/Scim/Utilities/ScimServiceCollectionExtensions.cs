using Bit.Core.OrganizationFeatures.OrganizationUsers;
using Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Scim.Users;
using Bit.Scim.Users.Interfaces;

namespace Bit.Scim.Utilities;

public static class ScimServiceCollectionExtensions
{
    public static void AddScimGroupCommands(this IServiceCollection services)
    {
        services.AddScoped<IDeleteGroupCommand, DeleteGroupCommand>();
    }

    public static void AddScimUserQueries(this IServiceCollection services)
    {
        services.AddScoped<IGetUserQuery, GetUserQuery>();
    }
}
