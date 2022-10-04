using Bit.Scim.Commands.Groups;
using Bit.Scim.Commands.Groups.Interfaces;
using Bit.Scim.Queries.Users;
using Bit.Scim.Queries.Users.Interfaces;

namespace Bit.Scim.Utilities;

public static class ScimServiceCollectionExtensions
{
    public static void AddScimGroupCommands(this IServiceCollection services)
    {
        services.AddScoped<IPutGroupCommand, PutGroupCommand>();
    }

    public static void AddScimUserQueries(this IServiceCollection services)
    {
        services.AddScoped<IGetUserQuery, GetUserQuery>();
    }
}
