using Bit.Core.Commands;
using Bit.Core.Commands.Interfaces;
using Bit.Core.Queries;
using Bit.Core.Queries.Interfaces;
using Bit.Scim.Queries.Users;
using Bit.Scim.Queries.Users.Interfaces;

namespace Bit.Scim.Utilities;

public static class ScimServiceCollectionExtensions
{
    public static void AddScimUserQueries(this IServiceCollection services)
    {
        services.AddScoped<IGetUserQuery, GetUserQuery>();
        services.AddScoped<IOrganizationHasConfirmedOwnersExceptQuery, OrganizationHasConfirmedOwnersExceptQuery>();
    }

    public static void AddScimUserCommands(this IServiceCollection services)
    {
        services.AddScoped<IDeleteOrganizationUserCommand, DeleteOrganizationUserCommand>();
        services.AddScoped<IPushDeleteUserRegistrationOrganizationCommand, PushDeleteUserRegistrationOrganizationCommand>();
    }
}
