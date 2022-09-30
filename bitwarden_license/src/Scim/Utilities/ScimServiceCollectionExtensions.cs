using Bit.Scim.Commands.Users;
using Bit.Scim.Commands.Users.Interfaces;

namespace Bit.Scim.Utilities;

public static class ScimServiceCollectionExtensions
{
    public static void AddScimCommands(this IServiceCollection services)
    {
        services.AddScoped<IPostUserCommand, PostUserCommand>();
    }
}
