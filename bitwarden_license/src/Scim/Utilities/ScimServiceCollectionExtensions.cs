﻿using Bit.Scim.Groups;
using Bit.Scim.Groups.Interfaces;
using Bit.Scim.Users;
using Bit.Scim.Users.Interfaces;

namespace Bit.Scim.Utilities;

public static class ScimServiceCollectionExtensions
{
    public static void AddScimGroupCommands(this IServiceCollection services)
    {
        services.AddScoped<IPatchGroupCommand, PatchGroupCommand>();
        services.AddScoped<IPostGroupCommand, PostGroupCommand>();
        services.AddScoped<IPutGroupCommand, PutGroupCommand>();
    }

    public static void AddScimGroupQueries(this IServiceCollection services)
    {
        services.AddScoped<IGetGroupsListQuery, GetGroupsListQuery>();
    }

    public static void AddScimUserCommands(this IServiceCollection services)
    {
        services.AddScoped<IPatchUserCommand, PatchUserCommand>();
        services.AddScoped<IPostUserCommand, PostUserCommand>();
    }

    public static void AddScimUserQueries(this IServiceCollection services)
    {
        services.AddScoped<IGetUsersListQuery, GetUsersListQuery>();
    }
}
