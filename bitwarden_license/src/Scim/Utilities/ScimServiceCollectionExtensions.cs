using Bit.Scim.Queries.Groups;
using Bit.Scim.Queries.Groups.Interfaces;

namespace Bit.Scim.Utilities;

public static class ScimServiceCollectionExtensions
{
    public static void AddScimCommands(this IServiceCollection services)
    {
        services.AddScoped<IGetGroupsListQuery, GetGroupsListQuery>();
    }
}
