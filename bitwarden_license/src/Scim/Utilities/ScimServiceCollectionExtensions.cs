using Bit.Scim.Queries.Groups;
using Bit.Scim.Queries.Groups.Interfaces;
using Bit.Scim.Queries.Users;
using Bit.Scim.Queries.Users.Interfaces;

namespace Bit.Scim.Utilities;

public static class ScimServiceCollectionExtensions
{
    public static void AddScimGroupQueries(this IServiceCollection services)
    {
        services.AddScoped<IGetGroupQuery, GetGroupQuery>();
    }

    public static void AddScimUserQueries(this IServiceCollection services)
    {
        services.AddScoped<IGetUserQuery, GetUserQuery>();
    }
}
