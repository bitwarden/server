using Bit.Scim.Commands.Users;
using Bit.Scim.Commands.Users.Interfaces;
using Bit.Scim.Queries.Users;
using Bit.Scim.Queries.Users.Interfaces;

namespace Bit.Scim.Utilities;

public static class ScimServiceCollectionExtensions
{
    public static void AddScimUserQueries(this IServiceCollection services)
    {
        services.AddScoped<IGetUserQuery, GetUserQuery>();
    }

    public static void AddScimUserCommands(this IServiceCollection services)
    {
        services.AddScoped<IDeleteUserCommand, DeleteUserCommand>();
    }
}
