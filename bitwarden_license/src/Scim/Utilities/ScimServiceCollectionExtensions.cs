using Bit.Scim.Queries.Users;
using Bit.Scim.Queries.Users.Interfaces;

namespace Bit.Scim.Utilities;

public static class ScimServiceCollectionExtensions
{
    public static void AddScimUserQueries(this IServiceCollection services)
    {
        services.AddScoped<IGetUsersListQuery, GetUsersListQuery>();
    }
}
