using Bit.Scim.Commands.Groups;
using Bit.Scim.Commands.Groups.Interfaces;

namespace Bit.Scim.Utilities;

public static class ScimServiceCollectionExtensions
{
    public static void AddScimGroupCommands(this IServiceCollection services)
    {
        services.AddScoped<IDeleteGroupCommand, DeleteGroupCommand>();
    }
}
