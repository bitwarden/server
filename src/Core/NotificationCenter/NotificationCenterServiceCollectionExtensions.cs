#nullable enable
using Bit.Core.NotificationCenter.Authorization;
using Bit.Core.NotificationCenter.Commands;
using Bit.Core.NotificationCenter.Commands.Interfaces;
using Bit.Core.NotificationCenter.Queries;
using Bit.Core.NotificationCenter.Queries.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.NotificationCenter;

public static class NotificationCenterServiceCollectionExtensions
{
    public static void AddNotificationCenterServices(this IServiceCollection services)
    {
        // Authorization Handlers
        services.AddScoped<IAuthorizationHandler, NotificationAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, NotificationStatusAuthorizationHandler>();
        // Commands
        services.AddScoped<ICreateNotificationCommand, CreateNotificationCommand>();
        services.AddScoped<ICreateNotificationStatusCommand, CreateNotificationStatusCommand>();
        services.AddScoped<IMarkNotificationDeletedCommand, MarkNotificationDeletedCommand>();
        services.AddScoped<IMarkNotificationReadCommand, MarkNotificationReadCommand>();
        services.AddScoped<IUpdateNotificationCommand, UpdateNotificationCommand>();
        // Queries
        services.AddScoped<IGetNotificationStatusDetailsForUserQuery, GetNotificationStatusDetailsForUserQuery>();
        services.AddScoped<IGetNotificationStatusForUserQuery, GetNotificationStatusForUserQuery>();
    }
}
